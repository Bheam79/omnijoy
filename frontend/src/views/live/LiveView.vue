<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useLiveStore } from '@/stores/live'
import { useAuthStore } from '@/stores/auth'
import StreamCard from '@/components/live/StreamCard.vue'
import GoLiveModal from '@/components/live/GoLiveModal.vue'
import type { StartStreamResponse } from '@/services/liveService'
import { useRouter } from 'vue-router'

const liveStore  = useLiveStore()
const auth       = useAuthStore()
const router     = useRouter()

const showGoLiveModal = ref(false)
let hubConnection: signalR.HubConnection | null = null

onMounted(async () => {
  await liveStore.loadActiveStreams()
  connectSignalR()
})

onUnmounted(() => {
  hubConnection?.stop()
})

// ── SignalR ───────────────────────────────────────────────────────────────────

function connectSignalR() {
  if (!auth.accessToken) return

  hubConnection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/live', { accessTokenFactory: () => auth.accessToken ?? '' })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  // A friend started a new stream — re-fetch to get the full DTO
  hubConnection.on('StreamStarted', () => {
    liveStore.loadActiveStreams()
  })

  // A stream has ended — remove it from the list
  hubConnection.on('StreamEnded', (streamId: string) => {
    liveStore.removeStream(streamId)
  })

  hubConnection.start().catch(console.error)
}

// ── Go Live ───────────────────────────────────────────────────────────────────

function onStreamStarted(response: StartStreamResponse) {
  showGoLiveModal.value = false
  // Navigate to the stream viewer page so the host can monitor their stream
  router.push(`/live/${response.id}`)
}
</script>

<template>
  <div class="max-w-5xl mx-auto px-4 py-6">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-2xl font-bold text-slate-100 flex items-center gap-2">
          <span class="inline-flex items-center justify-center w-8 h-8 rounded-full bg-red-100">
            <span class="w-3 h-3 rounded-full bg-red-500 animate-pulse" />
          </span>
          Live
        </h1>
        <p class="text-sm text-gray-500 mt-0.5">Watch friends stream live</p>
      </div>
      <button
        class="flex items-center gap-2 px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-sm font-semibold rounded-xl transition shadow-sm"
        @click="showGoLiveModal = true"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M15 10l4.553-2.069A1 1 0 0121 8.876V15.124a1 1 0 01-1.447.894L15 14M3 8a2 2 0 012-2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V8z"/>
        </svg>
        Go Live
      </button>
    </div>

    <!-- Loading skeleton -->
    <div v-if="liveStore.loading" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <div
        v-for="i in 3"
        :key="i"
        class="bg-slate-800 rounded-2xl border border-slate-700 overflow-hidden animate-pulse"
      >
        <div class="aspect-video bg-slate-700" />
        <div class="p-3 space-y-2">
          <div class="h-4 bg-slate-700 rounded w-3/4" />
          <div class="h-3 bg-slate-700 rounded w-1/2" />
        </div>
      </div>
    </div>

    <!-- Error -->
    <div
      v-else-if="liveStore.error"
      class="bg-red-950 border border-red-800 rounded-xl p-4 text-red-400 text-sm"
    >
      {{ liveStore.error }}
    </div>

    <!-- Empty state -->
    <div
      v-else-if="liveStore.streams.length === 0"
      class="flex flex-col items-center justify-center py-24 text-center"
    >
      <div class="w-20 h-20 rounded-full bg-slate-700 flex items-center justify-center mb-4">
        <svg class="w-10 h-10 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
                d="M15 10l4.553-2.069A1 1 0 0121 8.876V15.124a1 1 0 01-1.447.894L15 14M3 8a2 2 0 012-2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V8z"/>
        </svg>
      </div>
      <h3 class="text-lg font-semibold text-slate-300 mb-1">No live streams right now</h3>
      <p class="text-sm text-slate-500 mb-6 max-w-xs">
        No one is live at the moment. Be the first to start streaming!
      </p>
      <button
        class="flex items-center gap-2 px-5 py-2.5 bg-red-600 hover:bg-red-700 text-white text-sm font-semibold rounded-xl transition"
        @click="showGoLiveModal = true"
      >
        <span class="w-2 h-2 rounded-full bg-slate-800" />
        Start your first stream
      </button>
    </div>

    <!-- Stream grid -->
    <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <StreamCard
        v-for="stream in liveStore.streams"
        :key="stream.id"
        :stream="stream"
      />
    </div>
  </div>

  <!-- Go Live modal -->
  <GoLiveModal
    v-if="showGoLiveModal"
    @close="showGoLiveModal = false"
    @started="onStreamStarted"
  />
</template>
