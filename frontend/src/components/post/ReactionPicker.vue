<script setup lang="ts">
/**
 * ReactionPicker
 *
 * Renders a Like button that:
 *  - On desktop: hover for 500 ms → shows floating emoji picker
 *  - On mobile:  long-press for 500 ms → shows picker
 *
 * Clicking the button while no reaction is active triggers "Like".
 * Clicking the button while user already has a reaction removes it.
 * Clicking an emoji in the picker sets that reaction.
 *
 * Props:
 *   currentReaction  – the user's active ReactionType, or null
 *   disabled         – disables all interaction (e.g. not logged in)
 *
 * Events:
 *   react(type: ReactionType)  – user picked a reaction
 *   remove()                   – user removed their reaction
 */
import { ref, computed, onBeforeUnmount } from 'vue'
import {
  ALL_REACTION_TYPES,
  REACTION_EMOJIS,
  REACTION_LABELS,
  type ReactionType,
} from '@/services/reactionService'

const props = withDefaults(defineProps<{
  currentReaction?: ReactionType | null
  disabled?: boolean
  compact?: boolean
}>(), {
  currentReaction: null,
  disabled: false,
  compact: false,
})

const emit = defineEmits<{
  react: [type: ReactionType]
  remove: []
}>()

// ── Picker visibility ──────────────────────────────────────────────────────────

const pickerVisible = ref(false)
let openTimer: ReturnType<typeof setTimeout> | null = null
let closeTimer: ReturnType<typeof setTimeout> | null = null

function scheduleOpen() {
  if (props.disabled) return
  clearTimeout(closeTimer!)
  openTimer = setTimeout(() => {
    pickerVisible.value = true
  }, 500)
}

function scheduleClosed() {
  clearTimeout(openTimer!)
  closeTimer = setTimeout(() => {
    pickerVisible.value = false
  }, 300)
}

function cancelTimers() {
  clearTimeout(openTimer!)
  clearTimeout(closeTimer!)
}

// ── Long-press support for mobile ─────────────────────────────────────────────

let longPressTimer: ReturnType<typeof setTimeout> | null = null

function onTouchStart() {
  if (props.disabled) return
  longPressTimer = setTimeout(() => {
    pickerVisible.value = true
  }, 500)
}

function onTouchEnd() {
  clearTimeout(longPressTimer!)
}

// ── Click handler on the main button ─────────────────────────────────────────

function onButtonClick() {
  if (props.disabled) return
  // If picker is already open, just close it without acting
  if (pickerVisible.value) {
    pickerVisible.value = false
    return
  }
  if (props.currentReaction) {
    emit('remove')
  } else {
    emit('react', 'Like')
  }
}

// ── Emoji picker item interaction ─────────────────────────────────────────────

function onPickReaction(type: ReactionType) {
  pickerVisible.value = false
  cancelTimers()
  if (props.currentReaction === type) {
    emit('remove')
  } else {
    emit('react', type)
  }
}

/**
 * Tracks whether the last selection was already committed via mouseup.
 * Used to suppress the subsequent click event that follows a normal
 * mouse click (mousedown → mouseup → click) so we don't double-emit.
 */
let pickedViaMouseUp = false

/**
 * mouseup handler — fires when the user releases the mouse button over a
 * reaction emoji, including the hold-and-release flow where mousedown started
 * on the Like button (which would produce no click event on the emoji).
 * Also handles a normal mouse click — the follow-up click event is suppressed
 * by the pickedViaMouseUp flag.
 */
function onPickReactionMouseUp(type: ReactionType) {
  pickedViaMouseUp = true
  onPickReaction(type)
}

/**
 * click handler — handles keyboard-initiated activation (Enter / Space).
 * Mouse-driven clicks are already handled by onPickReactionMouseUp, so we
 * skip them here (pickedViaMouseUp flag) to avoid double-emitting.
 */
function onPickReactionClick(type: ReactionType) {
  if (pickedViaMouseUp) {
    pickedViaMouseUp = false
    return
  }
  onPickReaction(type)
}

// ── Active reaction display ───────────────────────────────────────────────────

const reactionColorClass = computed(() => {
  if (!props.currentReaction) return 'text-gray-500'
  const map: Record<ReactionType, string> = {
    Like:  'text-blue-400',
    Love:  'text-red-500',
    Haha:  'text-yellow-500',
    Wow:   'text-yellow-500',
    Sad:   'text-yellow-500',
    Angry: 'text-orange-600',
  }
  return map[props.currentReaction]
})

const buttonLabel = computed(() =>
  props.currentReaction
    ? REACTION_LABELS[props.currentReaction]
    : 'Like',
)

onBeforeUnmount(cancelTimers)
</script>

<template>
  <div class="relative" data-testid="reaction-picker">
    <!-- Main like/reaction button -->
    <button
      class="flex items-center font-medium rounded-lg transition flex-1 justify-center"
      :class="[
        compact ? 'gap-1 px-1 py-0.5 text-xs' : 'gap-1.5 px-3 py-1.5 text-sm',
        currentReaction
          ? `${reactionColorClass} hover:bg-blue-900/40`
          : 'text-gray-500 hover:text-blue-400 hover:bg-blue-900/40',
        disabled ? 'opacity-50 cursor-default' : 'cursor-pointer',
      ]"
      :disabled="disabled"
      :aria-label="buttonLabel"
      data-testid="reaction-button"
      @mouseenter="scheduleOpen"
      @mouseleave="scheduleClosed"
      @click="onButtonClick"
      @touchstart.passive="onTouchStart"
      @touchend="onTouchEnd"
    >
      <!-- Active reaction emoji OR default thumb-up icon -->
      <span v-if="currentReaction" class="text-base leading-none">
        {{ REACTION_EMOJIS[currentReaction] }}
      </span>
      <svg v-else :class="compact ? 'w-3.5 h-3.5' : 'w-5 h-5'" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M14 10h4.764a2 2 0 011.789 2.894l-3.5 7A2 2 0 0115.263 21h-4.017c-.163 0-.326-.02-.485-.06L7 20m7-10V5a2 2 0 00-2-2h-.095c-.5 0-.905.405-.905.905 0 .714-.211 1.412-.608 2.006L7 11v9m7-10h-2M7 20H5a2 2 0 01-2-2v-6a2 2 0 012-2h2.5"/>
      </svg>
      {{ buttonLabel }}
    </button>

    <!-- Floating emoji picker (shown on hover / long-press) -->
    <Transition name="picker">
      <div
        v-if="pickerVisible"
        class="absolute bottom-full left-0 mb-2 z-20 flex items-center gap-1 bg-slate-800 rounded-full shadow-lg border border-slate-700 px-2 py-1.5"
        data-testid="reaction-picker-popup"
        @mouseenter="cancelTimers"
        @mouseleave="scheduleClosed"
      >
        <button
          v-for="type in ALL_REACTION_TYPES"
          :key="type"
          class="text-2xl leading-none p-1.5 rounded-full transition hover:scale-125 hover:bg-slate-700 focus:outline-none"
          :class="{ 'scale-125': currentReaction === type }"
          :aria-label="REACTION_LABELS[type]"
          :data-testid="`pick-${type.toLowerCase()}`"
          @mouseup="onPickReactionMouseUp(type)"
          @click="onPickReactionClick(type)"
        >
          {{ REACTION_EMOJIS[type] }}
        </button>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.picker-enter-active,
.picker-leave-active {
  transition: opacity 150ms ease, transform 150ms ease;
}
.picker-enter-from,
.picker-leave-to {
  opacity: 0;
  transform: scale(0.85) translateY(4px);
}
</style>
