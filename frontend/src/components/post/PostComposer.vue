<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { postService, type PostDto, type PostLinkPreview } from '@/services/postService'
import { useFeedStore } from '@/stores/feed'
import { useAuthStore } from '@/stores/auth'
import { useCompanyModeStore } from '@/stores/companyMode'

const emit = defineEmits<{ created: [post: PostDto] }>()

const auth = useAuthStore()
const feed = useFeedStore()
const companyMode = useCompanyModeStore()

// ── State ─────────────────────────────────────────────────────────────────────

type PostType = 'Text' | 'Image' | 'Video' | 'TextOnBackground'
type Privacy = 'Everyone' | 'Friends' | 'OnlyMe'

const isOpen = ref(false)
const postType = ref<PostType>('Text')
const content = ref('')
const privacy = ref<Privacy>('Friends')
const background = ref('#1877f2')
const mediaFiles = ref<File[]>([])
const mediaPreviewUrls = ref<string[]>([])
const submitting = ref(false)
const error = ref<string | null>(null)

// ── URL preview state ────────────────────────────────────────────────────────
const linkPreview = ref<PostLinkPreview | null>(null)
const linkPreviewUrl = ref<string | null>(null)
const linkPreviewLoading = ref(false)
const linkPreviewDismissed = ref<Set<string>>(new Set())
let linkPreviewDebounce: ReturnType<typeof setTimeout> | null = null

const URL_REGEX = /https?:\/\/[^\s<>"']+/i

function extractFirstUrl(text: string): string | null {
  const match = text.match(URL_REGEX)
  if (!match) return null
  return match[0].replace(/[.,;:!?)\]]+$/, '')
}

async function refreshLinkPreview() {
  const url = extractFirstUrl(content.value)
  if (!url) {
    linkPreview.value = null
    linkPreviewUrl.value = null
    return
  }
  if (linkPreviewDismissed.value.has(url)) return
  if (linkPreviewUrl.value === url) return
  linkPreviewUrl.value = url
  linkPreviewLoading.value = true
  try {
    const og = await postService.fetchMetaPreview(url)
    if (linkPreviewUrl.value !== url) return
    linkPreview.value = {
      url: og.url,
      title: og.title,
      description: og.description,
      imageUrl: og.imageUrl,
      siteName: og.siteName,
    }
  } catch {
    if (linkPreviewUrl.value === url) linkPreview.value = null
  } finally {
    if (linkPreviewUrl.value === url) linkPreviewLoading.value = false
  }
}

function dismissLinkPreview() {
  if (linkPreviewUrl.value) linkPreviewDismissed.value.add(linkPreviewUrl.value)
  linkPreview.value = null
  linkPreviewUrl.value = null
  linkPreviewLoading.value = false
}

watch(content, () => {
  if (linkPreviewDebounce) clearTimeout(linkPreviewDebounce)
  linkPreviewDebounce = setTimeout(refreshLinkPreview, 400)
})

// Predefined backgrounds for TextOnBackground
const backgrounds = [
  '#1877f2', // blue
  '#e74c3c', // red
  '#2ecc71', // green
  '#9b59b6', // purple
  '#f39c12', // orange
  '#1abc9c', // teal
  '#e91e63', // pink
  '#34495e', // dark
  'linear-gradient(135deg,#667eea,#764ba2)',
  'linear-gradient(135deg,#f093fb,#f5576c)',
  'linear-gradient(135deg,#4facfe,#00f2fe)',
  'linear-gradient(135deg,#43e97b,#38f9d7)',
]

// ── Computed ──────────────────────────────────────────────────────────────────

const canSubmit = computed(() => {
  if (submitting.value) return false
  if (postType.value === 'Text' || postType.value === 'TextOnBackground') {
    return content.value.trim().length > 0
  }
  return mediaFiles.value.length > 0
})

const tobStyle = computed(() => {
  const bg = background.value
  if (bg.startsWith('http') || bg.startsWith('/')) {
    return { backgroundImage: `url(${bg})`, backgroundSize: 'cover', backgroundPosition: 'center' }
  }
  return { background: bg }
})

