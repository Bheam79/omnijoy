<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as signalR from '@microsoft/signalr'
import Hls from 'hls.js'
import { useAuthStore } from '@/stores/auth'
import { useLiveStore } from '@/stores/live'
import { liveService, type LiveStreamDto } from '@/services/liveService'

// ── Setup ─────────────────────────────────────────────────────────────────────

const route  = useRoute()
const router = useRouter()
const auth   = useAuthStore()
const liveStore = useLiveStore()

const streamId = computed(() => route.params.id as string)

// ── State ─────────────────────────────────────────────────────────────────────

const stream      = ref<LiveStreamDto | null>(null)
const loading     = ref(true)
const error       = ref<string | null>(null)
const viewerCount = ref(0)
const isHost      = computed(() => stream.value?.host.id === auth.user?.id)

// HLS player
const videoEl    = ref<HTMLVideoElement | null>(null)
let hls: Hls | null = null

// Live chat
interface ChatMessage {
  id: string
  userId: string
  displayName: string
  message: string
  sentAt: string
}
const chatMessages = ref<ChatMessage[]>([])
const chatInput    = ref('')
const chatContainer = ref<HTMLElement | null>(null)
const sendingChat  = ref(false)

// SignalR
let hubConnection: signalR.HubConnection | null = null

// ── Lifecycle ─────────────────────────────────────────────────────────────────

onMounted(async () => {
  await loadStream()
  if (stream.value) {
    connectSignalR()
  }
})

onUnmounted(() => {
  destroyHls()
  hubConnection?.invoke('LeaveStream', streamId.value).catch(() => {})
  hubConnection?.stop()
})

// ── Load stream ───────────────────────────────────────────────────────────────

async function loadStream() {
  loading.value = true
  error.value   = null
  try {
    stream.value = await liveService.getStream(streamId.value)
    viewerCount.value = stream.value.viewerCount
    if (stream.value.status === 'Live' && stream.value.hlsUrl) {
      await nextTick()
      initPlayer(stream.value.hlsUrl)
    }
  } catch (e: unknown) {
    error.value = extractError(e)
  } finally {
    loading.value = false
  }
}

// ── HLS Player ────────────────────────────────────────────────────────────────

function initPlayer(hlsUrl: string) {
  if (!videoEl.value) return
  destroyHls()

  // Add JWT token to the proxied HLS URL as a query param
  const token = auth.accessToken
  const url   = token ? `${hlsUrl}?access_token=${encodeURIComponent(token)}` : hlsUrl

  if (Hls.isSupported()) {
    hls = new Hls({
      xhrSetup(xhr) {
        // Attach auth header for segment requests
        if (token) xhr.setRequestHeader('Authorization', `Bearer ${token}`)
      },
      liveSyncDurationCount: 3,
      liveMaxLatencyDurationCount: 10,
    })
    hls.loadSource(url)
    hls.attachMedia(videoEl.value)
    hls.on(Hls.Events.MANIFEST_PARSED, () => {
      videoEl.value?.play().catch(() => {})
    })
  } else if (videoEl.value.canPlayType('application/vnd.apple.mpegurl')) {
    // Safari / iOS native HLS
    videoEl.value.src = url
    videoEl.value.play().catch(() => {})
  }
}

function destroyHls() {
  hls?.destroy()
  hls = null
}

// ── SignalR ───────────────────────────────────────────────────────────────────

