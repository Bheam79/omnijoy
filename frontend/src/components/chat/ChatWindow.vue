<script setup lang="ts">
import {
  ref,
  computed,
  watch,
  onMounted,
  onUnmounted,
  nextTick,
} from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useChatStore } from '@/stores/chat'
import type { Conversation, Message } from '@/types'
import OgPreviewCard from './OgPreviewCard.vue'

const props = defineProps<{ conversation: Conversation }>()

const auth = useAuthStore()
const chat = useChatStore()

const messagesEl = ref<HTMLElement | null>(null)
const textInput  = ref('')
const fileInput  = ref<HTMLInputElement | null>(null)
const pendingFile = ref<File | null>(null)
const uploadProgress = ref(0)
const sending = ref(false)
const isDragging = ref(false)
let ownTypingTimer: ReturnType<typeof setTimeout> | null = null
let typingSent = false

// ── Helpers ──────────────────────────────────────────────────────────────────

const conversationId = computed(() => props.conversation.id)
const isMinimized    = computed(() => chat.minimizedWindows.has(conversationId.value))

const otherParticipant = computed(() =>
  props.conversation.participants.find((p) => p.id !== auth.user?.id),
)

const windowMessages = computed(
  () => chat.messages[conversationId.value] ?? [],
)

const isTyping = computed(() => {
  const typers = chat.typingUsers[conversationId.value] ?? []
  return typers.some((id) => id !== auth.user?.id)
})