/** The identity shown in the author row. */
const authorName = computed(() =>
  companyMode.isActive
    ? companyMode.activeCompany!.name
    : (auth.user?.displayName ?? '?')
)
const authorInitial = computed(() => authorName.value.charAt(0).toUpperCase())

// ── Watchers ──────────────────────────────────────────────────────────────────

watch(postType, () => {
  mediaFiles.value = []
  mediaPreviewUrls.value.forEach(URL.revokeObjectURL)
  mediaPreviewUrls.value = []
})

// ── Methods ───────────────────────────────────────────────────────────────────

function open() {
  isOpen.value = true
}

function close() {
  isOpen.value = false
  reset()
}

function reset() {
  content.value = ''
  privacy.value = 'Friends'
  postType.value = 'Text'
  background.value = '#1877f2'
  mediaFiles.value = []
  mediaPreviewUrls.value.forEach(URL.revokeObjectURL)
  mediaPreviewUrls.value = []
  error.value = null
  submitting.value = false
  linkPreview.value = null
  linkPreviewUrl.value = null
  linkPreviewLoading.value = false
  linkPreviewDismissed.value = new Set()
}

function handleFileInput(event: Event) {
  const input = event.target as HTMLInputElement
  if (!input.files) return
  const newFiles = Array.from(input.files)
  for (const file of newFiles) {
    mediaFiles.value.push(file)
    mediaPreviewUrls.value.push(URL.createObjectURL(file))
  }
  input.value = ''
}

function removeMedia(index: number) {
  URL.revokeObjectURL(mediaPreviewUrls.value[index])
  mediaFiles.value.splice(index, 1)
  mediaPreviewUrls.value.splice(index, 1)
}

async function submit() {
  if (!canSubmit.value) return
  submitting.value = true
  error.value = null
  try {
    const post = await feed.createPost({
      content: content.value.trim(),
      postType: postType.value,
      privacy: privacy.value,
      background: postType.value === 'TextOnBackground' ? background.value : undefined,
      mediaFiles: mediaFiles.value.length > 0 ? mediaFiles.value : undefined,
      // Post as the active company when in company mode, otherwise as personal user.
      companyPageId: companyMode.activeCompany?.id ?? undefined,
      linkPreview: linkPreview.value ?? undefined,
    })
    emit('created', post)
    close()
  } catch (e: unknown) {
    error.value = extractError(e)
  } finally {
    submitting.value = false
  }
}

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const axiosError = e as { response?: { data?: { error?: string } }; message?: string }
    return axiosError.response?.data?.error ?? axiosError.message ?? 'Failed to create post.'
  }
  return 'Failed to create post.'
}

defineExpose({ open, close })
</script>

