<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute } from 'vue-router'
import { eventService, type EventDto } from '@/services/eventService'
import { useAuthStore } from '@/stores/auth'
import type { PostDto } from '@/services/postService'
import MentionText from '@/components/shared/MentionText.vue'

const route = useRoute()
const auth = useAuthStore()

const event = ref<EventDto | null>(null)
const loading = ref(true)
const errorMessage = ref<string | null>(null)
const errorTitle = ref<string | null>(null)

// ── Event wall posts (discussion) ─────────────────────────────────────────
const eventPosts   = ref<PostDto[]>([])
const postsLoading = ref(false)
const postsHasMore = ref(false)
const postsPage    = ref(1)

async function loadEventPosts(initial = false) {
  if (!event.value) return
  postsLoading.value = true
  try {
    const result = await eventService.getEventPosts(event.value.id, postsPage.value)
    if (initial || postsPage.value === 1) {
      eventPosts.value = result.items
    } else {
      eventPosts.value.push(...result.items)
    }
    postsHasMore.value = result.hasMore
  } catch {
    // Discussion is best-effort — failing to load it shouldn't block the page.
  } finally {
    postsLoading.value = false
  }
}

async function loadMorePosts() {
  postsPage.value += 1
  await loadEventPosts()
}

onMounted(async () => {
  const id = route.params.id as string
  try {
    event.value = await eventService.getEvent(id)
    await loadEventPosts(true)
  } catch (e: unknown) {
    const axiosError = e as { response?: { status?: number; data?: { error?: string } } }
    const status = axiosError.response?.status
    if (status === 404) {
      errorTitle.value = 'Event not found'
      errorMessage.value = 'This event does not exist or has been deleted.'
    } else if (status === 403 || status === 401) {
      errorTitle.value = 'This event is not public'
      errorMessage.value = 'The organiser has restricted who can see this event.'
    } else {
      errorTitle.value = 'Could not load event'
      errorMessage.value = axiosError.response?.data?.error ?? 'Something went wrong.'
    }
  } finally {
    loading.value = false
  }
})

const whenLabel = computed(() => {
  if (!event.value) return ''
  const start = new Date(event.value.startAt)
  const opts: Intl.DateTimeFormatOptions = {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
    hour12: false,
  }
  return start.toLocaleString(undefined, opts)
})
</script>

