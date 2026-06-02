import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── Service mock ──────────────────────────────────────────────────────────────

const mockCompanyPageService = vi.hoisted(() => ({
  getPages:   vi.fn(),
  createPage: vi.fn(),
  follow:     vi.fn(),
  unfollow:   vi.fn(),
}))

vi.mock('@/services/companyPageService', () => ({
  companyPageService: mockCompanyPageService,
}))

// ── PlacePicker stub ──────────────────────────────────────────────────────────

vi.mock('@/components/shared/PlacePicker.vue', () => ({
  default: {
    name:     'PlacePicker',
    props:    ['modelValue', 'mode', 'label', 'placeholder'],
    emits:    ['update:modelValue'],
    template: '<div data-testid="place-picker-stub" />',
  },
}))

// Stub URL APIs used during file preview
vi.stubGlobal('URL', {
  createObjectURL: vi.fn(() => 'blob:mock-url'),
  revokeObjectURL: vi.fn(),
})

// Import after mocks
import CompanyListView from '../CompanyListView.vue'
import type { CompanyPageDto } from '@/services/companyPageService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makePage(id: string, name: string, overrides: Partial<CompanyPageDto> = {}): CompanyPageDto {
  return {
    id,
    name,
    followerCount: 10,
    isFollowing:   false,
    createdBy:     { id: 'u1', displayName: 'Alice' },
    createdAt:     '2024-01-01T00:00:00Z',
    ...overrides,
  }
}

const emptyResult = { items: [] as CompanyPageDto[], page: 1, pageSize: 20, hasMore: false }

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  return mount(CompanyListView, {
    global: {
      plugins: [createPinia()],
      stubs:   {
        RouterLink: RouterLinkStub,
        // Teleport renders its slot inline so we can query modal contents
        Teleport: { template: '<div><slot /></div>' },
      },
    },
  })
}

// ── Helpers ───────────────────────────────────────────────────────────────────

/**
 * Both the header "Create Page" button and the modal footer submit button have
 * the same trimmed text ("Create Page"). The modal submit button is the LAST
 * one in DOM order (Teleport stub appends content after the main template),
 * so we always take the last match.
 */
