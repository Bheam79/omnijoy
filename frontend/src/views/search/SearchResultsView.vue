<script setup lang="ts">
/**
 * Search results page at `/search?q=…`.
 *
 * Tabs: All / People / Posts / Events / Companies
 *  - "All" shows up to 5 of each category (a card per category) using the
 *    backend's `type=all` mode.
 *  - Each category tab paginates that category independently (Load more
 *    button).
 *  - The URL drives both the query and the active tab via `?q=…&tab=…`
 *    so deep-linking and back/forward both work.
 */
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import {
  searchService,
  type SearchType,
  type SearchResponse,
  type UserSearchResult,
  type PostSearchResult,
  type EventSearchResult,
  type CompanySearchResult,
} from '@/services/searchService'
import { useProfileUrl } from '@/composables/useProfileUrl'
import { useCompanyUrl } from '@/composables/useCompanyUrl'

const route  = useRoute()
const router = useRouter()

type Tab = 'all' | 'users' | 'posts' | 'events' | 'companies'

const TABS: { id: Tab; label: string }[] = [
  { id: 'all',       label: 'All'       },
  { id: 'users',     label: 'People'    },
  { id: 'posts',     label: 'Posts'     },
  { id: 'events',    label: 'Events'    },
  { id: 'companies', label: 'Companies' },
]

const PAGE_SIZE = 20

// ── URL-driven state ──────────────────────────────────────────────────────────

const query     = computed(() => (route.query.q as string | undefined)?.trim() ?? '')
const activeTab = computed<Tab>(() => {
  const t = route.query.tab as string | undefined
  return (TABS.find(x => x.id === t)?.id) ?? 'all'
})

function setTab(tab: Tab) {
  router.replace({
    name:  'search',
    query: { ...route.query, tab },
  })
}

// ── Search state per tab ──────────────────────────────────────────────────────

// "All" mode: a single response covering 5 items per category.
const allResponse  = ref<SearchResponse | null>(null)
const allLoading   = ref(false)
const allError     = ref<string | null>(null)

// Per-tab paginated lists (independent state so switching tabs is cheap).
const users          = ref<UserSearchResult[]>([])
const usersHasMore   = ref(false)
const usersPage      = ref(1)
const usersLoading   = ref(false)
const usersError     = ref<string | null>(null)

const posts          = ref<PostSearchResult[]>([])
const postsHasMore   = ref(false)
const postsPage      = ref(1)
const postsLoading   = ref(false)
const postsError     = ref<string | null>(null)

const events         = ref<EventSearchResult[]>([])
const eventsHasMore  = ref(false)
const eventsPage     = ref(1)
const eventsLoading  = ref(false)
const eventsError    = ref<string | null>(null)

const companies         = ref<CompanySearchResult[]>([])
const companiesHasMore  = ref(false)
const companiesPage     = ref(1)
const companiesLoading  = ref(false)
const companiesError    = ref<string | null>(null)

// ── Loaders ───────────────────────────────────────────────────────────────────

async function loadAll() {
  const q = query.value
  if (!q) { allResponse.value = null; return }
  allLoading.value = true
  allError.value   = null
  try {
    allResponse.value = await searchService.search({ q, type: 'all', pageSize: 5 })
  } catch (e: unknown) {
    allResponse.value = null
    allError.value    = extractError(e)
  } finally {
    allLoading.value = false
  }
}

async function loadCategory(type: SearchType, append = false) {
  const q = query.value
  if (!q) return

  switch (type) {
    case 'users':
      await loadOne(q, 'users', append, users,     usersPage,     usersLoading,     usersError,     usersHasMore,     r => r.users)
      break
    case 'posts':
      await loadOne(q, 'posts', append, posts,     postsPage,     postsLoading,     postsError,     postsHasMore,     r => r.posts)
      break
    case 'events':
      await loadOne(q, 'events', append, events,   eventsPage,    eventsLoading,    eventsError,    eventsHasMore,    r => r.events)
      break
    case 'companies':
      await loadOne(q, 'companies', append, companies, companiesPage, companiesLoading, companiesError, companiesHasMore, r => r.companies)
      break
  }
}

