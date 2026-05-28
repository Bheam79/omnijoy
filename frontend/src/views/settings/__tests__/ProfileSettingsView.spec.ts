import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import ProfileSettingsView from '../ProfileSettingsView.vue'

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockSlugService = vi.hoisted(() => ({
  check:       vi.fn(),
  setUserSlug: vi.fn(),
  setCompanySlug: vi.fn(),
}))

vi.mock('@/services/slugService', () => ({
  slugService: mockSlugService,
}))

const mockSetUser = vi.fn()
const fakeUser = {
  id:            'user-1',
  email:         'alice@example.com',
  displayName:   'Alice',
  gender:        'NotDisclosed' as const,
  showBirthDate: false,
  createdAt:     '2024-01-01T00:00:00Z',
  urlSlug:       null as string | null | undefined,
}

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({
    user:    fakeUser,
    setUser: mockSetUser,
  }),
}))

// ── Router fixture ─────────────────────────────────────────────────────────────

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/settings/profile', component: ProfileSettingsView },
      { path: '/settings', name: 'settings', component: { template: '<div>Settings Hub</div>' } },
    ],
  })
}

async function mountView(userSlug: string | null = null) {
  fakeUser.urlSlug = userSlug
  const router = makeRouter()
  await router.push('/settings/profile')
  const wrapper = mount(ProfileSettingsView, {
    global: {
      plugins: [createPinia(), router],
      // Use real SlugPicker — slugService is mocked above
    },
  })
  await flushPromises()
  return wrapper
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ProfileSettingsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
    vi.clearAllMocks()
    mockSlugService.check.mockResolvedValue({ available: true })
    mockSlugService.setUserSlug.mockResolvedValue(undefined)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('renders the page heading', async () => {
    const wrapper = await mountView()
    expect(wrapper.text()).toContain('Profile')
  })

  it('renders the SlugPicker component', async () => {
    const wrapper = await mountView()
    expect(wrapper.find('[data-testid="slug-picker"]').exists()).toBe(true)
  })

  it('passes null initialSlug when user has no slug — input is empty', async () => {
    const wrapper = await mountView(null)
    const input = wrapper.find('[data-testid="slug-input"]').element as HTMLInputElement
    expect(input.value).toBe('')
  })

  it('passes the current slug as initialSlug — input pre-filled', async () => {
    const wrapper = await mountView('alice')
    const input = wrapper.find('[data-testid="slug-input"]').element as HTMLInputElement
    expect(input.value).toBe('alice')
  })

  it('shows the current public URL when the user already has a slug', async () => {
    const wrapper = await mountView('alice')
    expect(wrapper.find('[data-testid="current-url"]').text()).toContain('omnijoy.com/alice')
  })

  it('calls setUserSlug when the user saves a new slug', async () => {
    const wrapper = await mountView(null)

    await wrapper.find('[data-testid="slug-input"]').setValue('newname')
    vi.advanceTimersByTime(400)
    await flushPromises()

    await wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()

    expect(mockSlugService.setUserSlug).toHaveBeenCalledWith('newname')
  })

  it('calls setUser on the auth store after a successful save', async () => {
    const wrapper = await mountView(null)

    await wrapper.find('[data-testid="slug-input"]').setValue('newname')
    vi.advanceTimersByTime(400)
    await flushPromises()

    await wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()

    expect(mockSetUser).toHaveBeenCalledWith(
      expect.objectContaining({ urlSlug: 'newname' }),
    )
  })

  it('calls setUserSlug(null) and updates auth when the slug is cleared', async () => {
    const wrapper = await mountView('alice')

    await wrapper.find('[data-testid="clear-btn"]').trigger('click')
    await flushPromises()

    expect(mockSlugService.setUserSlug).toHaveBeenCalledWith(null)
    expect(mockSetUser).toHaveBeenCalledWith(
      expect.objectContaining({ urlSlug: undefined }),
    )
  })

  it('has a back link to /settings', async () => {
    const wrapper = await mountView()
    // RouterLink renders an <a> tag with the to attribute
    const links = wrapper.findAll('a')
    const settingsLink = links.find(l => l.attributes('href') === '/settings')
    expect(settingsLink?.exists()).toBe(true)
  })
})