type WrapperType = ReturnType<typeof mount>
function findModalSubmitBtn(wrapper: WrapperType) {
  const matches = wrapper.findAll('button').filter(b => b.text() === 'Create Page')
  return matches[matches.length - 1]
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('CompanyListView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockCompanyPageService.getPages.mockResolvedValue({ ...emptyResult })
    mockCompanyPageService.createPage.mockResolvedValue(makePage('new-1', 'Brand New'))
  })

  // ── Load on mount ─────────────────────────────────────────────────────────

  it('calls companyPageService.getPages on mount', async () => {
    mountView()
    await flushPromises()

    expect(mockCompanyPageService.getPages).toHaveBeenCalledOnce()
  })

  it('calls getPages with mine=false by default (All Pages tab)', async () => {
    mountView()
    await flushPromises()

    const [params] = mockCompanyPageService.getPages.mock.calls[0]
    expect(params.mine).toBe(false)
  })

  // ── Loading skeleton ──────────────────────────────────────────────────────

  it('shows the loading skeleton while loading', () => {
    mockCompanyPageService.getPages.mockReturnValue(new Promise(() => { /* never resolves */ }))
    const wrapper = mountView()

    expect(wrapper.find('.animate-pulse').exists()).toBe(true)
  })

  it('hides the loading skeleton after data loads', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('.animate-pulse').exists()).toBe(false)
  })

  // ── Company cards ─────────────────────────────────────────────────────────

  it('renders a company-card for each page in the list', async () => {
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [
        makePage('c1', 'Acme Corp'),
        makePage('c2', 'Globex'),
        makePage('c3', 'Initech'),
      ],
      page: 1, pageSize: 20, hasMore: false,
    })
    const wrapper = mountView()
    await flushPromises()

    const cards = wrapper.findAll('[data-testid="company-card"]')
    expect(cards).toHaveLength(3)
  })

  it('renders company names in the cards', async () => {
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [makePage('c1', 'Acme Corp'), makePage('c2', 'Globex')],
      page: 1, pageSize: 20, hasMore: false,
    })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Acme Corp')
    expect(wrapper.text()).toContain('Globex')
  })

  it('renders follower counts in the cards', async () => {
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [makePage('c1', 'Acme Corp', { followerCount: 99 })],
      page: 1, pageSize: 20, hasMore: false,
    })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('99')
    expect(wrapper.text()).toContain('follower')
  })

  it('renders company descriptions when present', async () => {
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [makePage('c1', 'Acme Corp', { description: 'We make stuff.' })],
      page: 1, pageSize: 20, hasMore: false,
    })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('We make stuff.')
  })

  // ── Empty state ───────────────────────────────────────────────────────────

  it('shows the empty state when no pages are returned', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('No company pages')
  })

  it('shows the "My Pages" empty label on the Mine tab', async () => {
    const wrapper = mountView()
    await flushPromises()

    const mineBtn = wrapper.findAll('button').find(b => b.text() === 'My Pages')
    await mineBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('No pages yet')
  })

  // ── Create Page button ────────────────────────────────────────────────────

  it('renders the Create Page button', () => {
    const wrapper = mountView()

    const btn = wrapper.find('[data-testid="create-company-button"]')
    expect(btn.exists()).toBe(true)
    expect(btn.text()).toContain('Create Page')
  })

  it('does not show the create modal by default', () => {
    const wrapper = mountView()

    expect(wrapper.find('h2').exists()).toBe(false)
  })

  it('opens the create modal when Create Page is clicked', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="create-company-button"]').trigger('click')

    expect(wrapper.find('h2').text()).toBe('Create Company Page')
  })

  it('also opens the modal from the empty-state "Create a Page" button', async () => {
    const wrapper = mountView()
    await flushPromises()

    const emptyCreateBtn = wrapper.findAll('button').find(b => b.text() === 'Create a Page')
    expect(emptyCreateBtn).toBeDefined()

    await emptyCreateBtn!.trigger('click')

    expect(wrapper.find('h2').text()).toBe('Create Company Page')
  })

  // ── Tabs ──────────────────────────────────────────────────────────────────

  it('renders All Pages and My Pages tabs', () => {
    const wrapper = mountView()

    expect(wrapper.text()).toContain('All Pages')
    expect(wrapper.text()).toContain('My Pages')
  })

  it('calls getPages with mine=true when My Pages tab is clicked', async () => {
    const wrapper = mountView()
    await flushPromises()
    mockCompanyPageService.getPages.mockClear()

    const mineBtn = wrapper.findAll('button').find(b => b.text() === 'My Pages')
    await mineBtn!.trigger('click')
    await flushPromises()

    expect(mockCompanyPageService.getPages).toHaveBeenCalledOnce()
    const [params] = mockCompanyPageService.getPages.mock.calls[0]
    expect(params.mine).toBe(true)
  })

  it('calls getPages with mine=false when All Pages tab is clicked', async () => {
    const wrapper = mountView()
    await flushPromises()

    // Switch to Mine first
    const mineBtn = wrapper.findAll('button').find(b => b.text() === 'My Pages')
    await mineBtn!.trigger('click')
    await flushPromises()
    mockCompanyPageService.getPages.mockClear()

    // Switch back to All
    const allBtn = wrapper.findAll('button').find(b => b.text() === 'All Pages')
    await allBtn!.trigger('click')
    await flushPromises()

    const [params] = mockCompanyPageService.getPages.mock.calls[0]
    expect(params.mine).toBe(false)
  })

  // ── Follow / Unfollow ─────────────────────────────────────────────────────

  it('shows the Follow button for a page the user is not following', async () => {
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [makePage('c1', 'Acme Corp', { isFollowing: false })],
      page: 1, pageSize: 20, hasMore: false,
    })
    const wrapper = mountView()
    await flushPromises()

    const followBtns = wrapper.findAll('button').filter(b => b.text() === 'Follow')
    expect(followBtns.length).toBeGreaterThan(0)
  })

  it('shows the Unfollow button for a page the user is following', async () => {
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [makePage('c1', 'Acme Corp', { isFollowing: true })],
      page: 1, pageSize: 20, hasMore: false,
    })
    const wrapper = mountView()
    await flushPromises()

    const unfollowBtns = wrapper.findAll('button').filter(b => b.text() === 'Unfollow')
    expect(unfollowBtns.length).toBeGreaterThan(0)
  })

  it('calls companyPageService.follow when Follow is clicked', async () => {
    const p = makePage('c1', 'Acme Corp', { isFollowing: false })
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [p],
      page: 1, pageSize: 20, hasMore: false,
    })
    mockCompanyPageService.follow.mockResolvedValue({ ...p, isFollowing: true })

    const wrapper = mountView()
    await flushPromises()

    const followBtn = wrapper.findAll('button').find(b => b.text() === 'Follow')
    await followBtn!.trigger('click')
    await flushPromises()

    expect(mockCompanyPageService.follow).toHaveBeenCalledWith('c1')
  })

  it('calls companyPageService.unfollow when Unfollow is clicked', async () => {
    const p = makePage('c1', 'Acme Corp', { isFollowing: true })
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [p],
      page: 1, pageSize: 20, hasMore: false,
    })
    mockCompanyPageService.unfollow.mockResolvedValue({ ...p, isFollowing: false })

    const wrapper = mountView()
    await flushPromises()

    const unfollowBtn = wrapper.findAll('button').find(b => b.text() === 'Unfollow')
    await unfollowBtn!.trigger('click')
    await flushPromises()

    expect(mockCompanyPageService.unfollow).toHaveBeenCalledWith('c1')
  })

  it('updates the follow state in the list after following', async () => {
    const p = makePage('c1', 'Acme Corp', { isFollowing: false })
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [p],
      page: 1, pageSize: 20, hasMore: false,
    })
    mockCompanyPageService.follow.mockResolvedValue({ ...p, isFollowing: true })

    const wrapper = mountView()
    await flushPromises()

    const followBtn = wrapper.findAll('button').find(b => b.text() === 'Follow')
    await followBtn!.trigger('click')
    await flushPromises()

    // After following, the button text should change to Unfollow
    const buttons = wrapper.findAll('button').map(b => b.text())
    expect(buttons.some(t => t === 'Unfollow')).toBe(true)
  })

  // ── Error state ───────────────────────────────────────────────────────────

  it('shows the error message when the API call fails', async () => {
    mockCompanyPageService.getPages.mockRejectedValue({
      response: { data: { error: 'Service unavailable' } },
    })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Service unavailable')
  })

  it('shows a fallback error message on a generic failure', async () => {
    mockCompanyPageService.getPages.mockRejectedValue(new Error('Boom'))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Boom')
  })

  // ── Load more ─────────────────────────────────────────────────────────────

  it('shows the Load more button when hasMore is true', async () => {
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [makePage('c1', 'Acme Corp')],
      page: 1, pageSize: 20, hasMore: true,
    })
    const wrapper = mountView()
    await flushPromises()

    const loadMoreBtn = wrapper.findAll('button').find(b => b.text().includes('Load more'))
    expect(loadMoreBtn).toBeDefined()
  })

  it('calls getPages with page=2 when Load more is clicked', async () => {
    mockCompanyPageService.getPages.mockResolvedValueOnce({
      items: [makePage('c1', 'Acme Corp')],
      page: 1, pageSize: 20, hasMore: true,
    })
    mockCompanyPageService.getPages.mockResolvedValueOnce({
      items: [makePage('c2', 'Globex')],
      page: 2, pageSize: 20, hasMore: false,
    })

    const wrapper = mountView()
    await flushPromises()

    const loadMoreBtn = wrapper.findAll('button').find(b => b.text().includes('Load more'))
    await loadMoreBtn!.trigger('click')
    await flushPromises()

    // Second call should be page=2
    const secondCall = mockCompanyPageService.getPages.mock.calls[1]
    expect(secondCall[0].page).toBe(2)
  })

  // ── Create company form ───────────────────────────────────────────────────

  it('shows a validation error when submitting without a name', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="create-company-button"]').trigger('click')

    const createBtn = findModalSubmitBtn(wrapper)
    await createBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Name is required.')
    expect(mockCompanyPageService.createPage).not.toHaveBeenCalled()
  })

  it('calls companyPageService.createPage with the name on submit', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="create-company-button"]').trigger('click')

    await wrapper.find('input[type="text"][maxlength="256"]').setValue('My New Company')

    const createBtn = findModalSubmitBtn(wrapper)
    await createBtn!.trigger('click')
    await flushPromises()

    expect(mockCompanyPageService.createPage).toHaveBeenCalledOnce()
    const [payload] = mockCompanyPageService.createPage.mock.calls[0]
    expect(payload.name).toBe('My New Company')
  })

  it('closes the modal and prepends the new page after successful creation', async () => {
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [makePage('c1', 'Existing Corp')],
      page: 1, pageSize: 20, hasMore: false,
    })
    mockCompanyPageService.createPage.mockResolvedValue(makePage('new-1', 'My New Company'))

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="create-company-button"]').trigger('click')

    await wrapper.find('input[type="text"][maxlength="256"]').setValue('My New Company')

    const createBtn = findModalSubmitBtn(wrapper)
    await createBtn!.trigger('click')
    await flushPromises()

    // Modal should be gone
    expect(wrapper.findAll('h2').length).toBe(0)

    // New page should appear in the list
    const cards = wrapper.findAll('[data-testid="company-card"]')
    expect(cards).toHaveLength(2)
    expect(wrapper.text()).toContain('My New Company')
  })

  it('shows the server error message when creation fails', async () => {
    mockCompanyPageService.createPage.mockRejectedValue({
      response: { data: { error: 'Name already taken.' } },
    })

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="create-company-button"]').trigger('click')

    await wrapper.find('input[type="text"][maxlength="256"]').setValue('Duplicate')

    const createBtn = findModalSubmitBtn(wrapper)
    await createBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Name already taken.')
  })

  it('closes the create modal when Cancel is clicked', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="create-company-button"]').trigger('click')
    expect(wrapper.find('h2').text()).toBe('Create Company Page')

    const cancelBtn = wrapper.findAll('button').find(b => b.text() === 'Cancel')
    await cancelBtn!.trigger('click')

    expect(wrapper.findAll('h2').length).toBe(0)
  })

  // ── Role badges ───────────────────────────────────────────────────────────

  it('shows the Owner role badge on company cards', async () => {
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [makePage('c1', 'Acme Corp', { myRole: 'Owner' })],
      page: 1, pageSize: 20, hasMore: false,
    })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Owner')
  })

  it('shows the cover image when coverUrl is set', async () => {
    mockCompanyPageService.getPages.mockResolvedValue({
      items: [makePage('c1', 'Acme Corp', { coverUrl: 'https://cdn.example.com/cover.jpg' })],
      page: 1, pageSize: 20, hasMore: false,
    })
    const wrapper = mountView()
    await flushPromises()

    const img = wrapper.find('img[src="https://cdn.example.com/cover.jpg"]')
    expect(img.exists()).toBe(true)
  })
})
