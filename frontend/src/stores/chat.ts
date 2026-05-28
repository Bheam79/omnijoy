/**
 * Pinia store for the Messenger feature.
 *
 * - conversations  — list of all conversations (sorted by most-recent message)
 * - messages       — map of conversationId → Message[]
 * - openWindows    — conversationIds of currently-open chat windows (max 3)
 * - minimizedWindows — Set of minimized window IDs
 * - listOpen       — whether the ConversationList panel is visible
 * - SignalR connection to /hubs/chat
 */
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import * as signalR from '@microsoft/signalr'
import type { Conversation, Message } from '@/types'
import { chatService } from '@/services/chatService'
import { useAuthStore } from '@/stores/auth'

const MAX_OPEN_WINDOWS = 3

export const useChatStore = defineStore('chat', () => {
  const auth = useAuthStore()

  // ── State ──────────────────────────────────────────────────────────────────

  const conversations    = ref<Conversation[]>([])
  const messages         = ref<Record<string, Message[]>>({})
  const openWindows      = ref<string[]>([])           // ordered list of open conversation IDs
  const minimizedWindows = ref<Set<string>>(new Set()) // IDs of minimized windows
  const listOpen         = ref(false)
  const loading          = ref(false)
  /** conversationId → array of userId strings currently typing */
  const typingUsers      = ref<Record<string, string[]>>({})

  let hubConnection: signalR.HubConnection | null = null

  // ── Computed ───────────────────────────────────────────────────────────────

  const totalUnread = computed(() =>
    conversations.value.reduce((sum, c) => sum + (c.unreadCount ?? 0), 0),
  )

  // ── Conversation actions ───────────────────────────────────────────────────

  async function loadConversations() {
    loading.value = true
    try {
      conversations.value = await chatService.getConversations()
    } finally {
      loading.value = false
    }
  }

  /**
   * Start or open a DM with another user.
   * Upserts the conversation into the list and opens a chat window.
   */
  async function openConversationWith(userId: string) {
    const conv = await chatService.getOrCreateDirect(userId)
    upsertConversation(conv)
    openWindow(conv.id)
  }

  // ── Message actions ────────────────────────────────────────────────────────

  async function loadMessages(conversationId: string, before?: string) {
    const msgs = await chatService.getMessages(conversationId, before)
    if (!before) {
      messages.value[conversationId] = msgs
    } else {
      // Prepend older messages (cursor pagination)
      messages.value[conversationId] = [
        ...msgs,
        ...(messages.value[conversationId] ?? []),
      ]
    }
  }

  async function sendMessage(
    conversationId: string,
    content?: string,
    file?: File,
    onProgress?: (p: number) => void,
  ): Promise<Message> {
    const msg = await chatService.sendMessage(conversationId, content, file, onProgress)
    appendMessage(msg)
    updateConversationLastMessage(msg)
    return msg
  }

  async function deleteMessage(messageId: string, conversationId: string) {
    const msg = await chatService.deleteMessage(messageId)
    replaceMessage(conversationId, msg)
  }

  async function markRead(conversationId: string) {
    await chatService.markRead(conversationId)
    const conv = conversations.value.find((c) => c.id === conversationId)
    if (conv) conv.unreadCount = 0
  }

  // ── Window management ──────────────────────────────────────────────────────

  function openWindow(conversationId: string) {
    if (!openWindows.value.includes(conversationId)) {
      if (openWindows.value.length >= MAX_OPEN_WINDOWS) {
        // Remove the oldest open window to make room
        openWindows.value.shift()
      }
      openWindows.value.push(conversationId)
    }
    minimizedWindows.value.delete(conversationId)
    listOpen.value = false
  }

  function closeWindow(conversationId: string) {
    openWindows.value = openWindows.value.filter((id) => id !== conversationId)
    minimizedWindows.value.delete(conversationId)
  }

  function toggleMinimize(conversationId: string) {
    if (minimizedWindows.value.has(conversationId)) {
      minimizedWindows.value.delete(conversationId)
    } else {
      minimizedWindows.value.add(conversationId)
    }
  }

  function toggleList() {
    listOpen.value = !listOpen.value
  }

  // ── SignalR ────────────────────────────────────────────────────────────────

  function connect() {
    if (!auth.accessToken || hubConnection) return

    hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/chat', {
        accessTokenFactory: () => auth.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    // ── Incoming events ────────────────────────────────────────────────────────
    hubConnection.on('ReceiveMessage', (msg: Message) => {
      appendMessage(msg)
      updateConversationLastMessage(msg)

      // Bump unread only when the window is not open / is minimized
      if (
        msg.sender.id !== auth.user?.id &&
        (!openWindows.value.includes(msg.conversationId) ||
          minimizedWindows.value.has(msg.conversationId))
      ) {
        const conv = conversations.value.find((c) => c.id === msg.conversationId)
        if (conv) conv.unreadCount = (conv.unreadCount ?? 0) + 1
      }
    })

    hubConnection.on(
      'MessageDeleted',
      (payload: { messageId: string; conversationId: string }) => {
        const msgs = messages.value[payload.conversationId]
        if (!msgs) return
        const idx = msgs.findIndex((m) => m.id === payload.messageId)
        if (idx !== -1) {
          msgs[idx] = {
            ...msgs[idx],
            isDeleted: true,
            content: undefined,
            media: undefined,
          }
        }
      },
    )

    hubConnection.on('ConversationCreated', (conv: Conversation) => {
      upsertConversation(conv)
    })

    hubConnection.on('UserTyping', (convId: string, userId: string) => {
      if (!typingUsers.value[convId]) typingUsers.value[convId] = []
      if (!typingUsers.value[convId].includes(userId)) {
        typingUsers.value[convId] = [...typingUsers.value[convId], userId]
      }
      // Auto-clear after 3 s of silence
      setTimeout(() => {
        typingUsers.value[convId] = (typingUsers.value[convId] ?? []).filter((id) => id !== userId)
      }, 3000)
    })

    hubConnection.on('UserOnline',  (_userId: string) => { /* future presence UI */ })
    hubConnection.on('UserOffline', (_userId: string) => { /* future presence UI */ })

    hubConnection.start().catch(() => { /* silent fail – dev hot-reload friendly */ })
  }

  async function joinConversation(conversationId: string) {
    if (hubConnection?.state === signalR.HubConnectionState.Connected) {
      await hubConnection.invoke('JoinConversation', conversationId)
    }
  }

  async function leaveConversation(conversationId: string) {
    if (hubConnection?.state === signalR.HubConnectionState.Connected) {
      await hubConnection.invoke('LeaveConversation', conversationId)
    }
  }

  async function sendTyping(conversationId: string) {
    if (hubConnection?.state === signalR.HubConnectionState.Connected) {
      await hubConnection.invoke('SendTyping', conversationId)
    }
  }

  function disconnect() {
    hubConnection?.stop()
    hubConnection = null
  }

  // ── Private helpers ────────────────────────────────────────────────────────

  function appendMessage(msg: Message) {
    if (!messages.value[msg.conversationId]) {
      messages.value[msg.conversationId] = []
    }
    // Deduplicate by id
    if (!messages.value[msg.conversationId].some((m) => m.id === msg.id)) {
      messages.value[msg.conversationId].push(msg)
    }
  }

  function replaceMessage(conversationId: string, msg: Message) {
    const msgs = messages.value[conversationId]
    if (!msgs) return
    const idx = msgs.findIndex((m) => m.id === msg.id)
    if (idx !== -1) msgs[idx] = msg
  }

  function updateConversationLastMessage(msg: Message) {
    const idx = conversations.value.findIndex((c) => c.id === msg.conversationId)
    if (idx !== -1) {
      conversations.value[idx] = {
        ...conversations.value[idx],
        lastMessage: msg,
      }
      // Move conversation to top
      const [conv] = conversations.value.splice(idx, 1)
      conversations.value.unshift(conv)
    }
  }

  function upsertConversation(conv: Conversation) {
    const idx = conversations.value.findIndex((c) => c.id === conv.id)
    if (idx === -1) {
      conversations.value.unshift(conv)
    } else {
      conversations.value[idx] = conv
    }
  }

  // ── Public API ─────────────────────────────────────────────────────────────

  return {
    conversations,
    messages,
    openWindows,
    minimizedWindows,
    listOpen,
    loading,
    typingUsers,
    totalUnread,
    // actions
    loadConversations,
    openConversationWith,
    loadMessages,
    sendMessage,
    deleteMessage,
    markRead,
    // window management
    openWindow,
    closeWindow,
    toggleMinimize,
    toggleList,
    // SignalR
    connect,
    joinConversation,
    leaveConversation,
    sendTyping,
    disconnect,
  }
})
