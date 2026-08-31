<script setup lang="ts">
import { onMounted, ref } from 'vue'
import {
  REACTION_EMOJIS,
  REACTION_LABELS,
  reactionService,
  type ReactionCountDto,
  type ReactionTargetKind,
  type ReactionWhoDto,
} from '@/services/reactionService'

const props = defineProps<{
  targetKind: ReactionTargetKind
  targetId: string
  counts: ReactionCountDto[]
  totalCount: number
}>()

const emit = defineEmits<{ close: [] }>()

const who = ref<ReactionWhoDto | null>(null)
const loading = ref(false)

onMounted(async () => {
  if (props.totalCount === 0) return
  loading.value = true
  try {
    who.value = await reactionService.getReactionWho(props.targetId, props.targetKind)
  } catch {
    who.value = null
  } finally {
    loading.value = false
  }
})
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

          <div v-if="totalCount > 0" class="border-t border-slate-700 pt-3 mt-3" data-testid="reaction-who-list">
            <p v-if="loading" class="text-center text-sm text-slate-500 py-2">Loading…</p>
            <template v-else-if="who && who.people.length > 0">
              <div
                v-for="person in who.people"
                :key="person.id"
                class="flex items-center gap-2 py-1.5"
                :data-testid="`reaction-person-${person.id}`"
              >
                <img
                  v-if="person.avatarUrl"
                  :src="person.avatarUrl"
                  :alt="person.displayName"
                  class="w-7 h-7 rounded-full object-cover"
                />
                <div v-else class="w-7 h-7 rounded-full bg-blue-500 text-white text-xs flex items-center justify-center">
                  {{ person.displayName.charAt(0).toUpperCase() }}
                </div>
                <span class="flex-1 truncate text-sm text-slate-300">{{ person.displayName }}</span>
                <span aria-hidden="true">{{ REACTION_EMOJIS[person.reactionType] }}</span>
              </div>
              <p v-if="who.remaining > 0" class="text-xs text-slate-500 mt-1">
                +{{ who.remaining }} more…
              </p>
            </template>
            <p v-else class="text-center text-sm text-slate-500 py-2">Unable to load reactors</p>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>
