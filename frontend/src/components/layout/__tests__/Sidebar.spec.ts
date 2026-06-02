import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Sidebar from '../Sidebar.vue'

// ── vue-router mock ───────────────────────────────────────────────────────────

const mockRoute = vi.hoisted(() => ({
  path: '/wall',
}))

vi.mock('vue-router', async () => {
  const actual = await vi.importActual<typeof import('vue-router')>('vue-router')
  return { ...actual, useRoute: () => mockRoute }
})

// ── Store mocks ───────────────────────────────────────────────────────────────

const authStore = vi.hoisted(() => ({
  user: {
    id:          'u1',
    displayName: 'Alice',
    avatarUrl:   null as string | null,
    role:        'User' as string,
  } as { id: string; displayName: string; avatarUrl: string | null; role: string } | null,
}))

vi.mock('@/stores/auth', () => ({ useAuthStore: () => authStore }))

const liveStore = vi.hoisted(() => ({
  streams: [] as unknown[],
}))

vi.mock('@/stores/live', () => ({ useLiveStore: () => liveStore }))

const companyModeStore = vi.hoisted(() => ({
  isActive:            false,
  activeCompany:       null as { id: string; name: string } | null,
  activeEvent:         null as { id: string; title: string } | null,
  canManageActiveEvent: false,
}))

