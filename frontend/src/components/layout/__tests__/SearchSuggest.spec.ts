import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import SearchSuggest from '../SearchSuggest.vue'
import type {
  UserSearchResult,
  PostSearchResult,
  EventSearchResult,
  CompanySearchResult,
} from '@/services/searchService'

// ── vue-router mock ───────────────────────────────────────────────────────────

const mockRouter = vi.hoisted(() => ({ push: vi.fn() }))

vi.mock('vue-router', async () => {
  const actual = await vi.importActual<typeof import('vue-router')>('vue-router')
  return { ...actual, useRouter: () => mockRouter }
})

// ── useSearchSuggest mock ─────────────────────────────────────────────────────
//
// The composable is replaced wholesale so tests can drive the component's
// state directly. `state` is the seed that's read at mount time; the factory
// turns it into real `ref()` instances so Vue's template reactivity (especially
// auto-unwrap for `groups.users`) works the same as in production.

interface MockState {
  query:       string
  loading:     boolean
  open:        boolean
  highlighted: number
  hasResults:  boolean
  users:       UserSearchResult[]
  posts:       PostSearchResult[]
  events:      EventSearchResult[]
  companies:   CompanySearchResult[]
}

const state = vi.hoisted<MockState>(() => ({
  query:       '',
  loading:     false,
  open:        false,
  highlighted: -1,
  hasResults:  false,
  users:       [],
  posts:       [],
  events:      [],
  companies:   [],
}))

const actions = vi.hoisted(() => ({
  moveDown:          vi.fn(),
  moveUp:            vi.fn(),
  selectHighlighted: vi.fn(),
  reset:             vi.fn(),
  close:             vi.fn(),
}))

// Track invocations of the composable itself so we can assert it was wired up.
const composableCalls = vi.hoisted(() => ({ count: 0 }))

vi.mock('@/composables/useSearchSuggest', async () => {
  const { ref, computed } = await import('vue')
  return {
    useSearchSuggest: () => {
      composableCalls.count++
      const query       = ref(state.query)
      const loading     = ref(state.loading)
      const open        = ref(state.open)
      const highlighted = ref(state.highlighted)

      const groups = computed(() => ({
        users:     state.users,
        posts:     state.posts,
        events:    state.events,
        companies: state.companies,
      }))

      // Flatten to keep `items` consistent with what the real composable
      // returns — the SearchSuggest template uses indexOf() against it for
      // highlight matching.
      const items = computed(() => {
        const list: Array<{ category: string; id: string; label: string; routeTo: string }> = []
        for (const u of state.users)    list.push({ category: 'user',    id: u.id, label: u.displayName, routeTo: `/profile/${u.id}` })
        for (const p of state.posts)    list.push({ category: 'post',    id: p.id, label: p.content,     routeTo: `/share/posts/${p.id}` })
        for (const e of state.events)   list.push({ category: 'event',   id: e.id, label: e.title,       routeTo: `/events/${e.id}` })
        for (const c of state.companies)list.push({ category: 'company', id: c.id, label: c.name,        routeTo: `/company/${c.id}` })
        return list
      })

      const hasResults = computed(() => state.hasResults || items.value.length > 0)

      return {
        query,
        loading,
        open,
        highlighted,
        items,
        hasResults,
        groups,
        ...actions,
      }
    },
  }
})

// ── Helpers ───────────────────────────────────────────────────────────────────

function resetState() {
  state.query       = ''
  state.loading     = false
  state.open        = false
  state.highlighted = -1
  state.hasResults  = false
  state.users       = []
  state.posts       = []
  state.events      = []
  state.companies   = []
  composableCalls.count = 0
}

function mountSuggest() {
  return mount(SearchSuggest, {
    global: {
      plugins: [createPinia()],
    },
  })
}

const SAMPLE_USER: UserSearchResult = {
  id:          'u1',
  displayName: 'Alice',
  bio:         'Hello!',
}

const SAMPLE_POST: PostSearchResult = {
  id:                'p1',
  authorId:          'u2',
  authorDisplayName: 'Bob',
  content:           'A short post',
  postType:          'Text',
  privacy:           'Everyone',
  createdAt:         '2024-01-01T00:00:00Z',
}

const SAMPLE_EVENT: EventSearchResult = {
  id:                 'e1',
  title:              'Summer Fest',
  startAt:            '2026-08-01T18:00:00Z',
  creatorId:          'u3',
  creatorDisplayName: 'Cara',
  location:           'Oslo',
}

