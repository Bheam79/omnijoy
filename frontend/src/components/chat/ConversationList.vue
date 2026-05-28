<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useChatStore } from '@/stores/chat'
import type { Conversation } from '@/types'

const auth = useAuthStore()
const chat = useChatStore()

const searchQuery = ref('')

onMounted(() => {
  if (!chat.conversations.length) {
    chat.loadConversations()
  }
})

function otherParticipant(conv: Conversation) {
  return conv.participants.find((p) => p.id !== auth.user?.id)
}

function lastMessagePreview(conv: Conversation): string {
  const msg = conv.lastMessage
  if (!msg) return 'No messages yet'
  if (msg.isDeleted) return 'Message deleted'
  if (msg.messageType === 'Image') return '📷 Image'
  if (msg.messageType === 'Video') return '🎥 Video'
  if (msg.messageType === 'File')  return `📎 ${msg.media?.fileName ?? 'File'}`
  return msg.content?.substring(0, 60) ?? ''
}

function formatTime(iso?: string): string {
  if (!iso) return ''
  const d = new Date(iso)
  const now = new Date()
  const isToday =
    d.getDate() === now.getDate() &&
    d.getMonth() === now.getMonth() &&
    d.getFullYear() === now.getFullYear()

  return isToday
    ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    : d.toLocaleDateString([], { month: 'short', day: 'numeric' })
}

const filtered = computed(() => {
  if (!searchQuery.value.trim()) return chat.conversations
  const q = searchQuery.value.toLowerCase()
  return chat.conversations.filter((c) => {
    const other = otherParticipant(c)
    return other?.displayName.toLowerCase().includes(q) ?? false
  })
})

import { computed } from 'vue'
</script>

<template>
  <div class="flex flex-col w-80 max-h-[480px] bg-white rounded-xl shadow-2xl border border-gray-200 overflow-hidden">
    <!-- Header -->
    <div class="flex items-center justify-between px-4 py-3 border-b border-gray-100">
      <span class="font-semibold text-gray-900">Messenger</span>
      <button
        class="p-1 rounded-full text-gray-400 hover:bg-gray-100 transition-colors"
        aria-label="Close"
        @click="chat.toggleList()"
      >
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
        </svg>
      </button>
    </div>

    <!-- Search -->
    <div class="px-3 py-2 border-b border-gray-100">
      <div class="relative">
        <svg class="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-gray-400 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
        </svg>
        <input
          v-model="searchQuery"
          type="search"
          placeholder="Search conversations…"
          class="w-full bg-gray-100 rounded-full pl-8 pr-3 py-1.5 text-xs focus:outline-none focus:ring-1 focus:ring-indigo-400"
        />
      </div>
    </div>

    <!-- Loading -->
    <div v-if="chat.loading" class="flex-1 flex items-center justify-center py-8 text-gray-400 text-sm">
      Loading…
    </div>

    <!-- Empty -->
    <div
      v-else-if="!filtered.length"
      class="flex-1 flex flex-col items-center justify-center py-8 text-gray-400 text-sm gap-2"
    >
      <svg class="h-8 w-8 opacity-50" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
          d="M8 12h.01M12 12h.01M16 12h.01M21 8a9 9 0 11-18 0 9 9 0 0118 0z"/>
      </svg>
      <span>No conversations yet</span>
    </div>

    <!-- Conversation list -->
    <ul v-else class="flex-1 overflow-y-auto divide-y divide-gray-50">
      <li
        v-for="conv in filtered"
        :key="conv.id"
        class="flex items-center gap-3 px-3 py-2.5 hover:bg-gray-50 cursor-pointer transition-colors"
        @click="chat.openWindow(conv.id)"
      >
        <!-- Avatar -->
        <div class="shrink-0 relative">
          <div class="h-10 w-10 rounded-full overflow-hidden bg-indigo-100 flex items-center justify-center text-indigo-600 font-bold text-sm">
            <img
              v-if="otherParticipant(conv)?.avatarUrl"
              :src="otherParticipant(conv)?.avatarUrl"
              :alt="otherParticipant(conv)?.displayName"
              class="h-full w-full object-cover"
            />
            <span v-else>
              {{ otherParticipant(conv)?.displayName?.[0]?.toUpperCase() ?? '?' }}
            </span>
          </div>
          <!-- Unread dot -->
          <span
            v-if="conv.unreadCount > 0"
            class="absolute -top-0.5 -right-0.5 min-w-[1rem] h-4 rounded-full bg-red-500 text-white text-[9px] font-bold flex items-center justify-center px-0.5 ring-2 ring-white"
          >
            {{ conv.unreadCount > 9 ? '9+' : conv.unreadCount }}
          </span>
        </div>

        <!-- Content -->
        <div class="flex-1 min-w-0">
          <div class="flex items-baseline justify-between">
            <span class="text-sm font-medium text-gray-900 truncate">
              {{ otherParticipant(conv)?.displayName ?? 'Unknown' }}
            </span>
            <span class="text-[10px] text-gray-400 shrink-0 ml-2">
              {{ formatTime(conv.lastMessage?.createdAt) }}
            </span>
          </div>
          <p
            class="text-xs truncate mt-0.5"
            :class="conv.unreadCount > 0 ? 'text-gray-900 font-medium' : 'text-gray-500'"
          >
            {{ lastMessagePreview(conv) }}
          </p>
        </div>
      </li>
    </ul>
  </div>
</template>