vi.mock('@/stores/companyMode', () => ({
  useCompanyModeStore: () => companyModeStore,
}))

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountSidebar(props = { open: true }) {
  return mount(Sidebar, {
    props,
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub, Transition: false },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('Sidebar', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()

    // Reset state
    mockRoute.path               = '/wall'
    authStore.user               = {
      id: 'u1', displayName: 'Alice', avatarUrl: null, role: 'User',
    }
    liveStore.streams            = []
    companyModeStore.isActive            = false
    companyModeStore.activeCompany       = null
    companyModeStore.activeEvent         = null
    companyModeStore.canManageActiveEvent = false
  })

  // ── Navigation links ───────────────────────────────────────────────────────

  it('renders all core navigation links', async () => {
    const wrapper = mountSidebar()
    await flushPromises()

    expect(wrapper.find('[data-testid="nav-wall"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-friends"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-events"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-company"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-live"]').exists()).toBe(true)
  })

  it("shows the user's displayName on the wall link", async () => {
    authStore.user = { id: 'u1', displayName: 'Bob', avatarUrl: null, role: 'User' }
    const wrapper = mountSidebar()
    await flushPromises()

    expect(wrapper.find('[data-testid="nav-wall"]').text()).toContain('Bob')
  })

  // ── Active route highlighting ──────────────────────────────────────────────

  it('highlights the wall link when route is /wall', async () => {
    mockRoute.path = '/wall'
    const wrapper = mountSidebar()
    await flushPromises()

    const wallLink = wrapper.find('[data-testid="nav-wall"]')
    expect(wallLink.classes()).toContain('text-indigo-300')
  })

  it('does not highlight the friends link when route is /wall', async () => {
    mockRoute.path = '/wall'
    const wrapper = mountSidebar()
    await flushPromises()

    const friendsLink = wrapper.find('[data-testid="nav-friends"]')
    expect(friendsLink.classes()).not.toContain('text-indigo-300')
  })

  it('highlights the events link when route is /events', async () => {
    mockRoute.path = '/events'
    const wrapper = mountSidebar()
    await flushPromises()

    const eventsLink = wrapper.find('[data-testid="nav-events"]')
    expect(eventsLink.classes()).toContain('text-indigo-300')
  })

  it('highlights the events link when route is a sub-route like /events/123', async () => {
    mockRoute.path = '/events/abc-123'
    const wrapper = mountSidebar()
    await flushPromises()

    const eventsLink = wrapper.find('[data-testid="nav-events"]')
    expect(eventsLink.classes()).toContain('text-indigo-300')
  })

  // ── Admin link ─────────────────────────────────────────────────────────────

  it('shows the Admin link for users with role Admin', async () => {
    authStore.user = { id: 'u1', displayName: 'Admin', avatarUrl: null, role: 'Admin' }
    const wrapper = mountSidebar()
    await flushPromises()

    expect(wrapper.find('[data-testid="nav-admin"]').exists()).toBe(true)
  })

  it('shows the Admin link for users with role Moderator', async () => {
    authStore.user = { id: 'u1', displayName: 'Mod', avatarUrl: null, role: 'Moderator' }
    const wrapper = mountSidebar()
    await flushPromises()

    expect(wrapper.find('[data-testid="nav-admin"]').exists()).toBe(true)
  })

  it('does NOT show the Admin link for regular users', async () => {
    authStore.user = { id: 'u1', displayName: 'Alice', avatarUrl: null, role: 'User' }
    const wrapper = mountSidebar()
    await flushPromises()

    expect(wrapper.find('[data-testid="nav-admin"]').exists()).toBe(false)
  })

  // ── Event management section ───────────────────────────────────────────────
  // The section appears only when: route starts with /events/, there is an
  // activeEvent, and canManageActiveEvent is true.

  it('shows event management links when on an event page with canManageActiveEvent=true', async () => {
    companyModeStore.activeEvent         = { id: 'e1', title: 'Summer Fest' }
    companyModeStore.canManageActiveEvent = true
    mockRoute.path = '/events/e1'

    const wrapper = mountSidebar()
    await flushPromises()

    expect(wrapper.find('[data-testid="event-nav-home"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="event-nav-edit"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="event-nav-participants"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="event-nav-settings"]').exists()).toBe(true)
    // Event title appears in the section heading link
    expect(wrapper.text()).toContain('Summer Fest')
  })

  it('does NOT show event management links when on a non-event route', async () => {
    companyModeStore.activeEvent         = { id: 'e1', title: 'Summer Fest' }
    companyModeStore.canManageActiveEvent = true
    mockRoute.path = '/wall'   // not an event route

    const wrapper = mountSidebar()
    await flushPromises()

    expect(wrapper.find('[data-testid="event-nav-home"]').exists()).toBe(false)
  })

  it('does NOT show event management links when canManageActiveEvent is false', async () => {
    companyModeStore.activeEvent         = { id: 'e1', title: 'Summer Fest' }
    companyModeStore.canManageActiveEvent = false
    mockRoute.path = '/events/e1'

    const wrapper = mountSidebar()
    await flushPromises()

    expect(wrapper.find('[data-testid="event-nav-home"]').exists()).toBe(false)
  })

  // ── Company mode active context ────────────────────────────────────────────
  // When in company mode, all regular nav links still appear (Sidebar remains
  // visible — it's CompanySidebar that renders the company-specific nav).

  it('still renders core nav links when companyMode.isActive is true', async () => {
    companyModeStore.isActive      = true
    companyModeStore.activeCompany = { id: 'c1', name: 'Acme Corp' }

    const wrapper = mountSidebar()
    await flushPromises()

    expect(wrapper.find('[data-testid="nav-wall"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-company"]').exists()).toBe(true)
  })

  // ── Sidebar visibility ─────────────────────────────────────────────────────

  it('applies translate-x-0 class when open=true', () => {
    const wrapper = mountSidebar({ open: true })
    const aside = wrapper.find('aside')
    expect(aside.classes()).toContain('translate-x-0')
  })

  it('applies -translate-x-full when open=false', () => {
    const wrapper = mountSidebar({ open: false })
    const aside = wrapper.find('aside')
    expect(aside.classes()).toContain('-translate-x-full')
  })

  // ── Mobile backdrop ────────────────────────────────────────────────────────

  it('emits close when the mobile backdrop is clicked', async () => {
    const wrapper = mountSidebar({ open: true })

    const backdrop = wrapper.find('[aria-hidden="true"]')
    expect(backdrop.exists()).toBe(true)
    await backdrop.trigger('click')

    expect(wrapper.emitted('close')).toBeTruthy()
  })
})