// URL detection regex
const URL_RE = /https?:\/\/[^\s<>'"]+/

function hasUrl(text?: string) {
  return text ? URL_RE.test(text) : false
}

function extractUrl(text?: string): string | null {
  if (!text) return null
  const m = text.match(URL_RE)
  return m ? m[0] : null
}

function formatFileSize(bytes?: number | null): string {
  if (!bytes) return ''
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

function isMine(msg: Message): boolean {
  return msg.sender.id === auth.user?.id
}

// ── Scroll to bottom ──────────────────────────────────────────────────────────

async function scrollToBottom() {
  await nextTick()
  if (messagesEl.value) {
    messagesEl.value.scrollTop = messagesEl.value.scrollHeight
  }
}

// ── Load older messages (cursor pagination) ───────────────────────────────────

async function loadOlderMessages() {
  const msgs = windowMessages.value
  if (!msgs.length) return
  const oldest = msgs[0].createdAt
  await chat.loadMessages(conversationId.value, oldest)
}

// ── Sending ───────────────────────────────────────────────────────────────────

async function send() {
  const text = textInput.value.trim()
  if (!text && !pendingFile.value) return
  if (sending.value) return

  sending.value = true
  uploadProgress.value = 0

  const file = pendingFile.value ?? undefined
  const content = text || undefined

  try {
    textInput.value = ''
    pendingFile.value = null
    await chat.sendMessage(
      conversationId.value,
      content,
      file,
      (p) => { uploadProgress.value = p },
    )
    scrollToBottom()
  } finally {
    sending.value = false
    uploadProgress.value = 0
  }
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault()
    send()
    return
  }

  // Typing indicator — debounced, sent at most once per 3 s
  if (!typingSent) {
    typingSent = true
    chat.sendTyping(conversationId.value)
    if (ownTypingTimer) clearTimeout(ownTypingTimer)
    ownTypingTimer = setTimeout(() => { typingSent = false }, 3000)
  }
}

// ── File picker / drag-drop ───────────────────────────────────────────────────

function openFilePicker() {
  fileInput.value?.click()
}

function onFileSelected(e: Event) {
  const input = e.target as HTMLInputElement
  if (input.files?.[0]) setPendingFile(input.files[0])
  input.value = ''
}

function setPendingFile(file: File) {
  pendingFile.value = file
}

function onDragover(e: DragEvent) {
  e.preventDefault()
  isDragging.value = true
}

function onDragleave() {
  isDragging.value = false
}

function onDrop(e: DragEvent) {
  e.preventDefault()
  isDragging.value = false
  const file = e.dataTransfer?.files[0]
  if (file) setPendingFile(file)
}

// ── Context menu (delete) ─────────────────────────────────────────────────────

const contextMenu = ref<{ msg: Message; x: number; y: number } | null>(null)

function onContextMenu(e: MouseEvent, msg: Message) {
  if (!isMine(msg) || msg.isDeleted) return
  e.preventDefault()
  contextMenu.value = { msg, x: e.clientX, y: e.clientY }
}

async function confirmDelete() {
  if (!contextMenu.value) return
  const { msg } = contextMenu.value
  contextMenu.value = null
  await chat.deleteMessage(msg.id, conversationId.value)
}

// ── Read marking ──────────────────────────────────────────────────────────────

function markAsRead() {
  if (props.conversation.unreadCount > 0) {
    chat.markRead(conversationId.value)
  }
}

// Typing state is tracked in the chat store's typingUsers map.
// The computed `isTyping` above reads from it directly.

// ── Lifecycle ─────────────────────────────────────────────────────────────────

onMounted(async () => {
  await chat.joinConversation(conversationId.value)
  if (!chat.messages[conversationId.value]) {
    await chat.loadMessages(conversationId.value)
  }
  scrollToBottom()
  markAsRead()
})

onUnmounted(async () => {
  await chat.leaveConversation(conversationId.value)
  if (ownTypingTimer) clearTimeout(ownTypingTimer)
})

// Auto-scroll on new messages
watch(
  () => chat.messages[conversationId.value]?.length,
  () => {
    if (!isMinimized.value) scrollToBottom()
  },
)

// Mark read when window becomes active
watch(isMinimized, (min) => {
  if (!min) markAsRead()
})

// Close context menu on outside click
function onDocClick() {
  contextMenu.value = null
}
</script>

<template>
  <!-- Context-menu click-away -->
  <div v-if="contextMenu" class="fixed inset-0 z-[9998]" @click="onDocClick" />

  <div
    class="flex flex-col w-72 shadow-2xl rounded-t-xl overflow-hidden border border-slate-700 bg-slate-800"
    :class="isMinimized ? 'h-11' : 'h-96'"
    @dragover.prevent="onDragover"
    @dragleave="onDragleave"
    @drop="onDrop"
  >
    <!-- ── Header ────────────────────────────────────────────────────────── -->
    <div
      class="flex items-center gap-2 px-3 h-11 shrink-0 bg-indigo-600 text-white cursor-pointer select-none"
      @click="chat.toggleMinimize(conversationId)"
    >
      <!-- Avatar -->
      <div class="shrink-0 h-7 w-7 rounded-full overflow-hidden bg-indigo-400 flex items-center justify-center text-xs font-bold">
        <img
          v-if="otherParticipant?.avatarUrl"
          :src="otherParticipant.avatarUrl"
          :alt="otherParticipant.displayName"
          class="h-full w-full object-cover"
        />
        <span v-else>{{ otherParticipant?.displayName?.[0]?.toUpperCase() ?? '?' }}</span>
      </div>

      <!-- Name -->
      <span class="flex-1 text-sm font-semibold truncate">
        {{ otherParticipant?.displayName ?? 'Conversation' }}
      </span>

      <!-- Minimize / close buttons -->
      <button
        class="p-0.5 rounded hover:bg-indigo-500 transition-colors"
        :aria-label="isMinimized ? 'Expand' : 'Minimize'"
        @click.stop="chat.toggleMinimize(conversationId)"
      >
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            :d="isMinimized ? 'M5 15l7-7 7 7' : 'M19 9l-7 7-7-7'" />
        </svg>
      </button>

      <button
        class="p-0.5 rounded hover:bg-indigo-500 transition-colors"
        aria-label="Close"
        @click.stop="chat.closeWindow(conversationId)"
      >
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
        </svg>
      </button>
    </div>

    <!-- ── Message list (hidden when minimized) ──────────────────────────── -->
    <template v-if="!isMinimized">
      <!-- Drag-drop overlay -->
      <div
        v-if="isDragging"
        class="absolute inset-0 z-10 bg-indigo-900/90 flex items-center justify-center text-indigo-400 font-semibold text-sm border-2 border-dashed border-indigo-400 rounded-xl"
      >
        Drop file to attach
      </div>

      <div
        ref="messagesEl"
        class="flex-1 overflow-y-auto px-2 py-2 space-y-1 scroll-smooth"
        @scroll.passive="() => {}"
      >
        <!-- Load older button -->
        <div class="text-center py-1">
          <button
            class="text-[11px] text-indigo-500 hover:underline"
            @click="loadOlderMessages"
          >
            Load older messages
          </button>
        </div>

        <!-- Messages -->
        <template v-for="msg in windowMessages" :key="msg.id">
          <div
            class="flex"
            :class="isMine(msg) ? 'justify-end' : 'justify-start'"
            @contextmenu="onContextMenu($event, msg)"
          >
            <!-- Deleted placeholder -->
            <div
              v-if="msg.isDeleted"
              class="max-w-[80%] px-3 py-1.5 rounded-2xl bg-slate-700 text-slate-500 text-xs italic"
            >
              Message deleted
            </div>

            <!-- Normal message bubble -->
            <div
              v-else
              class="max-w-[80%] px-3 py-1.5 rounded-2xl text-sm"
              :class="isMine(msg)
                ? 'bg-indigo-600 text-white rounded-br-sm'
                : 'bg-slate-700 text-slate-100 rounded-bl-sm'"
            >
              <!-- Image -->
              <template v-if="msg.messageType === 'Image' && msg.media">
                <img
                  :src="msg.media.url"
                  :alt="msg.media.fileName ?? 'image'"
                  class="rounded-lg max-w-full max-h-48 object-cover"
                  loading="lazy"
                />
              </template>

              <!-- Video -->
              <template v-else-if="msg.messageType === 'Video' && msg.media">
                <video
                  :src="msg.media.url"
                  controls
                  class="rounded-lg max-w-full max-h-48"
                />
              </template>

              <!-- File download -->
              <template v-else-if="msg.messageType === 'File' && msg.media">
                <a
                  :href="msg.media.url"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="flex items-center gap-2"
                  :class="isMine(msg) ? 'text-indigo-100 hover:text-white' : 'text-indigo-400 hover:text-indigo-800'"
                >
                  <!-- File icon -->
                  <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
                  </svg>
                  <span class="flex flex-col min-w-0">
                    <span class="text-xs font-medium truncate">{{ msg.media.fileName }}</span>
                    <span class="text-[10px] opacity-70">{{ formatFileSize(msg.media.fileSizeBytes) }}</span>
                  </span>
                </a>
              </template>

              <!-- Text (with optional OG preview) -->
              <template v-else>
                <span class="break-words whitespace-pre-wrap">{{ msg.content }}</span>
                <!-- URL preview -->
                <OgPreviewCard
                  v-if="hasUrl(msg.content)"
                  :url="extractUrl(msg.content)!"
                  :class="isMine(msg) ? 'border-indigo-400' : ''"
                />
              </template>

              <!-- Timestamp -->
              <div
                class="text-[10px] mt-0.5 text-right"
                :class="isMine(msg) ? 'text-indigo-200' : 'text-slate-500'"
              >
                {{ formatTime(msg.createdAt) }}
              </div>
            </div>
          </div>
        </template>

        <!-- Typing indicator -->
        <div v-if="isTyping" class="flex justify-start">
          <div class="bg-slate-700 rounded-2xl rounded-bl-sm px-3 py-2 flex gap-1">
            <span
              v-for="i in 3"
              :key="i"
              class="h-1.5 w-1.5 rounded-full bg-gray-400 animate-bounce"
              :style="{ animationDelay: `${(i - 1) * 150}ms` }"
            />
          </div>
        </div>
      </div>

      <!-- ── Upload progress ─────────────────────────────────────────────── -->
      <div v-if="uploadProgress > 0 && uploadProgress < 100" class="px-3 pb-1">
        <div class="h-1 bg-slate-700 rounded-full overflow-hidden">
          <div
            class="h-full bg-indigo-500 transition-all duration-200"
            :style="{ width: `${uploadProgress}%` }"
          />
        </div>
      </div>

      <!-- ── Pending file preview ────────────────────────────────────────── -->
      <div v-if="pendingFile" class="px-3 pb-1 flex items-center gap-2 text-xs text-slate-400">
        <svg class="h-4 w-4 text-slate-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M15.172 7l-6.586 6.586a2 2 0 102.828 2.828l6.414-6.586a4 4 0 00-5.656-5.656l-6.415 6.585a6 6 0 108.486 8.486L20.5 13"/>
        </svg>
        <span class="truncate flex-1">{{ pendingFile.name }}</span>
        <button class="text-slate-500 hover:text-slate-400" @click="pendingFile = null">
          <svg class="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
          </svg>
        </button>
      </div>

      <!-- ── Composer ────────────────────────────────────────────────────── -->
      <div class="shrink-0 border-t border-slate-700 flex items-end gap-1 px-2 py-1.5">
        <!-- File picker -->
        <button
          class="p-1.5 rounded-full text-slate-500 hover:text-indigo-400 hover:bg-slate-700 transition-colors shrink-0"
          aria-label="Attach file"
          @click="openFilePicker"
        >
          <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M15.172 7l-6.586 6.586a2 2 0 102.828 2.828l6.414-6.586a4 4 0 00-5.656-5.656l-6.415 6.585a6 6 0 108.486 8.486L20.5 13"/>
          </svg>
        </button>
        <input
          ref="fileInput"
          type="file"
          class="hidden"
          accept="image/*,video/*,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.zip,.rar,.7z"
          @change="onFileSelected"
        />

        <!-- Text input -->
        <textarea
          v-model="textInput"
          rows="1"
          placeholder="Write a message…"
          class="flex-1 resize-none rounded-2xl bg-slate-700 px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo-400 max-h-28 overflow-y-auto"
          @keydown="onKeydown"
        />

        <!-- Send button -->
        <button
          class="p-1.5 rounded-full transition-colors shrink-0"
          :class="textInput.trim() || pendingFile
            ? 'text-white bg-indigo-600 hover:bg-indigo-700'
            : 'text-slate-600 bg-slate-700 cursor-not-allowed'"
          :disabled="!textInput.trim() && !pendingFile"
          aria-label="Send"
          @click="send"
        >
          <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8"/>
          </svg>
        </button>
      </div>
    </template>
  </div>

  <!-- ── Right-click context menu ──────────────────────────────────────────── -->
  <Teleport to="body">
    <div
      v-if="contextMenu"
      class="fixed z-[9999] bg-slate-800 rounded-lg shadow-xl border border-slate-700 py-1 min-w-[140px]"
      :style="{ top: `${contextMenu.y}px`, left: `${contextMenu.x}px` }"
    >
      <button
        class="w-full flex items-center gap-2 px-4 py-2 text-sm text-red-400 hover:bg-red-950 transition-colors"
        @click="confirmDelete"
      >
        <svg class="h-4 w-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/>
        </svg>
        Delete message
      </button>
    </div>
  </Teleport>
</template>