async function loadOne<T>(
  q: string,
  type: SearchType,
  append: boolean,
  items:   { value: T[] },
  pageRef: { value: number },
  loading: { value: boolean },
  errorRef:{ value: string | null },
  hasMore: { value: boolean },
  selector: (resp: SearchResponse) => T[],
) {
  const nextPage = append ? pageRef.value + 1 : 1
  loading.value  = true
  errorRef.value = null
  try {
    const resp = await searchService.search({ q, type, page: nextPage, pageSize: PAGE_SIZE })
    const slice = selector(resp)
    items.value    = append ? [...items.value, ...slice] : slice
    pageRef.value  = nextPage
    hasMore.value  = resp.hasMore
  } catch (e: unknown) {
    errorRef.value = extractError(e)
    if (!append) items.value = []
  } finally {
    loading.value = false
  }
}

function loadingFor(tab: Tab): boolean {
  switch (tab) {
    case 'users':     return usersLoading.value
    case 'posts':     return postsLoading.value
    case 'events':    return eventsLoading.value
    case 'companies': return companiesLoading.value
    default:          return false
  }
}

function hasMoreFor(tab: Tab): boolean {
  switch (tab) {
    case 'users':     return usersHasMore.value     && users.value.length > 0
    case 'posts':     return postsHasMore.value     && posts.value.length > 0
    case 'events':    return eventsHasMore.value    && events.value.length > 0
    case 'companies': return companiesHasMore.value && companies.value.length > 0
    default:          return false
  }
}

function loadActive() {
  if (activeTab.value === 'all') {
    loadAll()
  } else {
    loadCategory(activeTab.value)
  }
}

function loadMore() {
  if (activeTab.value === 'all') return
  loadCategory(activeTab.value, true)
}

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const ae = e as { response?: { data?: { error?: string } }; message?: string }
    return ae.response?.data?.error ?? ae.message ?? 'Search failed.'
  }
  return 'Search failed.'
}

// ── Reactivity: refetch on q / tab changes ────────────────────────────────────

watch(() => [query.value, activeTab.value], () => {
  loadActive()
})

onMounted(loadActive)

// ── Display helpers ───────────────────────────────────────────────────────────