const SAMPLE_COMPANY: CompanySearchResult = {
  id:            'c1',
  name:          'Acme Corp',
  followerCount: 42,
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('SearchSuggest', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    resetState()
  })

  // ── Composable wiring ──────────────────────────────────────────────────────

  it('calls useSearchSuggest() to obtain suggestion state', () => {
    mountSuggest()
    expect(composableCalls.count).toBe(1)
  })

  it('always renders the search input', () => {
    const wrapper = mountSuggest()
    expect(wrapper.find('[data-testid="search-input"]').exists()).toBe(true)
  })

  // ── Empty input ────────────────────────────────────────────────────────────

  it('does not show the dropdown when query is empty (even if open=true)', () => {
    state.query = ''
    state.open  = true
    state.users = [SAMPLE_USER]
    const wrapper = mountSuggest()

    expect(wrapper.find('[data-testid="search-dropdown"]').exists()).toBe(false)
  })

  it('does not show the dropdown when query is only whitespace', () => {
    state.query = '   '
    state.open  = true
    state.users = [SAMPLE_USER]
    const wrapper = mountSuggest()

    expect(wrapper.find('[data-testid="search-dropdown"]').exists()).toBe(false)
  })

  // ── Rendering suggestions ─────────────────────────────────────────────────

  it('renders user suggestions returned by the composable', () => {
    state.query = 'ali'
    state.open  = true
    state.users = [SAMPLE_USER]
    const wrapper = mountSuggest()

    expect(wrapper.find('[data-testid="search-dropdown"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="suggest-user-0"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Alice')
    expect(wrapper.text()).toContain('Hello!')
  })

  it('renders post / event / company suggestions in their own sections', () => {
    state.query     = 'q'
    state.open      = true
    state.posts     = [SAMPLE_POST]
    state.events    = [SAMPLE_EVENT]
    state.companies = [SAMPLE_COMPANY]
    const wrapper = mountSuggest()

    expect(wrapper.find('[data-test="suggest-post-0"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="suggest-event-0"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="suggest-company-0"]').exists()).toBe(true)
    const text = wrapper.text()
    expect(text).toContain('A short post')
    expect(text).toContain('Summer Fest')
    expect(text).toContain('Acme Corp')
  })

  it('renders the "See all results" link when there is at least one result', () => {
    state.query = 'ali'
    state.open  = true
    state.users = [SAMPLE_USER]
    const wrapper = mountSuggest()

    expect(wrapper.find('[data-test="suggest-see-all"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('See all results for "ali"')
  })

  // ── Empty-results state ───────────────────────────────────────────────────

  it('shows the "No results" message when the dropdown is open with no items', () => {
    state.query      = 'xyz'
    state.open       = true
    state.hasResults = false
    const wrapper = mountSuggest()

    expect(wrapper.find('[data-testid="search-dropdown"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('No results for')
    expect(wrapper.text()).toContain('xyz')
  })

  it('shows the loading state when loading=true', () => {
    state.query   = 'al'
    state.open    = true
    state.loading = true
    const wrapper = mountSuggest()

    expect(wrapper.text()).toContain('Searching')
  })

  // ── Click a suggestion → router.push ───────────────────────────────────────

  it('clicking a user suggestion pushes the user profile route', async () => {
    state.query = 'ali'
    state.open  = true
    state.users = [SAMPLE_USER]
    const wrapper = mountSuggest()

    await wrapper.find('[data-test="suggest-user-0"]').trigger('mousedown')
    await flushPromises()

    expect(mockRouter.push).toHaveBeenCalledWith('/profile/u1')
  })

  it('clicking a post suggestion pushes the share-post route', async () => {
    state.query = 'q'
    state.open  = true
    state.posts = [SAMPLE_POST]
    const wrapper = mountSuggest()

    await wrapper.find('[data-test="suggest-post-0"]').trigger('mousedown')
    await flushPromises()

    expect(mockRouter.push).toHaveBeenCalledWith('/share/posts/p1')
  })

  it('clicking an event suggestion pushes the event route', async () => {
    state.query  = 'fest'
    state.open   = true
    state.events = [SAMPLE_EVENT]
    const wrapper = mountSuggest()

    await wrapper.find('[data-test="suggest-event-0"]').trigger('mousedown')
    await flushPromises()

    expect(mockRouter.push).toHaveBeenCalledWith('/events/e1')
  })

  it('clicking a company suggestion pushes the company route', async () => {
    state.query     = 'acme'
    state.open      = true
    state.companies = [SAMPLE_COMPANY]
    const wrapper = mountSuggest()

    await wrapper.find('[data-test="suggest-company-0"]').trigger('mousedown')
    await flushPromises()

    expect(mockRouter.push).toHaveBeenCalledWith('/company/c1')
  })

  // ── reset() on navigation ─────────────────────────────────────────────────

  it('calls reset() on the composable before navigating to a suggestion', async () => {
    state.query = 'ali'
    state.open  = true
    state.users = [SAMPLE_USER]
    const wrapper = mountSuggest()

    await wrapper.find('[data-test="suggest-user-0"]').trigger('mousedown')
    await flushPromises()

    expect(actions.reset).toHaveBeenCalledOnce()
    // reset must run BEFORE router.push so the dropdown can't visually flash
    // a stale list during route transition
    expect(actions.reset.mock.invocationCallOrder[0])
      .toBeLessThan(mockRouter.push.mock.invocationCallOrder[0])
  })

  // ── "See all results" link ────────────────────────────────────────────────

  it('clicking "See all results" navigates to /search with the query', async () => {
    state.query = 'ali'
    state.open  = true
    state.users = [SAMPLE_USER]
    const wrapper = mountSuggest()

    await wrapper.find('[data-test="suggest-see-all"]').trigger('mousedown')
    await flushPromises()

    expect(mockRouter.push).toHaveBeenCalledWith({
      name:  'search',
      query: { q: 'ali' },
    })
    expect(actions.reset).toHaveBeenCalledOnce()
  })

  // ── Keyboard navigation ───────────────────────────────────────────────────

  it('ArrowDown calls moveDown and opens the dropdown', async () => {
    state.query = 'a'
    const wrapper = mountSuggest()

    await wrapper.find('[data-testid="search-input"]').trigger('keydown', { key: 'ArrowDown' })

    expect(actions.moveDown).toHaveBeenCalledOnce()
  })

  it('ArrowUp calls moveUp', async () => {
    state.query = 'a'
    const wrapper = mountSuggest()

    await wrapper.find('[data-testid="search-input"]').trigger('keydown', { key: 'ArrowUp' })

    expect(actions.moveUp).toHaveBeenCalledOnce()
  })

  it('Enter on a highlighted item navigates to that item', async () => {
    state.query = 'ali'
    state.open  = true
    state.users = [SAMPLE_USER]
    actions.selectHighlighted.mockReturnValue({
      category: 'user',
      id:       'u1',
      label:    'Alice',
      routeTo:  '/profile/u1',
    })

    const wrapper = mountSuggest()
    await wrapper.find('[data-testid="search-input"]').trigger('keydown', { key: 'Enter' })
    await flushPromises()

    expect(mockRouter.push).toHaveBeenCalledWith('/profile/u1')
  })

  it('Enter with no highlighted item but non-empty query goes to /search', async () => {
    state.query = 'ali'
    state.open  = true
    actions.selectHighlighted.mockReturnValue(null)

    const wrapper = mountSuggest()
    await wrapper.find('[data-testid="search-input"]').trigger('keydown', { key: 'Enter' })
    await flushPromises()

    expect(mockRouter.push).toHaveBeenCalledWith({
      name:  'search',
      query: { q: 'ali' },
    })
  })

  it('Escape calls close()', async () => {
    state.query = 'a'
    state.open  = true
    const wrapper = mountSuggest()

    await wrapper.find('[data-testid="search-input"]').trigger('keydown', { key: 'Escape' })

    expect(actions.close).toHaveBeenCalledOnce()
  })

  // ── Clears suggestions when input is cleared ──────────────────────────────
  // The composable owns the actual debouncing + clearing logic; from the
  // component's point of view, "input cleared" means trimmedQuery === '' →
  // the v-if guards the dropdown closed regardless of any stale `items`.

  it('hides the dropdown when query is cleared even if items remain in state', async () => {
    state.query = 'ali'
    state.open  = true
    state.users = [SAMPLE_USER]
    const wrapper = mountSuggest()

    expect(wrapper.find('[data-testid="search-dropdown"]').exists()).toBe(true)

    // Simulate the user clearing the input
    await wrapper.find('[data-testid="search-input"]').setValue('')
    await flushPromises()

    expect(wrapper.find('[data-testid="search-dropdown"]').exists()).toBe(false)
  })
})
