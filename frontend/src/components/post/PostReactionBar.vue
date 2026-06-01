<script setup lang="ts">
/**
 * PostReactionBar
 *
 * Combines the ReactionPicker button with an aggregated count display.
 * Reads / writes via the reactionsStore (which is also updated by SignalR).
 *
 * Props:
 *   postId    – the post's GUID
 *   loggedIn  – whether the current user is authenticated
 */
import { computed, onMounted, ref } from 'vue'
import { useReactionsStore } from '@/stores/reactions'
import { REACTION_EMOJIS, reactionService, type ReactionType, type ReactionWhoDto } from '@/services/reactionService'
import ReactionPicker from './ReactionPicker.vue'
import ReactionsModal from './ReactionsModal.vue'

const props = defineProps<{
  postId: string
  loggedIn?: boolean
}>()

const reactionsStore = useReactionsStore()
const showModal = ref(false)

// ── Reactive derived data ─────────────────────────────────────────────────────

const data = computed(() => reactionsStore.byPost[props.postId])

const currentReaction = computed<ReactionType | null>(
  () => data.value?.currentUserReaction ?? null,
)

const totalCount = computed(() => data.value?.totalCount ?? 0)

const counts = computed(() => data.value?.counts ?? [])

/** Top-3 reaction types by count, for the emoji summary strip. */
const topEmojis = computed(() =>
  [...(data.value?.counts ?? [])]
    .sort((a, b) => b.count - a.count)
    .slice(0, 3)
    .map(c => REACTION_EMOJIS[c.reactionType]),
)

// ── Who reacted tooltip ───────────────────────────────────────────────────────

const whoData = ref<ReactionWhoDto | null>(null)
const whoLoading = ref(false)
const showWhoTooltip = ref(false)
let hoverTimeout: ReturnType<typeof setTimeout> | null = null

async function onCountMouseEnter() {
  // Delay slightly so fast mouse-overs don't trigger a fetch
  hoverTimeout = setTimeout(async () => {
    showWhoTooltip.value = true
    if (!whoData.value && !whoLoading.value) {
      whoLoading.value = true
      try {
        whoData.value = await reactionService.getReactionWho(props.postId)
      } catch {
        // silently ignore — tooltip just won't show names
      } finally {
        whoLoading.value = false
      }
    }
  }, 200)
}

function onCountMouseLeave() {
  if (hoverTimeout) {
    clearTimeout(hoverTimeout)
    hoverTimeout = null
  }
  showWhoTooltip.value = false
}

// ── Lifecycle ─────────────────────────────────────────────────────────────────

onMounted(() => {
  // Only fetch if we don't already have data for this post
  if (!reactionsStore.byPost[props.postId]) {
    reactionsStore.fetchReactions(props.postId)
  }
})

// ── Actions ───────────────────────────────────────────────────────────────────

function onReact(type: ReactionType) {
  reactionsStore.addOrUpdate(props.postId, type)
}

function onRemove() {
  reactionsStore.remove(props.postId)
}
</script>

<template>
  <div data-testid="post-reaction-bar">
    <!-- Reaction count strip (only when there are reactions) -->
    <div
      v-if="totalCount > 0"
      class="px-4 py-1 flex items-center gap-1 text-sm text-gray-500 border-b border-slate-700"
    >
      <!-- Wrapper with hover tooltip -->
      <div
        class="relative"
        @mouseenter="onCountMouseEnter"
        @mouseleave="onCountMouseLeave"
      >
        <button
          class="flex items-center gap-1 hover:underline focus:outline-none"
          aria-label="See reactions"
          data-testid="reaction-count-button"
          @click="showModal = true"
        >
          <!-- Top-3 emoji strip -->
          <span
            v-for="(emoji, i) in topEmojis"
            :key="i"
            class="text-base leading-none"
            aria-hidden="true"
          >{{ emoji }}</span>
          <!-- Total count -->
          <span class="ml-1 text-xs text-gray-500">{{ totalCount }}</span>
        </button>

        <!-- Who-reacted tooltip -->
        <div
          v-if="showWhoTooltip && totalCount > 0"
          class="absolute bottom-full left-0 mb-2 z-50 bg-gray-900 text-white text-xs rounded-lg shadow-xl px-3 py-2 min-w-[140px] max-w-[220px] pointer-events-none"
          data-testid="reaction-who-tooltip"
        >
          <!-- Loading state -->
          <span v-if="whoLoading" class="text-gray-400">Loading…</span>

          <!-- Populated list -->
          <template v-else-if="whoData && whoData.people.length > 0">
            <div
              v-for="person in whoData.people"
              :key="person.id"
              class="flex items-center gap-1.5 py-0.5"
            >
              <span class="text-sm leading-none">{{ REACTION_EMOJIS[person.reactionType] }}</span>
              <span class="truncate font-medium">{{ person.displayName }}</span>
            </div>
            <div v-if="whoData.remaining > 0" class="mt-1 text-gray-400">
              +{{ whoData.remaining }} more…
            </div>
          </template>

          <!-- Empty / error fallback -->
          <span v-else class="text-gray-400">No reactions yet</span>
        </div>
      </div>
    </div>

    <!-- Picker + count row -->
    <div class="flex items-center gap-1 px-4 py-2 border-t border-slate-700">
      <ReactionPicker
        :current-reaction="currentReaction"
        :disabled="!loggedIn"
        class="flex-1"
        @react="onReact"
        @remove="onRemove"
      />
    </div>

    <!-- Reactions breakdown modal -->
    <ReactionsModal
      v-if="showModal"
      :counts="counts"
      :total-count="totalCount"
      @close="showModal = false"
    />
  </div>
</template>