<template>
  <!-- Trigger bar (always visible) -->
  <div
    data-testid="post-composer"
    class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 p-3 flex items-center gap-3 cursor-pointer hover:bg-slate-700 transition"
    @click="open"
  >
    <div
      class="w-10 h-10 rounded-full flex items-center justify-center text-white font-semibold text-sm shrink-0"
      :class="companyMode.isActive ? 'bg-indigo-600' : 'bg-blue-500'"
    >
      {{ authorInitial }}
    </div>
    <div data-testid="post-composer-trigger" class="flex-1 bg-slate-700 rounded-full px-4 py-2 text-slate-500 text-sm select-none">
      <span v-if="companyMode.isActive">Post as {{ companyMode.activeCompany?.name }}…</span>
      <span v-else>What's on your mind?</span>
    </div>
  </div>

  <!-- Modal overlay -->
  <Teleport to="body">
    <Transition name="modal">
      <div
        v-if="isOpen"
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50"
        @mousedown.self="close"
      >
        <div
          data-testid="post-modal"
          class="bg-slate-800 rounded-2xl shadow-2xl w-full max-w-lg flex flex-col max-h-[90vh]"
          @click.stop
        >
          <!-- Modal header -->
          <div class="flex items-center justify-between px-5 py-4 border-b border-slate-700">
            <h2 class="text-lg font-semibold text-slate-100">Create Post</h2>
            <button
              class="text-slate-500 hover:text-slate-400 p-1 rounded-full hover:bg-slate-700 transition"
              @click="close"
              aria-label="Close"
            >
              <svg class="w-5 h-5" fill="currentColor" viewBox="0 0 20 20">
                <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd"/>
              </svg>
            </button>
          </div>

          <!-- Scrollable body -->
          <div class="flex-1 overflow-y-auto px-5 py-4 space-y-4">

            <!-- Author row -->
            <div class="flex items-center gap-3">
              <div
                class="w-10 h-10 rounded-full flex items-center justify-center text-white font-semibold text-sm shrink-0"
                :class="companyMode.isActive ? 'bg-indigo-600' : 'bg-blue-500'"
              >
                {{ authorInitial }}
              </div>
              <div>
                <p class="font-semibold text-slate-100 text-sm">{{ authorName }}</p>
                <!-- Privacy selector -->
                <select
                  v-model="privacy"
                  class="mt-0.5 text-xs bg-slate-700 border-0 rounded-md px-2 py-0.5 text-slate-300 focus:ring-2 focus:ring-blue-400 cursor-pointer"
                >
                  <option value="Everyone">🌐 Everyone</option>
                  <option value="Friends">👥 Friends</option>
                  <option value="OnlyMe">🔒 Only me</option>
                </select>
              </div>
            </div>

            <!-- Post type tabs -->
            <div class="flex gap-1 bg-slate-900 p-1 rounded-xl text-sm">
              <button
                v-for="(label, type) in { Text: '✍️ Text', Image: '🖼️ Image', Video: '🎬 Video', TextOnBackground: '🎨 BG Text' }"
                :key="type"
                :class="[
                  'flex-1 py-1.5 px-2 rounded-lg font-medium transition',
                  postType === type
                    ? 'bg-slate-800 shadow text-blue-400'
                    : 'text-gray-500 hover:text-slate-300'
                ]"
                @click="postType = type as PostType"
              >
                {{ label }}
              </button>
            </div>

            <!-- Text area -->
            <textarea
              v-if="postType !== 'TextOnBackground'"
              v-model="content"
              data-testid="post-textarea"
              rows="4"
              class="w-full resize-none border-0 bg-transparent text-slate-100 placeholder-slate-500 focus:outline-none text-lg leading-relaxed"
              placeholder="What's on your mind?"
              autofocus
            />

            <!-- TextOnBackground preview + text -->
            <div v-else class="space-y-3">
              <!-- Background picker -->
              <div class="flex flex-wrap gap-2">
                <button
                  v-for="bg in backgrounds"
                  :key="bg"
                  class="w-8 h-8 rounded-full border-2 transition"
                  :class="background === bg ? 'border-gray-900 scale-110' : 'border-transparent'"
                  :style="{ background: bg }"
                  @click="background = bg"
                  :aria-label="`Background ${bg}`"
                />
              </div>

              <!-- Live preview -->
              <div
                :style="tobStyle"
                class="rounded-xl flex items-center justify-center min-h-36 p-6"
              >
                <textarea
                  v-model="content"
                  rows="3"
                  class="w-full resize-none bg-transparent text-white text-xl font-bold text-center placeholder-white/60 focus:outline-none drop-shadow leading-relaxed"
                  placeholder="Your text here…"
                />
              </div>
            </div>

            <!-- Media upload (Image / Video) -->
            <div v-if="postType === 'Image' || postType === 'Video'" class="space-y-3">
              <!-- Optional caption -->
              <textarea
                v-model="content"
                rows="2"
                class="w-full resize-none border border-slate-700 rounded-xl px-3 py-2 text-slate-100 placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-400 text-sm"
                placeholder="Add a caption… (optional)"
              />

              <!-- File input -->
              <label
                class="flex flex-col items-center justify-center border-2 border-dashed border-slate-600 rounded-xl py-6 cursor-pointer hover:border-blue-400 hover:bg-blue-900/40 transition"
              >
                <svg class="w-8 h-8 text-slate-500 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/>
                </svg>
                <span class="text-sm text-gray-500">
                  {{ postType === 'Video' ? 'Click to select video' : 'Click to select images' }}
                </span>
                <input
                  type="file"
                  :accept="postType === 'Video' ? 'video/*' : 'image/*'"
                  :multiple="postType === 'Image'"
                  class="hidden"
                  @change="handleFileInput"
                />
              </label>

              <!-- Previews -->
              <div v-if="mediaPreviewUrls.length" class="grid grid-cols-3 gap-2">
                <div
                  v-for="(url, i) in mediaPreviewUrls"
                  :key="i"
                  class="relative rounded-lg overflow-hidden aspect-square bg-black"
                >
                  <video
                    v-if="postType === 'Video'"
                    :src="url"
                    class="w-full h-full object-contain"
                    preload="metadata"
                  />
                  <img
                    v-else
                    :src="url"
                    :alt="`Preview ${i + 1}`"
                    class="w-full h-full object-cover"
                  />
                  <button
                    class="absolute top-1 right-1 bg-black/60 text-white rounded-full w-5 h-5 flex items-center justify-center text-xs hover:bg-black/80 transition"
                    @click="removeMedia(i)"
                    aria-label="Remove"
                  >×</button>
                </div>
              </div>
            </div>

            <!-- Link preview card (auto-fetched when a URL is detected) -->
            <div
              v-if="linkPreviewLoading"
              class="flex items-center gap-3 border border-slate-700 rounded-xl p-3 bg-slate-950"
            >
              <svg class="animate-spin w-5 h-5 text-blue-500" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
              </svg>
              <span class="text-sm text-gray-500">Fetching link preview…</span>
            </div>

            <div
              v-else-if="linkPreview"
              class="relative border border-slate-700 rounded-xl overflow-hidden bg-slate-800"
            >
              <button
                type="button"
                class="absolute top-2 right-2 bg-black/50 text-white rounded-full w-6 h-6 flex items-center justify-center text-xs hover:bg-black/70 transition z-10"
                @click="dismissLinkPreview"
                aria-label="Remove link preview"
              >×</button>
              <img
                v-if="linkPreview.imageUrl"
                :src="linkPreview.imageUrl"
                :alt="linkPreview.title ?? ''"
                class="w-full max-h-48 object-cover bg-slate-700"
              />
              <div class="px-3 py-2">
                <p v-if="linkPreview.siteName" class="text-xs uppercase tracking-wide text-slate-500">
                  {{ linkPreview.siteName }}
                </p>
                <p
                  v-if="linkPreview.title"
                  class="font-semibold text-sm text-slate-100 line-clamp-2"
                >{{ linkPreview.title }}</p>
                <p
                  v-if="linkPreview.description"
                  class="text-xs text-gray-500 mt-1 line-clamp-2"
                >{{ linkPreview.description }}</p>
                <p class="text-xs text-blue-400 mt-1 truncate">{{ linkPreview.url }}</p>
              </div>
            </div>

            <!-- Error -->
            <p v-if="error" class="text-sm text-red-400 bg-red-950 border border-red-800 rounded-lg px-3 py-2">
              {{ error }}
            </p>
          </div>

          <!-- Footer -->
          <div class="px-5 py-4 border-t border-slate-700">
            <button
              data-testid="post-submit-button"
              class="w-full py-2.5 rounded-xl font-semibold text-sm transition"
              :class="canSubmit
                ? 'bg-blue-600 hover:bg-blue-700 text-white'
                : 'bg-slate-700 text-slate-500 cursor-not-allowed'"
              :disabled="!canSubmit"
              @click="submit"
            >
              <span v-if="submitting" class="flex items-center justify-center gap-2">
                <svg class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
                  <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                  <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
                </svg>
                Posting…
              </span>
              <span v-else>Post</span>
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.2s ease;
}
.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}
.modal-enter-active .bg-slate-800,
.modal-leave-active .bg-slate-800 {
  transition: transform 0.2s ease;
}
.modal-enter-from .bg-slate-800 {
  transform: scale(0.95);
}
</style>