function connectSignalR() {
  if (!auth.accessToken) return

  hubConnection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/live', { accessTokenFactory: () => auth.accessToken ?? '' })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  // Viewer count updates
  hubConnection.on('ViewerJoined', (_sid: string, count: number) => {
    viewerCount.value = count
  })
  hubConnection.on('ViewerLeft', (_sid: string, count: number) => {
    viewerCount.value = count
  })

  // Live chat
  hubConnection.on('LiveChatMessage', (payload: {
    streamId: string
    userId: string
    message: string
    sentAt: string
  }) => {
    chatMessages.value.push({
      id: crypto.randomUUID(),
      userId: payload.userId,
      displayName: payload.userId === auth.user?.id ? (auth.user.displayName) : payload.userId,
      message: payload.message,
      sentAt: payload.sentAt,
    })
    scrollChat()
  })

  // Stream ended
  hubConnection.on('StreamEnded', (id: string) => {
    if (id !== streamId.value) return
    if (stream.value) {
      stream.value = { ...stream.value, status: 'Ended', hlsUrl: undefined }
    }
    destroyHls()
    liveStore.removeStream(id)
  })

  hubConnection.start().then(() => {
    hubConnection!.invoke('JoinStream', streamId.value).catch(console.error)
  }).catch(console.error)
}

// ── Send chat message ─────────────────────────────────────────────────────────

async function sendChat() {
  const msg = chatInput.value.trim()
  if (!msg || sendingChat.value || !hubConnection) return

  sendingChat.value = true
  chatInput.value = ''
  try {
    await hubConnection.invoke('SendLiveChat', streamId.value, msg)
  } catch (e) {
    chatInput.value = msg // restore on failure
    console.error(e)
  } finally {
    sendingChat.value = false
  }
}

// ── End stream (host only) ────────────────────────────────────────────────────

async function endStream() {
  if (!confirm('Are you sure you want to end your stream?')) return
  try {
    await liveStore.endStream(streamId.value)
    router.push('/live')
  } catch (e) {
    alert(extractError(e))
  }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function scrollChat() {
  nextTick(() => {
    if (chatContainer.value) {
      chatContainer.value.scrollTop = chatContainer.value.scrollHeight
    }
  })
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const ax = e as { response?: { data?: { error?: string } }; message?: string }
    return ax.response?.data?.error ?? ax.message ?? 'An unexpected error occurred.'
  }
  return 'An unexpected error occurred.'
}
</script>