<template>
  <main class="min-h-screen bg-slate-700 py-8 px-4">
    <div class="max-w-3xl mx-auto">
      <header class="flex items-center justify-between mb-6">
        <RouterLink to="/" class="text-2xl font-extrabold text-blue-400">Omnijoy</RouterLink>
        <RouterLink
          v-if="!auth.isAuthenticated"
          to="/login"
          class="text-sm font-semibold text-blue-400 hover:underline"
        >Sign in</RouterLink>
        <RouterLink
          v-else
          to="/wall"
          class="text-sm font-semibold text-blue-400 hover:underline"
        >Open Omnijoy</RouterLink>
      </header>

      <div v-if="loading" class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 p-8 text-center text-gray-500">
        Loading…
      </div>

      <div
        v-else-if="errorMessage"
        class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 p-8 text-center"
      >
        <h1 class="text-xl font-semibold text-slate-100 mb-2">{{ errorTitle }}</h1>
        <p class="text-gray-500">{{ errorMessage }}</p>
        <RouterLink
          to="/"
          class="inline-block mt-6 px-5 py-2 rounded-lg bg-blue-600 text-white font-semibold hover:bg-blue-700 transition"
        >Back to Omnijoy</RouterLink>
      </div>

      <template v-else-if="event">
        <article class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 overflow-hidden">
          <img
            v-if="event.coverImageUrl"
            :src="event.coverImageUrl"
            :alt="event.title"
            class="w-full max-h-64 object-cover bg-slate-700"
          />
          <div class="p-6">
            <div class="flex items-start justify-between gap-3">
              <h1 class="text-2xl font-bold text-slate-100 min-w-0">{{ event.title }}</h1>
              <a
                v-if="event.ticketUrl"
                :href="event.ticketUrl"
                target="_blank"
                rel="noopener noreferrer"
                data-testid="event-buy-tickets"
                class="shrink-0 inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-500 shadow transition"
              >
                <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M15 5v2m0 4v2m0 4v2M5 5a2 2 0 00-2 2v3a2 2 0 110 4v3a2 2 0 002 2h14a2 2 0 002-2v-3a2 2 0 110-4V7a2 2 0 00-2-2H5z"/>
                </svg>
                Get tickets
              </a>
            </div>
            <p class="text-sm text-gray-500 mt-1">
              Hosted by <span class="font-semibold">{{ event.creator.displayName }}</span>
            </p>

            <dl class="mt-5 space-y-3 text-sm text-slate-300">
              <div class="flex gap-2">
                <dt class="font-semibold w-28 shrink-0">When</dt>
                <dd>{{ whenLabel }}</dd>
              </div>
              <div v-if="event.location" class="flex gap-2">
                <dt class="font-semibold w-28 shrink-0">Where</dt>
                <dd>{{ event.location }}</dd>
              </div>
              <div class="flex gap-2">
                <dt class="font-semibold w-28 shrink-0">Participants</dt>
                <dd>Going {{ event.goingCount }} - Maybe {{ event.maybeCount }}</dd>
              </div>
            </dl>

            <p
              v-if="event.description"
              class="mt-5 text-slate-300 leading-relaxed whitespace-pre-wrap"
            >{{ event.description }}</p>

            <div class="mt-6 flex gap-3">
              <RouterLink
                :to="auth.isAuthenticated ? `/events/${event.id}` : '/register'"
                class="px-5 py-2 rounded-lg bg-blue-600 text-white font-semibold hover:bg-blue-700 transition"
              >
                {{ auth.isAuthenticated ? 'View event' : 'Sign up to RSVP' }}
              </RouterLink>
            </div>
          </div>
        </article>

        <!-- ── Discussion / event wall ─────────────────────────────────── -->
        <section
          data-testid="event-discussion"
          class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 p-6 mt-5"
        >
          <h2 class="text-base font-semibold text-slate-100 mb-3">
            Discussion
            <span class="ml-1 text-xs font-normal text-gray-500">
              (sign in to join the conversation)
            </span>
          </h2>

          <div v-if="postsLoading && eventPosts.length === 0" class="space-y-3">
            <div v-for="n in 2" :key="n" class="flex gap-3 animate-pulse">
              <div class="w-8 h-8 rounded-full bg-slate-700 shrink-0" />
              <div class="flex-1 space-y-1.5">
                <div class="h-3 bg-slate-700 rounded w-1/4" />
                <div class="h-3 bg-slate-700 rounded w-3/4" />
              </div>
            </div>
          </div>

          <p
            v-else-if="eventPosts.length === 0"
            class="text-sm text-gray-500 italic"
          >
            No posts yet.
          </p>

          <div v-else class="space-y-3">
            <div
              v-for="post in eventPosts"
              :key="post.id"
              class="flex gap-3 p-3 rounded-lg bg-slate-900/60"
            >
              <img
                v-if="post.author.avatarUrl"
                :src="post.author.avatarUrl"
                :alt="post.author.displayName"
                class="w-8 h-8 rounded-full object-cover shrink-0"
              />
              <div
                v-else
                class="w-8 h-8 rounded-full bg-indigo-600 flex items-center justify-center text-white text-xs font-semibold shrink-0"
              >
                {{ post.author.displayName.charAt(0).toUpperCase() }}
              </div>
              <div class="flex-1 min-w-0">
                <div class="flex items-baseline gap-2 mb-0.5">
                  <span class="text-sm font-semibold text-slate-200">
                    {{ post.author.displayName }}
                  </span>
                  <span class="text-xs text-gray-600">
                    {{ new Date(post.createdAt).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }) }}
                  </span>
                </div>
                <p class="text-sm text-slate-300 whitespace-pre-wrap leading-relaxed">
                  <MentionText :content="post.content" :mentions="post.mentions" />
                </p>
              </div>
            </div>
          </div>

          <div v-if="postsHasMore" class="text-center mt-4">
            <button
              type="button"
              :disabled="postsLoading"
              class="text-sm text-indigo-400 hover:text-indigo-300 transition disabled:opacity-50"
              @click="loadMorePosts"
            >
              {{ postsLoading ? 'Loading…' : 'Load more posts' }}
            </button>
          </div>
        </section>
      </template>
    </div>
  </main>
</template>
