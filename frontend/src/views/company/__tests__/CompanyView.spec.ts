import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'

// ── Service mocks ─────────────────────────────────────────────────────────────

const mockCompanyPageService = vi.hoisted(() => ({
  getPage:    vi.fn(),
  getAdmins:  vi.fn(),
  follow:     vi.fn(),
  unfollow:   vi.fn(),
  updatePage: vi.fn(),
  addAdmin:   vi.fn(),
  removeAdmin: vi.fn(),
}))

vi.mock('@/services/companyPageService', () => ({
  companyPageService: mockCompanyPageService,
}))

const mockPostService = vi.hoisted(() => ({
  getFeed: vi.fn(),
}))

vi.mock('@/services/postService', () => ({
  postService: mockPostService,
}))

const mockEventService = vi.hoisted(() => ({
  getEvents: vi.fn(),
}))

vi.mock('@/services/eventService', () => ({
  eventService: mockEventService,
}))

// ── Auth store mock ────────────────────────────────────────────────────────────

const mockAuth = vi.hoisted(() => ({
  user: { id: 'user-owner', displayName: 'Owner', email: 'o@x.com' } as { id: string; displayName: string; email: string } | null,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuth,
}))

// ── SlugPicker and PlacePicker stubs ──────────────────────────────────────────

vi.mock('@/components/shared/SlugPicker.vue', () => ({
  default: {
    name:  'SlugPicker',
    props: ['initialSlug', 'scope', 'targetId'],
    emits: ['updated'],
    template: '<div data-testid="slug-picker-stub" />',
  },
}))

vi.mock('@/components/shared/PlacePicker.vue', () => ({
  default: {
    name:     'PlacePicker',
    props:    ['modelValue', 'mode', 'label', 'placeholder'],
    emits:    ['update:modelValue'],
    template: '<div data-testid="place-picker-stub" />',
  },
}))

// Import after mocks
import CompanyView from '../CompanyView.vue'
import type { CompanyPageDto, AdminsResult } from '@/services/companyPageService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makePage(overrides: Partial<CompanyPageDto> = {}): CompanyPageDto {
  return {
    id:            'company-1',
    name:          'Acme Corp',
    description:   'We build great things.',
    followerCount: 42,
    isFollowing:   false,
    createdBy:     { id: 'user-owner', displayName: 'Owner' },
    createdAt:     '2024-01-01T00:00:00Z',
    ...overrides,
  }
}

const emptyAdmins: AdminsResult = {
  admins: [{ userId: 'user-owner', displayName: 'Owner', role: 'Owner' }],
}

const emptyFeed = { items: [], hasMore: false }

/** Feed item that matches company-1 so it appears in the posts tab. */
function makePostFeedItem(postId: string) {
  return {
    itemType: 'Post',
    post: {
      id:            postId,
      author:        { id: 'u1', displayName: 'Alice' },
      content:       'Hello from Acme!',
      postType:      'Text' as const,
      privacy:       'Everyone' as const,
      media:         [],
      companyPageId: 'company-1',
      createdAt:     '2026-01-01T00:00:00Z',
      updatedAt:     '2026-01-01T00:00:00Z',
    },
  }
}

// ── Router + mount helper ─────────────────────────────────────────────────────

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/company/:id', name: 'company', component: CompanyView },
      { path: '/profile/:userId', name: 'profile', component: { template: '<div/>' } },
    ],
  })
}

let router: ReturnType<typeof makeRouter>

const PostCardStub  = { name: 'PostCard',  props: ['post'],  template: '<div data-testid="post-card-stub" />' }
const PostComposerStub = { name: 'PostComposer', template: '<div />' }
const EventCardStub = { name: 'EventCard', props: ['event'], template: '<div />' }
const EventCreateModalStub = { name: 'EventCreateModal', template: '<div />' }

const globalStubs = {
  PostCard:         PostCardStub,
  PostComposer:     PostComposerStub,
  EventCard:        EventCardStub,
  EventCreateModal: EventCreateModalStub,
  // Teleport renders its slot inline so the modal h2/inputs are queryable
  Teleport: { template: '<div><slot /></div>' },
}