<template>
  <div class="max-w-7xl mx-auto px-4 py-6">
    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center h-64">
      <div class="w-10 h-10 border-4 border-indigo-200 border-t-indigo-600 rounded-full animate-spin" />
    </div>

    <!-- Error -->
    <div
      v-else-if="error"
      class="bg-red-50 border border-red-200 rounded-xl p-6 text-red-700 text-center"
    >
      <p class="font-semibold mb-1">Could not load stream</p>
      <p class="text-sm">{{ error }}</p>
      <button
        class="mt-4 px-4 py-2 bg-red-600 text-white text-sm rounded-xl hover:bg-red-700 transition"
        @click="$router.back()"
      >
        Go back
      </button>
    </div>

    <!-- Stream page -->
    <template v-else-if="stream">
      <!-- Header -->
      <div class="flex items-start justify-between mb-4 gap-4">
        <div class="min-w-0">
          <div class="flex items-center gap-2 mb-1">
            <span
              v-if="stream.status === 'Live'"
              class="inline-flex items-center gap-1.5 px-2 py-0.5 bg-red-600 text-white text-xs font-bold rounded-full"
            >
              <span class="w-1.5 h-1.5 rounded-full bg-white animate-pulse" />
              LIVE
            </span>
            <span
              v-else
              class="inline-flex items-center gap-1.5 px-2 py-0.5 bg-gray-200 text-gray-600 text-xs font-semibold rounded-full"
            >
              ENDED
            </span>
          </div>
          <h1 class="text-xl font-bold text-gray-900 truncate">{{ stream.title }}</h1>
          <div class="flex items-center gap-2 mt-1 text-sm text-gray-500">
            <img
              v-if="stream.host.avatarUrl"
              :src="stream.host.avatarUrl"
              class="w-5 h-5 rounded-full object-cover"
            />
            <div
              v-else
              class="w-5 h-5 rounded-full bg-indigo-200 flex items-center justify-center text-indigo-700 text-[10px] font-bold"
            >
              {{ stream.host.displayName.charAt(0).toUpperCase() }}
            </div>
            <span>{{ stream.host.displayName }}</span>
            <span class="text-gray-300">·</span>
            <span class="flex items-center gap-1">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"/>
              </svg>
              {{ viewerCount }} watching
            </span>
          </div>
        </div>

        <!-- End stream button (host only) -->
        <button
          v-if="isHost && stream.status === 'Live'"
          class="shrink-0 flex items-center gap-2 px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-sm font-semibold rounded-xl transition"
          @click="endStream"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M9 10a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1h-4a1 1 0 01-1-1v-4z"/>
          </svg>
          End stream
        </button>
      </div>

      <!-- Main content: player + chat -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <!-- Video player -->
        <div class="lg:col-span-2">
          <div class="relative bg-black rounded-2xl overflow-hidden aspect-video">
            <!-- Live video -->
            <video
              v-if="stream.status === 'Live'"
              ref="videoEl"
              class="w-full h-full object-contain"
              playsinline
              controls
            />

            <!-- Stream ended overlay -->
            <div
              v-else
              class="absolute inset-0 flex flex-col items-center justify-center text-white"
            >
              <svg class="w-16 h-16 text-gray-500 mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1"
                      d="M15 10l4.553-2.069A1 1 0 0121 8.876V15.124a1 1 0 01-1.447.894L15 14M3 8a2 2 0 012-2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V8z"/>
              </svg>
              <p class="text-lg font-semibold text-gray-300">Stream has ended</p>
              <button
                class="mt-4 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-medium rounded-xl transition"
                @click="$router.push('/live')"
              >
                Browse live streams
              </button>
            </div>
          </div>
        </div>

        <!-- Chat sidebar -->
        <div class="flex flex-col bg-white rounded-2xl border border-gray-100 overflow-hidden" style="min-height: 400px; max-height: 600px;">
          <!-- Chat header -->
          <div class="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
            <span class="text-sm font-semibold text-gray-800">Live Chat</span>
            <span class="text-xs text-gray-400">{{ chatMessages.length }} messages</span>
          </div>

          <!-- Messages -->
          <div
            ref="chatContainer"
            class="flex-1 overflow-y-auto p-3 space-y-2 scroll-smooth"
          >
            <div
              v-if="chatMessages.length === 0"
              class="flex flex-col items-center justify-center h-full text-gray-400 py-8"
            >
              <svg class="w-8 h-8 mb-2 opacity-50" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
                      d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"/>
              </svg>
              <p class="text-xs">Be the first to say something!</p>
            </div>

            <div
              v-for="msg in chatMessages"
              :key="msg.id"
              class="flex gap-2 text-sm"
            >
              <div class="shrink-0 w-6 h-6 rounded-full bg-indigo-200 flex items-center justify-center text-indigo-700 text-[10px] font-bold mt-0.5">
                {{ (msg.displayName || 'U').charAt(0).toUpperCase() }}
              </div>
              <div class="min-w-0">
                <span class="font-semibold text-gray-800 text-xs">{{ msg.displayName || 'User' }}</span>
                <span class="text-gray-400 text-[10px] ml-1">{{ formatTime(msg.sentAt) }}</span>
                <p class="text-gray-700 text-xs break-words">{{ msg.message }}</p>
              </div>
            </div>
          </div>

          <!-- Input -->
          <div class="p-3 border-t border-gray-100">
            <form class="flex items-center gap-2" @submit.prevent="sendChat">
              <input
                v-model="chatInput"
                type="text"
                placeholder="Say something…"
                maxlength="500"
                :disabled="stream.status !== 'Live'"
                class="flex-1 px-3 py-2 bg-gray-50 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent disabled:opacity-50"
              />
              <button
                type="submit"
                :disabled="!chatInput.trim() || sendingChat || stream.status !== 'Live'"
                class="shrink-0 px-3 py-2 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white text-sm rounded-xl transition"
              >
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8"/>
                </svg>
              </button>
            </form>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
