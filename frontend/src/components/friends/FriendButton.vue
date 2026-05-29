<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { friendService, type FriendStatus } from '@/services/friendService'

const props = defineProps<{ userId: string }>()
const emit = defineEmits<{
  statusChanged: [status: FriendStatus]
}>()

const status = ref<FriendStatus | null>(null)
const loading = ref(false)
const menuOpen = ref(false)

onMounted(loadStatus)

async function loadStatus() {
  try {
    status.value = await friendService.getStatus(props.userId)
  } catch {
    status.value = 'None'
  }
}

async function doAction(action: () => Promise<unknown>, next: FriendStatus) {
  loading.value = true
  menuOpen.value = false
  try {
    await action()
    status.value = next
    emit('statusChanged', next)
  } catch {
    // no-op, keep existing status
  } finally {
    loading.value = false
  }
}

function sendRequest() {
  doAction(() => friendService.sendRequest(props.userId), 'RequestSent')
}
function cancelRequest() {
  doAction(() => friendService.cancelRequest(props.userId), 'None')
}
function acceptRequest() {
  doAction(() => friendService.acceptRequest(props.userId), 'Accepted')
}
function declineRequest() {
  doAction(() => friendService.declineRequest(props.userId), 'None')
}
function removeFriend() {
  doAction(() => friendService.removeFriend(props.userId), 'None')
}
function blockUser() {
  doAction(() => friendService.blockUser(props.userId), 'BlockedByMe')
}
function unblockUser() {
  doAction(() => friendService.unblockUser(props.userId), 'None')
}
</script>

<template>
  <div class="relative">
    <!-- Loading skeleton -->
    <div v-if="status === null" class="h-9 w-28 rounded-lg bg-slate-700 animate-pulse" />

    <!-- No relationship yet -->
    <button
      v-else-if="status === 'None'"
      class="flex items-center gap-1.5 text-sm font-medium bg-indigo-600 hover:bg-indigo-700 text-white px-3 py-2 rounded-lg transition-colors"
      :disabled="loading"
      @click="sendRequest"
    >
      <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z"/>
      </svg>
      {{ loading ? 'Sending…' : 'Add Friend' }}
    </button>

    <!-- Request sent — can cancel -->
    <button
      v-else-if="status === 'RequestSent'"
      class="flex items-center gap-1.5 text-sm font-medium bg-slate-700 hover:bg-slate-600 text-slate-300 px-3 py-2 rounded-lg transition-colors"
      :disabled="loading"
      @click="cancelRequest"
      title="Cancel friend request"
    >
      <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
      </svg>
      {{ loading ? 'Cancelling…' : 'Pending' }}
    </button>

    <!-- Request received — accept / decline -->
    <div v-else-if="status === 'RequestReceived'" class="flex items-center gap-2">
      <button
        class="flex items-center gap-1.5 text-sm font-medium bg-indigo-600 hover:bg-indigo-700 text-white px-3 py-2 rounded-lg transition-colors"
        :disabled="loading"
        @click="acceptRequest"
      >
        ✓ Accept
      </button>
      <button
        class="flex items-center gap-1.5 text-sm font-medium bg-slate-700 hover:bg-slate-700 text-slate-300 px-3 py-2 rounded-lg transition-colors"
        :disabled="loading"
        @click="declineRequest"
      >
        ✕ Decline
      </button>
    </div>

    <!-- Already friends — dropdown for Unfriend / Block -->
    <div v-else-if="status === 'Accepted'" class="relative">
      <button
        class="flex items-center gap-1.5 text-sm font-medium bg-slate-700 hover:bg-slate-700 text-slate-300 px-3 py-2 rounded-lg transition-colors"
        @click.stop="menuOpen = !menuOpen"
      >
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z"/>
        </svg>
        Friends ▾
      </button>
      <!-- Click-catcher -->
      <div v-if="menuOpen" class="fixed inset-0 z-10" @click="menuOpen = false" />
      <div
        v-if="menuOpen"
        class="absolute left-0 top-full mt-1 z-20 bg-slate-800 border border-slate-700 rounded-xl shadow-lg w-44 py-1"
      >
        <button
          class="w-full text-left px-4 py-2.5 text-sm text-slate-300 hover:bg-slate-700 transition"
          @click="removeFriend"
        >
          Unfriend
        </button>
        <button
          class="w-full text-left px-4 py-2.5 text-sm text-red-400 hover:bg-red-950 transition"
          @click="blockUser"
        >
          Block
        </button>
      </div>
    </div>

    <!-- Blocked by me -->
    <button
      v-else-if="status === 'BlockedByMe'"
      class="flex items-center gap-1.5 text-sm font-medium bg-red-100 hover:bg-red-200 text-red-400 px-3 py-2 rounded-lg transition-colors"
      :disabled="loading"
      @click="unblockUser"
    >
      🚫 Blocked (Unblock?)
    </button>

    <!-- Blocked by them — show nothing actionable -->
    <div v-else-if="status === 'BlockedByThem'" />
  </div>
</template>