/** feedItems defaults to empty; pass items to test posts rendering. */
async function mountView(
  pageOverrides: Partial<CompanyPageDto> = {},
  feedItems: ReturnType<typeof makePostFeedItem>[] = [],
) {
  router = makeRouter()
  await router.push('/company/company-1')

  mockCompanyPageService.getPage.mockResolvedValue(makePage(pageOverrides))
  mockCompanyPageService.getAdmins.mockResolvedValue({ ...emptyAdmins })
  mockPostService.getFeed.mockResolvedValue({ items: feedItems, hasMore: false })
  mockEventService.getEvents.mockResolvedValue({ items: [], hasMore: false, page: 1, pageSize: 20 })

  const wrapper = mount(CompanyView, {
    global: {
      plugins: [createPinia(), router],
      stubs:   globalStubs,
    },
  })
  await flushPromises()
  return wrapper
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('CompanyView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockAuth.user = { id: 'user-owner', displayName: 'Owner', email: 'o@x.com' }
  })

  // ── Fetch on mount ────────────────────────────────────────────────────────

  it('fetches the company page with the id from the route on mount', async () => {
    await mountView()

    expect(mockCompanyPageService.getPage).toHaveBeenCalledWith('company-1')
  })

  it('also fetches admins on mount', async () => {
    await mountView()

    expect(mockCompanyPageService.getAdmins).toHaveBeenCalledWith('company-1')
  })

  it('also fetches the feed on mount to derive company posts', async () => {
    await mountView()

    expect(mockPostService.getFeed).toHaveBeenCalledWith(1, 50)
  })

  // ── Loading skeleton ──────────────────────────────────────────────────────

  it('shows a loading skeleton while the data is being fetched', () => {
    mockCompanyPageService.getPage.mockReturnValue(new Promise(() => { /* never resolves */ }))
    mockCompanyPageService.getAdmins.mockReturnValue(new Promise(() => { /* never resolves */ }))
    mockPostService.getFeed.mockReturnValue(new Promise(() => { /* never resolves */ }))
    router = makeRouter()
    router.push('/company/company-1')

    const wrapper = mount(CompanyView, {
      global: {
        plugins: [createPinia(), router],
        stubs:   globalStubs,
      },
    })

    expect(wrapper.find('.animate-pulse').exists()).toBe(true)
  })

  it('hides the loading skeleton once data is loaded', async () => {
    const wrapper = await mountView()

    expect(wrapper.find('.animate-pulse').exists()).toBe(false)
  })

  // ── Renders company data ──────────────────────────────────────────────────

  it('renders the company name', async () => {
    const wrapper = await mountView()

    expect(wrapper.find('h1').text()).toBe('Acme Corp')
  })

  it('renders the follower count', async () => {
    const wrapper = await mountView({ followerCount: 42 })

    expect(wrapper.text()).toContain('42')
    expect(wrapper.text()).toContain('follower')
  })

  it('renders the logo image when logoUrl is set', async () => {
    const wrapper = await mountView({ logoUrl: 'https://cdn.example.com/logo.png' })

    const imgs = wrapper.findAll('img')
    const logo = imgs.find(img => img.attributes('src') === 'https://cdn.example.com/logo.png')
    expect(logo).toBeDefined()
  })

  it('renders the first-letter avatar when no logo is set', async () => {
    const wrapper = await mountView({ logoUrl: undefined })

    // The fallback avatar shows the first letter of the company name
    expect(wrapper.text()).toContain('A') // "Acme Corp"[0].toUpperCase()
  })

  // ── Follow / Unfollow ─────────────────────────────────────────────────────

  it('shows the Follow button when not following', async () => {
    const wrapper = await mountView({ isFollowing: false })

    const buttons = wrapper.findAll('button')
    expect(buttons.some(b => b.text() === 'Follow')).toBe(true)
  })

  it('shows the Unfollow button when already following', async () => {
    const wrapper = await mountView({ isFollowing: true })

    const buttons = wrapper.findAll('button')
    expect(buttons.some(b => b.text() === 'Unfollow')).toBe(true)
  })

  it('calls companyPageService.follow when Follow is clicked', async () => {
    mockCompanyPageService.follow.mockResolvedValue(makePage({ isFollowing: true }))
    const wrapper = await mountView({ isFollowing: false })

    const followBtn = wrapper.findAll('button').find(b => b.text() === 'Follow')
    await followBtn!.trigger('click')
    await flushPromises()

    expect(mockCompanyPageService.follow).toHaveBeenCalledWith('company-1')
  })

  it('calls companyPageService.unfollow when Unfollow is clicked', async () => {
    mockCompanyPageService.unfollow.mockResolvedValue(makePage({ isFollowing: false }))
    const wrapper = await mountView({ isFollowing: true })

    const unfollowBtn = wrapper.findAll('button').find(b => b.text() === 'Unfollow')
    await unfollowBtn!.trigger('click')
    await flushPromises()

    expect(mockCompanyPageService.unfollow).toHaveBeenCalledWith('company-1')
  })

  // ── Posts tab (default) ───────────────────────────────────────────────────

  it('shows "No posts yet." on the posts tab when there are no posts', async () => {
    const wrapper = await mountView()

    expect(wrapper.text()).toContain('No posts yet.')
  })

  it('renders a PostCard stub for each matching post', async () => {
    const wrapper = await mountView({}, [makePostFeedItem('p1'), makePostFeedItem('p2')])

    const cards = wrapper.findAll('[data-testid="post-card-stub"]')
    expect(cards).toHaveLength(2)
  })

  it('does not render posts from other company pages', async () => {
    const otherCompanyItem = {
      itemType: 'Post',
      post: {
        id:            'p-other',
        author:        { id: 'u2', displayName: 'Bob' },
        content:       'Other company post',
        postType:      'Text' as const,
        privacy:       'Everyone' as const,
        media:         [],
        companyPageId: 'OTHER-COMPANY',
        createdAt:     '2026-01-01T00:00:00Z',
        updatedAt:     '2026-01-01T00:00:00Z',
      },
    }
    const wrapper = await mountView({}, [makePostFeedItem('p1'), otherCompanyItem])

    const cards = wrapper.findAll('[data-testid="post-card-stub"]')
    expect(cards).toHaveLength(1)
  })

  // ── Admin actions ─────────────────────────────────────────────────────────

  it('shows the Edit Page button for an Owner', async () => {
    const wrapper = await mountView({ myRole: 'Owner' })

    expect(wrapper.text()).toContain('Edit Page')
  })

  it('shows the Edit Page button for an Admin', async () => {
    const wrapper = await mountView({ myRole: 'Admin' })

    expect(wrapper.text()).toContain('Edit Page')
  })

  it('does NOT show the Edit Page button for a regular follower', async () => {
    const wrapper = await mountView({ myRole: undefined, isFollowing: true })

    expect(wrapper.text()).not.toContain('Edit Page')
  })

  it('shows the role badge for Owners', async () => {
    const wrapper = await mountView({ myRole: 'Owner' })

    expect(wrapper.text()).toContain('Owner')
  })

  it('shows the "Act as" / company mode button for members with a role', async () => {
    const wrapper = await mountView({ myRole: 'Editor' })

    expect(wrapper.text()).toContain('Act as Acme Corp')
  })

  it('does NOT show the "Act as" button for non-members', async () => {
    const wrapper = await mountView({ myRole: undefined })

    expect(wrapper.text()).not.toContain('Act as Acme Corp')
  })

  // ── About tab ─────────────────────────────────────────────────────────────

  it('renders the description on the About tab', async () => {
    const wrapper = await mountView({ description: 'We build great things.' })

    const aboutBtn = wrapper.findAll('button').find(b => b.text().toLowerCase() === 'about')
    await aboutBtn!.trigger('click')

    expect(wrapper.text()).toContain('We build great things.')
  })

  it('shows "No description." when description is absent on the About tab', async () => {
    const wrapper = await mountView({ description: undefined })

    const aboutBtn = wrapper.findAll('button').find(b => b.text().toLowerCase() === 'about')
    await aboutBtn!.trigger('click')

    expect(wrapper.text()).toContain('No description.')
  })

  it('renders the address on the About tab when addressText is set', async () => {
    const wrapper = await mountView({ addressText: '123 Main St, Oslo' })

    const aboutBtn = wrapper.findAll('button').find(b => b.text().toLowerCase() === 'about')
    await aboutBtn!.trigger('click')

    const addressEl = wrapper.find('[data-testid="company-address"]')
    expect(addressEl.exists()).toBe(true)
    expect(addressEl.text()).toContain('123 Main St, Oslo')
  })

  it('does not render the address block on the About tab when addressText is absent', async () => {
    const wrapper = await mountView({ addressText: null })

    const aboutBtn = wrapper.findAll('button').find(b => b.text().toLowerCase() === 'about')
    await aboutBtn!.trigger('click')

    expect(wrapper.find('[data-testid="company-address"]').exists()).toBe(false)
  })

  // ── Tab navigation ────────────────────────────────────────────────────────

  it('renders Posts / Events / About tab buttons', async () => {
    const wrapper = await mountView()

    const tabBtns = wrapper.findAll('button').map(b => b.text().toLowerCase())
    expect(tabBtns.some(t => t === 'posts')).toBe(true)
    expect(tabBtns.some(t => t === 'events')).toBe(true)
    expect(tabBtns.some(t => t === 'about')).toBe(true)
  })

  // ── Error / not-found states ──────────────────────────────────────────────

  it('shows an error message when the page fails to load', async () => {
    mockCompanyPageService.getPage.mockRejectedValue({
      response: { status: 500, data: { error: 'Server exploded' } },
    })
    router = makeRouter()
    await router.push('/company/company-1')
    mockCompanyPageService.getAdmins.mockResolvedValue({ ...emptyAdmins })
    mockPostService.getFeed.mockResolvedValue({ ...emptyFeed })

    const wrapper = mount(CompanyView, {
      global: {
        plugins: [createPinia(), router],
        stubs:   globalStubs,
      },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('Server exploded')
  })

  it('shows the fallback error message on a generic failure', async () => {
    mockCompanyPageService.getPage.mockRejectedValue(new Error('Network error'))
    router = makeRouter()
    await router.push('/company/company-1')
    mockCompanyPageService.getAdmins.mockResolvedValue({ ...emptyAdmins })
    mockPostService.getFeed.mockResolvedValue({ ...emptyFeed })

    const wrapper = mount(CompanyView, {
      global: {
        plugins: [createPinia(), router],
        stubs:   globalStubs,
      },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('Network error')
  })

  // ── Edit modal ────────────────────────────────────────────────────────────

  it('opens the edit modal when Edit Page is clicked by an admin', async () => {
    const wrapper = await mountView({ myRole: 'Owner' })

    const editBtn = wrapper.findAll('button').find(b => b.text() === 'Edit Page')
    await editBtn!.trigger('click')

    // The edit modal renders "Edit Page" as an h2 heading
    expect(wrapper.find('h2').text()).toBe('Edit Page')
  })

  it('shows the company name pre-filled in the edit form', async () => {
    const wrapper = await mountView({ myRole: 'Owner', name: 'Acme Corp' })

    const editBtn = wrapper.findAll('button').find(b => b.text() === 'Edit Page')
    await editBtn!.trigger('click')

    const nameInput = wrapper.find('input[type="text"][maxlength="256"]')
    expect((nameInput.element as HTMLInputElement).value).toBe('Acme Corp')
  })

  it('calls companyPageService.updatePage on save', async () => {
    const updatedPage = makePage({ name: 'Acme Corp Updated', myRole: 'Owner' })
    mockCompanyPageService.updatePage.mockResolvedValue(updatedPage)

    const wrapper = await mountView({ myRole: 'Owner' })

    const editBtn = wrapper.findAll('button').find(b => b.text() === 'Edit Page')
    await editBtn!.trigger('click')

    const saveBtn = wrapper.findAll('button').find(b => b.text() === 'Save Changes')
    await saveBtn!.trigger('click')
    await flushPromises()

    expect(mockCompanyPageService.updatePage).toHaveBeenCalledWith(
      'company-1',
      expect.objectContaining({}),
    )
  })
})
