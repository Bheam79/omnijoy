/**
 * Auto-suggest composable backing the top-nav search dropdown.
 *
 * Responsibilities:
 *  - Debounce user input by `debounceMs` (default 300ms) before hitting
 *    `GET /api/search/suggest`.
 *  - Flatten the per-category response into a single navigable list so that
 *    arrow-key handling in the consuming component stays trivial.
 *  - Track the highlighted index for keyboard navigation (↑ / ↓ / Enter / Esc).
 *  - Cancel in-flight requests by ignoring stale results (request-id guard).
 *
 * This composable is consumer-agnostic: it does not render anything and does
 * not touch the router. The component using it decides what to do on Enter
 * (navigate to a result) or on "See all" (navigate to /search?q=…).
 */
import { ref, computed, watch, onUnmounted } from 'vue'
import { searchService, type SearchResponse } from '@/services/searchService'
import { useProfileUrl } from '@/composables/useProfileUrl'
import { useCompanyUrl } from '@/composables/useCompanyUrl'

export type SuggestCategory = 'user' | 'post' | 'event' | 'company'

/**
 * A flattened result entry. `routeTo` is the destination path for the entry —
 * the consumer typically passes this directly to `router.push()`.
 */
export interface SuggestItem {
  category:    SuggestCategory
  id:          string
  /** Primary display label (user name, event title, company name, post excerpt). */
  label:       string
  /** Optional secondary text shown beneath the label (e.g. post excerpt, event date). */
  secondary?:  string
  /** Image URL for avatar / cover / logo. */
  imageUrl?:   string
  /** Destination route when the entry is activated. */
  routeTo:     string
}

export interface UseSearchSuggestOptions {
  debounceMs?: number
  /** Minimum query length before any request is fired. */
  minChars?:   number
}

/**
 * Build a navigable, flattened list out of a SearchResponse. Order matches
 * the order users see in the dropdown (People → Posts → Events → Companies).
 */
export function flattenSuggestions(resp: SearchResponse | null): SuggestItem[] {
  if (!resp) return []
  const items: SuggestItem[] = []

  for (const u of resp.users) {
    items.push({
      category:  'user',
      id:        u.id,
      label:     u.displayName,
      secondary: u.bio ?? undefined,
      imageUrl:  u.avatarUrl,
      routeTo:   useProfileUrl(u),
    })
  }
  for (const p of resp.posts) {
    items.push({
      category:  'post',
      id:        p.id,
      label:     truncate(p.content, 60) || 'Untitled post',
      secondary: p.authorDisplayName,
      imageUrl:  p.authorAvatarUrl,
      routeTo:   `/share/posts/${p.id}`,
    })
  }
  for (const e of resp.events) {
    items.push({
      category:  'event',
      id:        e.id,
      label:     e.title,
      secondary: formatEventSecondary(e.startAt, e.location),
      imageUrl:  e.coverImageUrl,
      routeTo:   `/events/${e.id}`,
    })
  }
  for (const c of resp.companies) {
    items.push({
      category:  'company',
      id:        c.id,
      label:     c.name,
      secondary: `${c.followerCount} follower${c.followerCount === 1 ? '' : 's'}`,
      imageUrl:  c.logoUrl,
      routeTo:   useCompanyUrl(c),
    })
  }
  return items
}

function truncate(s: string, n: number): string {
  if (!s) return ''
  return s.length > n ? `${s.slice(0, n - 1)}…` : s
}

function formatEventSecondary(startAt: string, location?: string): string {
  const d = new Date(startAt)
  const date = isNaN(d.getTime())
    ? startAt
    : d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
  return location ? `${date} · ${location}` : date
}

export function useSearchSuggest(options: UseSearchSuggestOptions = {}) {
  const debounceMs = options.debounceMs ?? 300
  const minChars   = options.minChars   ?? 1

  const query        = ref('')
  const response     = ref<SearchResponse | null>(null)
  const loading      = ref(false)
  const error        = ref<string | null>(null)
  const open         = ref(false)
  const highlighted  = ref(-1)

  let debounceTimer: ReturnType<typeof setTimeout> | null = null
  let reqId = 0

  const items = computed<SuggestItem[]>(() => flattenSuggestions(response.value))

  const hasResults = computed(() => items.value.length > 0)

  const groups = computed(() => ({
    users:     response.value?.users     ?? [],
    posts:     response.value?.posts     ?? [],
    events:    response.value?.events    ?? [],
    companies: response.value?.companies ?? [],
  }))

  /** Fire the request immediately (no debounce). Used after tests / on focus. */
  async function fetchNow(q: string) {
    const trimmed = q.trim()
    if (trimmed.length < minChars) {
      response.value    = null
      loading.value     = false
      error.value       = null
      highlighted.value = -1
      return
    }

    const myReq = ++reqId
    loading.value = true
    error.value   = null
    try {
      const resp = await searchService.suggest(trimmed)
      // Drop stale responses (a newer request was issued in the meantime)
      if (myReq !== reqId) return
      response.value    = resp
      highlighted.value = items.value.length > 0 ? 0 : -1
    } catch (e: unknown) {
      if (myReq !== reqId) return
      response.value = null
      error.value    = extractError(e)
    } finally {
      if (myReq === reqId) loading.value = false
    }
  }

  function clearTimer() {
    if (debounceTimer !== null) {
      clearTimeout(debounceTimer)
      debounceTimer = null
    }
  }

  // Debounced fetcher — every change resets the timer.
  watch(query, (q) => {
    clearTimer()
    open.value = q.trim().length >= minChars
    if (q.trim().length < minChars) {
      response.value = null
      return
    }
    debounceTimer = setTimeout(() => {
      void fetchNow(q)
    }, debounceMs)
  })

  // ── Keyboard navigation helpers ───────────────────────────────────────────

  function moveDown() {
    const list = items.value
    if (list.length === 0) {
      highlighted.value = -1
      return
    }
    highlighted.value = (highlighted.value + 1) % list.length
  }

  function moveUp() {
    const list = items.value
    if (list.length === 0) {
      highlighted.value = -1
      return
    }
    highlighted.value = highlighted.value <= 0
      ? list.length - 1
      : highlighted.value - 1
  }

  function selectHighlighted(): SuggestItem | null {
    const list = items.value
    if (highlighted.value < 0 || highlighted.value >= list.length) return null
    return list[highlighted.value]
  }

  function reset() {
    clearTimer()
    query.value       = ''
    response.value    = null
    loading.value     = false
    error.value       = null
    open.value        = false
    highlighted.value = -1
    reqId++
  }

  function close() {
    open.value        = false
    highlighted.value = -1
  }

  onUnmounted(() => {
    clearTimer()
  })

  return {
    // state
    query,
    response,
    loading,
    error,
    open,
    highlighted,
    items,
    hasResults,
    groups,
    // actions
    fetchNow,
    moveDown,
    moveUp,
    selectHighlighted,
    reset,
    close,
  }
}

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const ae = e as { response?: { data?: { error?: string } }; message?: string }
    return ae.response?.data?.error ?? ae.message ?? 'Search failed.'
  }
  return 'Search failed.'
}
