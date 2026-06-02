import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'

// ── Auth store mock ───────────────────────────────────────────────────────────

type FakeUser = {
  id:            string
  email:         string
  displayName:   string
  gender:        'NotDisclosed'
  showBirthDate: boolean
  createdAt:     string
}

const authStore = vi.hoisted(() => ({
  user: null as FakeUser | null,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => authStore,
}))

// Import after the mock so the component picks it up.
import SettingsView from '@/views/settings/SettingsView.vue'

// ── Router fixture ────────────────────────────────────────────────────────────
//
// The Settings hub uses RouterLink components — render with a real router so
// the links resolve to <a href="…"> tags we can assert against.

function makeRouter(): Router {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/settings',                component: SettingsView },
      { path: '/settings/profile',        name: 'profile',        component: { template: '<div>Profile</div>' } },
      { path: '/settings/privacy',        name: 'privacy',        component: { template: '<div>Privacy</div>' } },
      { path: '/settings/notifications',  name: 'notifications',  component: { template: '<div>Notifications</div>' } },
      { path: '/settings/account',        name: 'account',        component: { template: '<div>Account</div>' } },
      { path: '/profile/:id',             name: 'user-profile',   component: { template: '<div>User profile</div>' } },
    ],
  })
}

async function mountView(initialPath = '/settings') {
  const router = makeRouter()
  await router.push(initialPath)
  await router.isReady()
  const wrapper = mount(SettingsView, {
    global: { plugins: [createPinia(), router] },
  })
  await flushPromises()
  return { wrapper, router }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('SettingsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()

    authStore.user = {
      id:            'user-1',
      email:         'alice@example.com',
      displayName:   'Alice',
      gender:        'NotDisclosed',
      showBirthDate: false,
      createdAt:     '2024-01-01T00:00:00Z',
    }
  })

  // ── Heading / layout ──────────────────────────────────────────────────────

  it('renders the "Settings" heading', async () => {
    const { wrapper } = await mountView()
    expect(wrapper.find('h1').text()).toBe('Settings')
  })

  // ── Sub-page navigation links ─────────────────────────────────────────────

  it('renders a navigation link to the Public URL / Profile sub-page (/settings/profile)', async () => {
    const { wrapper } = await mountView()
    const link = wrapper.find('a[href="/settings/profile"]')
    expect(link.exists()).toBe(true)
    expect(link.text()).toContain('Public URL')
  })

  it('renders a navigation link to the Privacy sub-page (/settings/privacy)', async () => {
    const { wrapper } = await mountView()
    const link = wrapper.find('a[href="/settings/privacy"]')
    expect(link.exists()).toBe(true)
    expect(link.text()).toContain('Privacy')
  })

  it('renders a navigation link to the Notifications sub-page (/settings/notifications)', async () => {
    const { wrapper } = await mountView()
    const link = wrapper.find('a[href="/settings/notifications"]')
    expect(link.exists()).toBe(true)
    expect(link.text()).toContain('Notifications')
  })

  it('renders a navigation link to the Account sub-page (/settings/account)', async () => {
    const { wrapper } = await mountView()
    const link = wrapper.find('a[href="/settings/account"]')
    expect(link.exists()).toBe(true)
    expect(link.text()).toContain('Account')
  })

  it('renders all four settings sub-page links (Profile, Privacy, Notifications, Account)', async () => {
    const { wrapper } = await mountView()
    const hrefs = wrapper.findAll('a').map(a => a.attributes('href'))
    expect(hrefs).toContain('/settings/profile')
    expect(hrefs).toContain('/settings/privacy')
    expect(hrefs).toContain('/settings/notifications')
    expect(hrefs).toContain('/settings/account')
  })

  // ── My Profile link (depends on auth.user) ────────────────────────────────

  it('renders the My Profile link to /profile/<userId> when the user is signed in', async () => {
    const { wrapper } = await mountView()
    const link = wrapper.find('a[href="/profile/user-1"]')
    expect(link.exists()).toBe(true)
    expect(link.text()).toContain('My Profile')
  })

  it('does NOT render the My Profile link when auth.user is null', async () => {
    authStore.user = null
    const { wrapper } = await mountView()
    const profileLinks = wrapper.findAll('a').filter(a => a.text().includes('My Profile'))
    expect(profileLinks).toHaveLength(0)
  })

  // ── Active-route highlighting ─────────────────────────────────────────────
  //
  // vue-router's RouterLink stamps the active class on whichever link matches
  // the current location. The Settings hub itself is rendered on the parent
  // route, so we navigate to a sub-route and assert that exactly the right
  // link picks up `router-link-active`.

  it('highlights the Privacy link when the current route is /settings/privacy', async () => {
    const { wrapper, router } = await mountView('/settings')
    await router.push('/settings/privacy')
    await flushPromises()

    const active = wrapper.findAll('a.router-link-active').map(a => a.attributes('href'))
    expect(active).toContain('/settings/privacy')
    expect(active).not.toContain('/settings/notifications')
    expect(active).not.toContain('/settings/account')
  })

  it('highlights the Notifications link when the current route is /settings/notifications', async () => {
    const { wrapper, router } = await mountView('/settings')
    await router.push('/settings/notifications')
    await flushPromises()

    const active = wrapper.findAll('a.router-link-active').map(a => a.attributes('href'))
    expect(active).toContain('/settings/notifications')
    expect(active).not.toContain('/settings/privacy')
  })

  it('highlights the Account link when the current route is /settings/account', async () => {
    const { wrapper, router } = await mountView('/settings')
    await router.push('/settings/account')
    await flushPromises()

    const active = wrapper.findAll('a.router-link-active').map(a => a.attributes('href'))
    expect(active).toContain('/settings/account')
  })

  // ── Sub-page link card descriptions ───────────────────────────────────────

  it('shows descriptive helper text underneath each link', async () => {
    const { wrapper } = await mountView()
    const text = wrapper.text()
    expect(text).toContain('Choose a vanity URL')
    expect(text).toContain('who can see your posts')
    expect(text).toContain('which events trigger notifications')
    expect(text).toContain('Change email, password')
  })
})
