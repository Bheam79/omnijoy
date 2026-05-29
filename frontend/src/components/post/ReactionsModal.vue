<script setup lang="ts">
/**
 * ReactionsModal – shows the aggregated reaction breakdown for a post.
 * Since the API returns counts (not per-user), we display emoji + count per type.
 */
import { REACTION_EMOJIS, REACTION_LABELS, type ReactionCountDto } from '@/services/reactionService'

defineProps<{
  counts: ReactionCountDto[]
  totalCount: number
}>()

const emit = defineEmits<{ close: [] }>()
</script>

<template>
  <!-- Backdrop -->
  <Teleport to="body">
    <div
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
      data-testid="reactions-modal"
      @click.self="emit('close')"
    >
      <!-- Panel -->
      <div class="bg-slate-800 rounded-2xl shadow-xl w-80 max-h-[70vh] flex flex-col">
        <!-- Header -->
        <div class="flex items-center justify-between px-5 pt-4 pb-3 border-b border-slate-700">
          <h2 class="font-semibold text-slate-100">Reactions</h2>
          <button
            class="text-slate-500 hover:text-slate-400 rounded-full p-1 hover:bg-slate-700 transition"
            aria-label="Close"
            data-testid="reactions-modal-close"
            @click="emit('close')"
          >
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <!-- Content -->
        <div class="overflow-y-auto p-4 space-y-2">
          <!-- Empty state -->
          <p v-if="totalCount === 0" class="text-center text-slate-500 text-sm py-6">
            No reactions yet. Be the first!
          </p>

          <!-- Total -->
          <div v-else class="flex items-center justify-between mb-3">
            <span class="text-sm text-gray-500">Total reactions</span>
            <span class="font-semibold text-slate-200">{{ totalCount }}</span>
          </div>

          <!-- Per-type breakdown -->
          <div
            v-for="item in counts"
            :key="item.reactionType"
            class="flex items-center justify-between py-2.5 px-3 rounded-xl bg-slate-950"
            :data-testid="`reaction-row-${item.reactionType.toLowerCase()}`"
          >
            <div class="flex items-center gap-2.5">
              <span class="text-2xl leading-none">{{ REACTION_EMOJIS[item.reactionType] }}</span>
              <span class="text-sm font-medium text-slate-300">{{ REACTION_LABELS[item.reactionType] }}</span>
            </div>
            <span class="text-sm font-semibold text-slate-400">{{ item.count }}</span>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>
