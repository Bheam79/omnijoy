<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { PostDto } from '@/services/postService'
import { shareService, type CreateSharePayload } from '@/services/shareService'
import { useFriendsStore } from '@/stores/friends'
import { useFeedStore } from '@/stores/feed'

const props = defineProps<{
  post: PostDto
  modelValue: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  shared: []
}>()

// ── State ─────────────────────────────────────────────────────────────────────

const targetType = ref<'OwnWall' | 'FriendWall' | 'CompanyPage'>('OwnWall')
const targetId = ref<string>('')
const message = ref('')
const submitting = ref(false)
const error = ref<string | null>(null)

const friendsStore = useFriendsStore()
const feedStore = useFeedStore()

// ── Load friends when modal opens ────────────────────────────────────────────

watch(() => props.modelValue, async (open) => {
  if (open) {
    error.value = null
    targetType.value = 'OwnWall'
    targetId.value = ''
    message.value = ''
    if (friendsStore.friends.length === 0) {
      await friendsStore.loadFriends(true)
    }
  }
})

// ── Validation ────────────────────────────────────────────────────────────────

const isValid = computed(() => {
  if (targetType.value === 'FriendWall') return !!targetId.value
  if (targetType.value === 'CompanyPage') return !!targetId.value
  return true
})

// ── Submit ────────────────────────────────────────────────────────────────────

async function submit() {
  if (!isValid.value || submitting.value) return

  submitting.value = true
  error.value = null

  try {
    const payload: CreateSharePayload = {
      targetType: targetType.value,
      targetId:   targetType.value !== 'OwnWall' ? targetId.value : undefined,
      message:    message.value.trim() || undefined,
    }

    const sharedPost = await shareService.sharePost(props.post.id, payload)
    feedStore.prependSharedPost(sharedPost)

    emit('shared')
    close()
  } catch (e: unknown) {
    error.value = extractError(e)
  } finally {
    submitting.value = false
  }
}

function close() {
  emit('update:modelValue', false)
}

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const axiosError = e as { response?: { data?: { error?: string } }; message?: string }
    return axiosError.response?.data?.error ?? axiosError.message ?? 'An unexpected error occurred.'
  }
  return 'An unexpected error occurred.'
}
</script>

<template>
  <Teleport to="body">
    <div
      v-if="modelValue"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      @click.self="close"
    >
      <div class="bg-white rounded-2xl shadow-xl w-full max-w-md" role="dialog" aria-modal="true" aria-label="Share post">
        <!-- Header -->
        <div class="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h2 class="text-lg font-semibold text-gray-900">Share post</h2>
          <button
            class="text-gray-400 hover:text-gray-600 p-1 rounded-full hover:bg-gray-100 transition"
            aria-label="Close"
            @click="close"
          >
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <!-- Body -->
        <div class="px-5 py-4 space-y-4">
          <!-- Target selector -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Share to</label>
            <select
              v-model="targetType"
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              aria-label="Share destination"
            >
              <option value="OwnWall">My Wall</option>
              <option value="FriendWall">A Friend's Wall</option>
              <option value="CompanyPage">A Company Page</option>
            </select>
          </div>

          <!-- Friend picker -->
          <div v-if="targetType === 'FriendWall'">
            <label class="block text-sm font-medium text-gray-700 mb-1">Select friend</label>
            <select
              v-model="targetId"
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              aria-label="Select friend"
            >
              <option value="" disabled>Choose a friend…</option>
              <option
                v-for="friend in friendsStore.friends"
                :key="friend.user.id"
                :value="friend.user.id"
              >
                {{ friend.user.displayName }}
              </option>
            </select>
            <p v-if="friendsStore.loadingFriends" class="text-xs text-gray-400 mt-1">Loading friends…</p>
            <p v-else-if="friendsStore.friends.length === 0" class="text-xs text-gray-400 mt-1">
              You have no friends yet.
            </p>
          </div>

          <!-- Company page ID input (simplified: direct ID input) -->
          <div v-if="targetType === 'CompanyPage'">
            <label class="block text-sm font-medium text-gray-700 mb-1">Company Page ID</label>
            <input
              v-model="targetId"
              type="text"
              placeholder="Enter company page ID"
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              aria-label="Company page ID"
            />
          </div>

          <!-- Optional message -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Message <span class="font-normal text-gray-400">(optional)</span>
            </label>
            <textarea
              v-model="message"
              rows="3"
              placeholder="Say something about this post…"
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-500"
              aria-label="Share message"
            />
          </div>

          <!-- Error -->
          <p v-if="error" class="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
            {{ error }}
          </p>
        </div>

        <!-- Footer -->
        <div class="px-5 pb-4 flex justify-end gap-2">
          <button
            class="px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 rounded-lg transition"
            @click="close"
          >
            Cancel
          </button>
          <button
            class="px-5 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition disabled:opacity-50 disabled:cursor-not-allowed"
            :disabled="!isValid || submitting"
            @click="submit"
          >
            <span v-if="submitting">Sharing…</span>
            <span v-else>Share</span>
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
