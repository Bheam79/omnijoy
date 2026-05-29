<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useChatStore } from '@/stores/chat'
import ChatWindow from './ChatWindow.vue'
import ConversationList from './ConversationList.vue'

const auth = useAuthStore()
const chat = useChatStore()

const openConversations = computed(() =>
  chat.openWindows
    .map((id) => chat.conversations.find((c) => c.id === id))
    .filter(Boolean) as NonNullable<ReturnType<typeof chat.conversations.find>>[],
)

onMounted(() => {
  if (auth.isAuthenticated) {
    chat.connect()
    chat.loadConversations()
  }
})

onUnmounted(() => {
  chat.disconnect()
})
</script>

<template>
  <!-- Only render when authenticated -->
  <template v-if="auth.isAuthenticated">
    <!-- Floating container anchored to bottom-right -->
    <div class="fixed bottom-0 right-4 z-[9000] flex items-end gap-3 pointer-events-none">
      <!-- Open chat windows (rendered right-to-left so newest is on the right) -->
      <div
        v-for="conv in openConversations"
        :key="conv.id"
        class="pointer-events-auto"
      >
        <ChatWindow :conversation="conv" />
      </div>

      <!-- Conversation list panel -->
      <Transition
        enter-active-class="transition ease-out duration-150 origin-bottom-right"
        enter-from-class="opacity-0 scale-95"
        enter-to-class="opacity-100 scale-100"
        leave-active-class="transition ease-in duration-100 origin-bottom-right"
        leave-from-class="opacity-100 scale-100"
        leave-to-class="opacity-0 scale-95"
      >
        <div v-if="chat.listOpen" class="pointer-events-auto mb-0">
          <ConversationList />
        </div>
      </Transition>

      <!-- Messenger FAB -->
      <button
        class="pointer-events-auto mb-4 h-12 w-12 rounded-full bg-indigo-600 text-white shadow-xl hover:bg-indigo-700 active:scale-95 transition-all flex items-center justify-center relative"
        aria-label="Open Messenger"
        @click="chat.toggleList()"
      >
        <svg class="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M8 12h.01M12 12h.01M16 12h.01M21 8a9 9 0 11-18 0 9 9 0 0118 0z"/>
        </svg>
        <!-- Unread badge -->
        <span
          v-if="chat.totalUnread > 0"
          class="absolute -top-1 -right-1 min-w-[1.25rem] h-5 rounded-full bg-red-500 ring-2 ring-slate-900 flex items-center justify-center text-white text-[10px] font-bold px-1"
        >
          {{ chat.totalUnread > 99 ? '99+' : chat.totalUnread }}
        </span>
      </button>
    </div>

    <!-- Click-away to close conversation list -->
    <div
      v-if="chat.listOpen"
      class="fixed inset-0 z-[8999]"
      @click="chat.toggleList()"
    />
  </template>
</template>
