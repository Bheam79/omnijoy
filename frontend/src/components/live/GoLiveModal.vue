<script setup lang="ts">
import { ref } from 'vue'
import { useLiveStore } from '@/stores/live'
import type { StartStreamResponse } from '@/services/liveService'

const emit = defineEmits<{
  close: []
  started: [stream: StartStreamResponse]
}>()

const liveStore = useLiveStore()

const title   = ref('')
const privacy = ref<'Everyone' | 'Friends'>('Friends')
const loading = ref(false)
const error   = ref<string | null>(null)
const started = ref<StartStreamResponse | null>(null)

async function submit() {
  if (!title.value.trim()) {
    error.value = 'Please enter a stream title.'
    return
  }

  loading.value = true
  error.value = null

  try {
    const response = await liveStore.startStream({
      title: title.value.trim(),
      privacy: privacy.value,
    })
    started.value = response
    emit('started', response)
  } catch (e: unknown) {
    error.value = extractError(e)
  } finally {
    loading.value = false
  }
}

function copyToClipboard(text: string) {
  navigator.clipboard.writeText(text).catch(() => {})
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
  <!-- Backdrop -->
  <div
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4"
    @click.self="emit('close')"
  >
    <div class="w-full max-w-md bg-slate-800 rounded-2xl shadow-2xl overflow-hidden">
      <!-- Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-slate-700">
        <h2 class="text-lg font-semibold text-slate-100 flex items-center gap-2">
          <span class="inline-flex items-center justify-center w-7 h-7 rounded-full bg-red-100">
            <span class="w-2.5 h-2.5 rounded-full bg-red-500 animate-pulse" />
          </span>
          Go Live
        </h2>
        <button
          class="p-1.5 text-slate-500 hover:text-slate-400 hover:bg-slate-700 rounded-lg transition"
          @click="emit('close')"
        >
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
          </svg>
        </button>
      </div>

      <!-- Body -->
      <div class="p-6">
        <!-- After stream is created -->
        <template v-if="started">
          <div class="mb-4 p-3 bg-green-950 border border-green-800 rounded-xl text-sm text-green-400 font-medium">
            Your stream is live! Use the info below in OBS or your streaming software.
          </div>

          <div class="space-y-4">
            <!-- Ingest URL -->
            <div>
              <label class="block text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">
                RTMP Ingest URL
              </label>
              <div class="flex items-center gap-2">
                <input
                  :value="started.ingestUrl"
                  readonly
                  class="flex-1 px-3 py-2 bg-slate-950 border border-slate-700 rounded-lg text-sm font-mono text-slate-300 truncate"
                />
                <button
                  class="shrink-0 px-3 py-2 bg-slate-700 hover:bg-slate-700 text-slate-300 text-sm rounded-lg transition"
                  @click="copyToClipboard(started.ingestUrl)"
                >
                  Copy
                </button>
              </div>
            </div>

            <!-- Stream Key -->
            <div>
              <label class="block text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">
                Stream Key (keep this secret!)
              </label>
              <div class="flex items-center gap-2">
                <input
                  :value="started.streamKey"
                  readonly
                  type="password"
                  class="flex-1 px-3 py-2 bg-slate-950 border border-slate-700 rounded-lg text-sm font-mono text-slate-300"
                />
                <button
                  class="shrink-0 px-3 py-2 bg-slate-700 hover:bg-slate-700 text-slate-300 text-sm rounded-lg transition"
                  @click="copyToClipboard(started.streamKey)"
                >
                  Copy
                </button>
              </div>
              <p class="mt-1 text-xs text-slate-500">
                In OBS: Settings → Stream → Service: Custom → paste the URL and key above.
              </p>
            </div>
          </div>

          <div class="mt-6 flex justify-end gap-3">
            <button
              class="px-4 py-2 text-sm font-medium text-slate-300 bg-slate-700 hover:bg-slate-700 rounded-xl transition"
              @click="emit('close')"
            >
              Close
            </button>
          </div>
        </template>

        <!-- Setup form -->
        <template v-else>
          <div class="space-y-4">
            <!-- Title -->
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">
                Stream title <span class="text-red-500">*</span>
              </label>
              <input
                v-model="title"
                type="text"
                placeholder="What are you streaming today?"
                maxlength="100"
                class="w-full px-3 py-2 border border-slate-600 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
                @keyup.enter="submit"
              />
            </div>

            <!-- Privacy -->
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-2">Who can watch?</label>
              <div class="flex gap-3">
                <button
                  v-for="opt in [{ value: 'Friends' as const, label: 'Friends only' }, { value: 'Everyone' as const, label: 'Everyone (public)' }]"
                  :key="opt.value"
                  class="flex-1 py-2 px-3 text-sm rounded-xl border-2 font-medium transition"
                  :class="privacy === opt.value
                    ? 'border-indigo-500 bg-indigo-900/50 text-indigo-300'
                    : 'border-slate-700 text-slate-400 hover:border-slate-600'"
                  @click="privacy = opt.value"
                >
                  {{ opt.label }}
                </button>
              </div>
            </div>

            <!-- Error -->
            <p v-if="error" class="text-sm text-red-400 bg-red-950 rounded-lg px-3 py-2">
              {{ error }}
            </p>
          </div>

          <div class="mt-6 flex justify-end gap-3">
            <button
              class="px-4 py-2 text-sm font-medium text-slate-300 bg-slate-700 hover:bg-slate-700 rounded-xl transition"
              :disabled="loading"
              @click="emit('close')"
            >
              Cancel
            </button>
            <button
              class="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-red-600 hover:bg-red-700 disabled:opacity-50 rounded-xl transition"
              :disabled="loading || !title.trim()"
              @click="submit"
            >
              <span
                v-if="loading"
                class="w-4 h-4 border-2 border-white/40 border-t-white rounded-full animate-spin"
              />
              <span v-else class="w-2 h-2 rounded-full bg-slate-800" />
              {{ loading ? 'Starting…' : 'Start streaming' }}
            </button>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>