function formatDate(iso: string) {
  const d = new Date(iso)
  if (isNaN(d.getTime())) return iso
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

function tabCount(tab: Tab): number | null {
  // Show how many items are in each tab from the "all" preview, when available
  if (!allResponse.value) return null
  switch (tab) {
    case 'users':     return allResponse.value.users.length
    case 'posts':     return allResponse.value.posts.length
    case 'events':    return allResponse.value.events.length
    case 'companies': return allResponse.value.companies.length
    default:          return null
  }
}

const activeLoading = computed(() => loadingFor(activeTab.value))
const activeHasMore = computed(() => hasMoreFor(activeTab.value))
</script>

<template>
  <div class="space-y-4">
    <!-- Header -->
    <header>
      <h1 class="text-2xl font-bold text-slate-100">
        Search results
      </h1>
      <p v-if="query" class="text-sm text-gray-500 mt-0.5">
        for "<span class="font-medium text-slate-300">{{ query }}</span>"
      </p>
    </header>

    <!-- Empty query state -->
    <div v-if="!query" class="text-center py-16">
      <p class="text-4xl mb-3">🔍</p>
      <p class="font-semibold text-slate-300">Search Omnijoy</p>
      <p class="text-sm text-gray-500 mt-1">
        Type in the search box above to find people, posts, events, and companies.
      </p>
    </div>

    <template v-else>
      <!-- Tabs -->
      <nav class="flex gap-1 bg-slate-900 p-1 rounded-xl text-sm overflow-x-auto">
        <button
          v-for="tab in TABS"
          :key="tab.id"
          :class="[
            'flex-1 min-w-fit py-1.5 px-3 rounded-lg font-medium transition whitespace-nowrap',
            activeTab === tab.id
              ? 'bg-slate-800 shadow text-indigo-400'
              : 'text-gray-500 hover:text-slate-300',
          ]"
          :data-test="`tab-${tab.id}`"
          @click="setTab(tab.id)"
        >
          {{ tab.label }}
          <span v-if="tabCount(tab.id) !== null" class="text-slate-500 text-xs ml-1">
            ({{ tabCount(tab.id) }}{{ allResponse?.hasMore ? '+' : '' }})
          </span>
        </button>
      </nav>

      <!-- ── All tab ───────────────────────────────────────────────────────── -->
      <template v-if="activeTab === 'all'">
        <div v-if="allLoading" class="text-center py-12 text-gray-500 text-sm">Searching…</div>
        <div v-else-if="allError" class="bg-red-950 border border-red-800 rounded-xl p-4 text-red-400 text-sm">
          {{ allError }}
        </div>
        <div
          v-else-if="allResponse && (allResponse.users.length + allResponse.posts.length + allResponse.events.length + allResponse.companies.length) === 0"
          class="text-center py-16"
        >
          <p class="text-4xl mb-3">🤷</p>
          <p class="font-semibold text-slate-300">No results found</p>
          <p class="text-sm text-gray-500 mt-1">Try different keywords.</p>
        </div>

        <template v-else-if="allResponse">
          <!-- People preview -->
          <section v-if="allResponse.users.length > 0" class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700">
            <div class="flex items-center justify-between mb-3">
              <h2 class="font-semibold text-slate-200 text-sm">People</h2>
              <button class="text-xs text-indigo-400 hover:underline font-medium" @click="setTab('users')">See all</button>
            </div>
            <ul class="divide-y divide-slate-700">
              <li v-for="u in allResponse.users" :key="`user-${u.id}`" class="py-2">
                <RouterLink :to="useProfileUrl(u)" class="flex items-center gap-3 group">
                  <img v-if="u.avatarUrl" :src="u.avatarUrl" :alt="u.displayName" class="h-10 w-10 rounded-full object-cover" />
                  <div v-else class="h-10 w-10 rounded-full bg-indigo-900 text-indigo-400 flex items-center justify-center font-bold">
                    {{ u.displayName.charAt(0).toUpperCase() }}
                  </div>
                  <div class="min-w-0 flex-1">
                    <p class="text-sm font-semibold text-slate-100 group-hover:underline truncate">{{ u.displayName }}</p>
                    <p v-if="u.bio" class="text-xs text-gray-500 truncate">{{ u.bio }}</p>
                  </div>
                </RouterLink>
              </li>
            </ul>
          </section>

          <!-- Posts preview -->
          <section v-if="allResponse.posts.length > 0" class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700">
            <div class="flex items-center justify-between mb-3">
              <h2 class="font-semibold text-slate-200 text-sm">Posts</h2>
              <button class="text-xs text-indigo-400 hover:underline font-medium" @click="setTab('posts')">See all</button>
            </div>
            <ul class="divide-y divide-slate-700">
              <li v-for="p in allResponse.posts" :key="`post-${p.id}`" class="py-2">
                <RouterLink :to="`/share/posts/${p.id}`" class="flex items-start gap-3 group">
                  <img v-if="p.authorAvatarUrl" :src="p.authorAvatarUrl" :alt="p.authorDisplayName" class="h-9 w-9 rounded-full object-cover shrink-0" />
                  <div v-else class="h-9 w-9 rounded-full bg-blue-900 text-blue-400 flex items-center justify-center font-bold shrink-0 text-sm">
                    {{ p.authorDisplayName.charAt(0).toUpperCase() }}
                  </div>
                  <div class="min-w-0 flex-1">
                    <p class="text-sm text-slate-100 line-clamp-2 group-hover:underline">{{ p.content }}</p>
                    <p class="text-xs text-gray-500 mt-0.5">{{ p.authorDisplayName }} · {{ formatDate(p.createdAt) }}</p>
                  </div>
                </RouterLink>
              </li>
            </ul>
          </section>

          <!-- Events preview -->
          <section v-if="allResponse.events.length > 0" class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700">
            <div class="flex items-center justify-between mb-3">
              <h2 class="font-semibold text-slate-200 text-sm">Events</h2>
              <button class="text-xs text-indigo-400 hover:underline font-medium" @click="setTab('events')">See all</button>
            </div>
            <ul class="divide-y divide-slate-700">
              <li v-for="e in allResponse.events" :key="`event-${e.id}`" class="py-2">
                <RouterLink :to="`/events/${e.id}`" class="flex items-center gap-3 group">
                  <img v-if="e.coverImageUrl" :src="e.coverImageUrl" :alt="e.title" class="h-10 w-10 rounded-lg object-cover shrink-0" />
                  <div v-else class="h-10 w-10 rounded-lg bg-purple-900 text-purple-400 flex items-center justify-center font-bold shrink-0">📅</div>
                  <div class="min-w-0 flex-1">
                    <p class="text-sm font-semibold text-slate-100 truncate group-hover:underline">{{ e.title }}</p>
                    <p class="text-xs text-gray-500 truncate">
                      {{ formatDate(e.startAt) }}<template v-if="e.location"> · {{ e.location }}</template>
                    </p>
                  </div>
                </RouterLink>
              </li>
            </ul>
          </section>

          <!-- Companies preview -->
          <section v-if="allResponse.companies.length > 0" class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700">
            <div class="flex items-center justify-between mb-3">
              <h2 class="font-semibold text-slate-200 text-sm">Companies</h2>
              <button class="text-xs text-indigo-400 hover:underline font-medium" @click="setTab('companies')">See all</button>
            </div>
            <ul class="divide-y divide-slate-700">
              <li v-for="c in allResponse.companies" :key="`company-${c.id}`" class="py-2">
                <RouterLink :to="useCompanyUrl(c)" class="flex items-center gap-3 group">
                  <img v-if="c.logoUrl" :src="c.logoUrl" :alt="c.name" class="h-10 w-10 rounded-lg object-cover shrink-0" />
                  <div v-else class="h-10 w-10 rounded-lg bg-slate-700 text-slate-300 flex items-center justify-center font-bold shrink-0">
                    {{ c.name.charAt(0).toUpperCase() }}
                  </div>
                  <div class="min-w-0 flex-1">
                    <p class="text-sm font-semibold text-slate-100 truncate group-hover:underline">{{ c.name }}</p>
                    <p class="text-xs text-gray-500 truncate">
                      {{ c.followerCount }} follower{{ c.followerCount === 1 ? '' : 's' }}
                    </p>
                  </div>
                </RouterLink>
              </li>
            </ul>
          </section>
        </template>
      </template>

      <!-- ── People tab ────────────────────────────────────────────────────── -->
      <template v-else-if="activeTab === 'users'">
        <div v-if="usersLoading && users.length === 0" class="text-center py-12 text-gray-500 text-sm">Searching…</div>
        <div v-else-if="usersError" class="bg-red-950 border border-red-800 rounded-xl p-4 text-red-400 text-sm">{{ usersError }}</div>
        <div v-else-if="users.length === 0" class="text-center py-16">
          <p class="text-4xl mb-3">👤</p>
          <p class="font-semibold text-slate-300">No people found</p>
          <p class="text-sm text-gray-500 mt-1">Try different keywords.</p>
        </div>
        <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <RouterLink
            v-for="u in users"
            :key="u.id"
            :to="useProfileUrl(u)"
            class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700 flex items-center gap-3 hover:border-indigo-700 transition-colors"
          >
            <img v-if="u.avatarUrl" :src="u.avatarUrl" :alt="u.displayName" class="w-12 h-12 rounded-full object-cover" />
            <div v-else class="w-12 h-12 rounded-full bg-indigo-900 text-indigo-400 flex items-center justify-center font-bold">
              {{ u.displayName.charAt(0).toUpperCase() }}
            </div>
            <div class="min-w-0 flex-1">
              <p class="font-semibold text-slate-100 text-sm truncate">{{ u.displayName }}</p>
              <p v-if="u.bio" class="text-xs text-gray-500 line-clamp-2">{{ u.bio }}</p>
            </div>
          </RouterLink>
        </div>
      </template>

      <!-- ── Posts tab ─────────────────────────────────────────────────────── -->
      <template v-else-if="activeTab === 'posts'">
        <div v-if="postsLoading && posts.length === 0" class="text-center py-12 text-gray-500 text-sm">Searching…</div>
        <div v-else-if="postsError" class="bg-red-950 border border-red-800 rounded-xl p-4 text-red-400 text-sm">{{ postsError }}</div>
        <div v-else-if="posts.length === 0" class="text-center py-16">
          <p class="text-4xl mb-3">📝</p>
          <p class="font-semibold text-slate-300">No posts found</p>
          <p class="text-sm text-gray-500 mt-1">Try different keywords.</p>
        </div>
        <div v-else class="space-y-3">
          <RouterLink
            v-for="p in posts"
            :key="p.id"
            :to="`/share/posts/${p.id}`"
            class="block bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700 hover:border-indigo-700 transition-colors"
          >
            <div class="flex items-center gap-3 mb-2">
              <img v-if="p.authorAvatarUrl" :src="p.authorAvatarUrl" :alt="p.authorDisplayName" class="w-9 h-9 rounded-full object-cover" />
              <div v-else class="w-9 h-9 rounded-full bg-blue-900 text-blue-400 flex items-center justify-center font-bold text-sm">
                {{ p.authorDisplayName.charAt(0).toUpperCase() }}
              </div>
              <div class="min-w-0">
                <p class="text-sm font-semibold text-slate-100 truncate">{{ p.authorDisplayName }}</p>
                <p class="text-xs text-gray-500">{{ formatDate(p.createdAt) }}</p>
              </div>
            </div>
            <p class="text-sm text-slate-300 line-clamp-3 whitespace-pre-wrap">{{ p.content }}</p>
          </RouterLink>
        </div>
      </template>

      <!-- ── Events tab ────────────────────────────────────────────────────── -->
      <template v-else-if="activeTab === 'events'">
        <div v-if="eventsLoading && events.length === 0" class="text-center py-12 text-gray-500 text-sm">Searching…</div>
        <div v-else-if="eventsError" class="bg-red-950 border border-red-800 rounded-xl p-4 text-red-400 text-sm">{{ eventsError }}</div>
        <div v-else-if="events.length === 0" class="text-center py-16">
          <p class="text-4xl mb-3">📅</p>
          <p class="font-semibold text-slate-300">No events found</p>
          <p class="text-sm text-gray-500 mt-1">Try different keywords.</p>
        </div>
        <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <RouterLink
            v-for="e in events"
            :key="e.id"
            :to="`/events/${e.id}`"
            class="bg-slate-800 rounded-xl overflow-hidden shadow-sm border border-slate-700 hover:border-indigo-700 transition-colors"
          >
            <div v-if="e.coverImageUrl" class="h-28 bg-slate-700 overflow-hidden">
              <img :src="e.coverImageUrl" :alt="e.title" class="w-full h-full object-cover" />
            </div>
            <div v-else class="h-28 bg-gradient-to-br from-purple-400 to-indigo-500 flex items-center justify-center text-4xl">📅</div>
            <div class="p-3">
              <p class="font-semibold text-slate-100 text-sm truncate">{{ e.title }}</p>
              <p class="text-xs text-gray-500 mt-1">
                {{ formatDate(e.startAt) }}<template v-if="e.location"> · {{ e.location }}</template>
              </p>
              <p v-if="e.description" class="text-xs text-gray-500 mt-1 line-clamp-2">{{ e.description }}</p>
            </div>
          </RouterLink>
        </div>
      </template>

      <!-- ── Companies tab ─────────────────────────────────────────────────── -->
      <template v-else-if="activeTab === 'companies'">
        <div v-if="companiesLoading && companies.length === 0" class="text-center py-12 text-gray-500 text-sm">Searching…</div>
        <div v-else-if="companiesError" class="bg-red-950 border border-red-800 rounded-xl p-4 text-red-400 text-sm">{{ companiesError }}</div>
        <div v-else-if="companies.length === 0" class="text-center py-16">
          <p class="text-4xl mb-3">🏢</p>
          <p class="font-semibold text-slate-300">No companies found</p>
          <p class="text-sm text-gray-500 mt-1">Try different keywords.</p>
        </div>
        <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <RouterLink
            v-for="c in companies"
            :key="c.id"
            :to="useCompanyUrl(c)"
            class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700 flex items-center gap-3 hover:border-indigo-700 transition-colors"
          >
            <img v-if="c.logoUrl" :src="c.logoUrl" :alt="c.name" class="w-12 h-12 rounded-lg object-cover" />
            <div v-else class="w-12 h-12 rounded-lg bg-indigo-900 text-indigo-400 flex items-center justify-center font-bold">
              {{ c.name.charAt(0).toUpperCase() }}
            </div>
            <div class="min-w-0 flex-1">
              <p class="font-semibold text-slate-100 text-sm truncate">{{ c.name }}</p>
              <p class="text-xs text-gray-500">
                {{ c.followerCount }} follower{{ c.followerCount === 1 ? '' : 's' }}
              </p>
              <p v-if="c.description" class="text-xs text-gray-500 line-clamp-1 mt-0.5">{{ c.description }}</p>
            </div>
          </RouterLink>
        </div>
      </template>

      <!-- Load more (only category tabs) -->
      <div
        v-if="activeTab !== 'all' && activeHasMore"
        class="text-center pt-2"
      >
        <button
          class="px-6 py-2 text-sm font-medium text-indigo-400 border border-indigo-700 rounded-xl hover:bg-indigo-900/50 transition disabled:opacity-50"
          :disabled="activeLoading"
          data-test="load-more"
          @click="loadMore"
        >
          {{ activeLoading ? 'Loading…' : 'Load more' }}
        </button>
      </div>
    </template>
  </div>
</template>
