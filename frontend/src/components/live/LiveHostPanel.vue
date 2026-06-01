<script setup lang="ts">
import { ref, onUnmounted } from 'vue'
import { useAuthStore } from '@/stores/auth'

const props = defineProps<{
  streamId: string
}>()

defineEmits<{
  error: [message: string]
}>()

const auth = useAuthStore()

// ── State ──────────────────────────────────────────────────────────────────────

type PanelStatus = 'idle' | 'requesting' | 'connecting' | 'live' | 'error' | 'stopped'
const status       = ref<PanelStatus>('idle')
const errorMessage = ref('')
const localVideoEl = ref<HTMLVideoElement | null>(null)

// ── Media / streaming handles ─────────────────────────────────────────────────

let mediaStream:   MediaStream   | null = null
let mediaRecorder: MediaRecorder | null = null
let ws:            WebSocket     | null = null

// ── Actions ───────────────────────────────────────────────────────────────────

async function startStreaming() {
  status.value       = 'requesting'
  errorMessage.value = ''

  try {
    // 1. Ask browser for camera + microphone
    mediaStream = await navigator.mediaDevices.getUserMedia({
      video: { width: { ideal: 1280 }, height: { ideal: 720 }, frameRate: { ideal: 30 } },
      audio: true,
    })

    // 2. Show muted local preview (no echo)
    if (localVideoEl.value) {
      localVideoEl.value.srcObject = mediaStream
      await localVideoEl.value.play()
    }

    status.value = 'connecting'

    // 3. Open WebSocket to the backend ingest endpoint
    const token  = auth.accessToken ?? ''
    const proto  = location.protocol === 'https:' ? 'wss' : 'ws'
    const wsUrl  = `${proto}://${location.host}/api/live/${props.streamId}/ingest?access_token=${encodeURIComponent(token)}`
    ws           = new WebSocket(wsUrl)
    ws.binaryType = 'arraybuffer'

    ws.onopen = () => {
      status.value = 'live'

      // 4. Start recording — send 1-second chunks over WebSocket
      const mimeType = getBestMimeType()
      mediaRecorder  = new MediaRecorder(mediaStream!, {
        mimeType,
        videoBitsPerSecond: 2_000_000,
        audioBitsPerSecond: 128_000,
      })

      mediaRecorder.ondataavailable = (e: BlobEvent) => {
        if (e.data && e.data.size > 0 && ws?.readyState === WebSocket.OPEN) {
          ws.send(e.data)
        }
      }

      mediaRecorder.onerror = () => {
        stopStreaming()
        setError('Recording stopped unexpectedly. Please try again.')
      }

      mediaRecorder.start(1000) // 1-second chunks
    }

    ws.onclose = (_e: CloseEvent) => {
      if (status.value === 'live' || status.value === 'connecting') {
        stopStreaming()
      }
    }

    ws.onerror = () => {
      stopStreaming()
      setError('Connection to streaming server lost. Please try again.')
    }

  } catch (e: unknown) {
    stopStreaming()
    if (e instanceof DOMException) {
      if (e.name === 'NotAllowedError' || e.name === 'PermissionDeniedError') {
        setError('Camera/microphone permission denied. Please allow access in your browser and try again.')
      } else if (e.name === 'NotFoundError' || e.name === 'DevicesNotFoundError') {
        setError('No camera or microphone found. Please connect a device and try again.')
      } else if (e.name === 'NotReadableError') {
        setError('Camera or microphone is already in use by another application.')
      } else {
        setError(`Could not access camera/microphone: ${e.message}`)
      }
    } else {
      setError('Could not start streaming. Please try again.')
    }
  }
}

function stopStreaming() {
  mediaRecorder?.stop()
  mediaRecorder = null

  ws?.close()
  ws = null

  mediaStream?.getTracks().forEach(t => t.stop())
  mediaStream = null

  if (localVideoEl.value) {
    localVideoEl.value.srcObject = null
  }

  if (status.value !== 'error') {
    status.value = 'stopped'
  }
}

function setError(msg: string) {
  errorMessage.value = msg
  status.value       = 'error'
}

/** Returns the best webm mime type the browser supports. */
function getBestMimeType(): string {
  const candidates = [
    'video/webm;codecs=avc1,opus',  // H.264 — most efficient for transcoding
    'video/webm;codecs=vp8,opus',   // VP8 — widely supported fallback
    'video/webm;codecs=vp9,opus',   // VP9 — better quality, slower
    'video/webm',                   // browser default
  ]
  for (const type of candidates) {
    if (MediaRecorder.isTypeSupported(type)) return type
  }
  return 'video/webm'
}

// ── Cleanup on unmount ────────────────────────────────────────────────────────

onUnmounted(() => {
  stopStreaming()
})
</script>

<template>
  <div class="flex flex-col gap-4">
    <!-- Local camera preview -->
    <div class="relative bg-black rounded-2xl overflow-hidden aspect-video">
      <video
        ref="localVideoEl"
        class="w-full h-full object-contain transition-opacity duration-300"
        :class="status === 'live' || status === 'connecting' ? 'opacity-100' : 'opacity-0'"
        muted
        playsinline
      />

      <!-- Idle / requesting overlay -->
      <div
        v-if="status === 'idle' || status === 'requesting' || status === 'stopped'"
        class="absolute inset-0 flex flex-col items-center justify-center text-white"
      >
        <svg class="w-16 h-16 text-slate-500 mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1"
                d="M15 10l4.553-2.069A1 1 0 0121 8.876V15.124a1 1 0 01-1.447.894L15 14
                   M3 8a2 2 0 012-2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V8z"/>
        </svg>
        <p v-if="status === 'requesting'" class="text-sm text-slate-300 animate-pulse">
          Requesting camera &amp; microphone access…
        </p>
        <p v-else-if="status === 'stopped'" class="text-sm text-slate-500">
          Streaming stopped
        </p>
        <p v-else class="text-sm text-slate-500">
          Your camera preview will appear here
        </p>
      </div>

      <!-- Connecting overlay -->
      <div
        v-if="status === 'connecting'"
        class="absolute inset-0 flex flex-col items-center justify-center bg-black/60"
      >
        <div class="w-8 h-8 border-4 border-red-500 border-t-transparent rounded-full animate-spin mb-2" />
        <p class="text-sm text-white">Connecting to streaming server…</p>
      </div>

      <!-- Live badge -->
      <div v-if="status === 'live'" class="absolute top-3 left-3">
        <span class="inline-flex items-center gap-1.5 px-2.5 py-1 bg-red-600 text-white text-xs font-bold rounded-full shadow">
          <span class="w-1.5 h-1.5 rounded-full bg-white animate-pulse" />
          BROADCASTING
        </span>
      </div>

      <!-- Error overlay -->
      <div
        v-if="status === 'error'"
        class="absolute inset-0 flex flex-col items-center justify-center bg-black/80 p-6 text-center"
      >
        <svg class="w-10 h-10 text-red-400 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
                d="M12 9v2m0 4h.01
                   M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"/>
        </svg>
        <p class="text-sm text-red-300">{{ errorMessage }}</p>
      </div>
    </div>

    <!-- Controls row -->
    <div class="flex items-center gap-3 flex-wrap">
      <!-- Start button (idle / stopped / error) -->
      <button
        v-if="status === 'idle' || status === 'stopped' || status === 'error'"
        class="flex items-center gap-2 px-5 py-2.5 bg-red-600 hover:bg-red-700 text-white text-sm font-semibold rounded-xl transition"
        @click="startStreaming"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M15 10l4.553-2.069A1 1 0 0121 8.876V15.124a1 1 0 01-1.447.894L15 14
                   M3 8a2 2 0 012-2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V8z"/>
        </svg>
        {{ status === 'error' ? 'Retry camera' : 'Start camera' }}
      </button>

      <!-- Stop button (live) -->
      <button
        v-if="status === 'live'"
        class="flex items-center gap-2 px-5 py-2.5 bg-slate-700 hover:bg-slate-600 text-white text-sm font-semibold rounded-xl transition"
        @click="stopStreaming"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z
                   M9 10a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1h-4a1 1 0 01-1-1v-4z"/>
        </svg>
        Stop camera
      </button>

      <!-- Status labels -->
      <p v-if="status === 'live'" class="text-xs text-green-400 font-medium flex items-center gap-1.5">
        <span class="w-1.5 h-1.5 rounded-full bg-green-400 animate-pulse" />
        Broadcasting live — viewers will see your stream within a few seconds
      </p>
      <p v-else-if="status === 'requesting' || status === 'connecting'" class="text-xs text-slate-400 flex items-center gap-1.5">
        <span class="w-3 h-3 border-2 border-slate-400 border-t-transparent rounded-full animate-spin" />
        {{ status === 'requesting' ? 'Allow camera &amp; microphone access in your browser…' : 'Connecting…' }}
      </p>
    </div>

    <!-- Hint text -->
    <p class="text-xs text-slate-500">
      This panel shows your local camera. Your viewers watch via the HLS player below.
      There is a short delay (a few seconds) between what you see here and what viewers receive.
    </p>
  </div>
</template>
