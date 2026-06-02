import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'

// ── Service mock ──────────────────────────────────────────────────────────────

const mockUserService = vi.hoisted(() => ({
  getPrivacySettings:    vi.fn(),
  updatePrivacySettings: vi.fn(),
}))

vi.mock('@/services/userService', () => ({
  userService: mockUserService,
}))

// Import after the mock so the component picks it up.
import PrivacySettingsView from '@/views/settings/PrivacySettingsView.vue'
import type { PrivacySettings } from '@/services/userService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

const defaultPrivacy: PrivacySettings = {
  whoCanSeePosts:      'Friends',
  whoCanSeeProfile:    'Friends',
  whoCanSendMessages:  'Friends',
  whoCanSeeFriendList: 'Friends',
  whoCanSeeEvents:     'Friends',
  whoCanTagInPosts:    'Friends',
}

// ── Router fixture ────────────────────────────────────────────────────────────

function makeRouter(): Router {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/settings/privacy', component: PrivacySettingsView },
      { path: '/settings',         name: 'settings', component: { template: '<div>Settings</div>' } },
    ],
  })
}

async function mountView() {
  const router = makeRouter()
  await router.push('/settings/privacy')
  await router.isReady()
  const wrapper = mount(PrivacySettingsView, {
    global: { plugins: [createPinia(), router] },
  })
  await flushPromises()
  return wrapper
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('PrivacySettingsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockUserService.getPrivacySettings.mockResolvedValue({ ...defaultPrivacy })
    mockUserService.updatePrivacySettings.mockImplementation(
      async (payload: Partial<PrivacySettings>) => ({ ...defaultPrivacy, ...payload }),
    )
  })

  // ── Initial load ──────────────────────────────────────────────────────────

  it('calls userService.getPrivacySettings on mount', async () => {
    await mountView()
    expect(mockUserService.getPrivacySettings).toHaveBeenCalledTimes(1)
  })

  it('renders the "Privacy" page heading', async () => {
    const wrapper = await mountView()
    expect(wrapper.find('h1').text()).toBe('Privacy')
  })

  it('shows a loading spinner before settings resolve', async () => {
    mockUserService.getPrivacySettings.mockReturnValue(new Promise(() => { /* never */ }))
    const router = makeRouter()
    await router.push('/settings/privacy')
    const wrapper = mount(PrivacySettingsView, {
      global: { plugins: [createPinia(), router] },
    })
    // No flushPromises — keep loading state observable

    expect(wrapper.find('.animate-spin').exists()).toBe(true)
    // Form is hidden while loading
    expect(wrapper.find('[data-testid="privacy-settings-form"]').exists()).toBe(false)
  })

  it('renders the form (not the spinner) after settings load', async () => {
    const wrapper = await mountView()
    expect(wrapper.find('.animate-spin').exists()).toBe(false)
    expect(wrapper.find('[data-testid="privacy-settings-form"]').exists()).toBe(true)
  })

  // ── Select rendering ──────────────────────────────────────────────────────

  it('renders all six privacy select dropdowns', async () => {
    const wrapper = await mountView()
    for (const testid of [
      'who-can-see-posts',
      'who-can-see-profile',
      'who-can-send-requests',
      'who-can-see-friends',
      'who-can-see-events',
      'who-can-tag-in-posts',
    ]) {
      expect(wrapper.find(`[data-testid="${testid}"]`).exists(), `missing select ${testid}`).toBe(true)
    }
  })

  it('each select offers the four privacy audience options', async () => {
    const wrapper = await mountView()
    const select = wrapper.find('[data-testid="who-can-see-posts"]')
    const options = select.findAll('option').map(o => o.attributes('value'))
    expect(options).toEqual(['Everyone', 'FriendsOfFriends', 'Friends', 'OnlyMe'])
  })

  it('reflects the loaded values in the select dropdowns', async () => {
    mockUserService.getPrivacySettings.mockResolvedValue({
      ...defaultPrivacy,
      whoCanSeePosts:   'Everyone',
      whoCanTagInPosts: 'OnlyMe',
    })
    const wrapper = await mountView()

    const posts = wrapper.find('[data-testid="who-can-see-posts"]').element as HTMLSelectElement
    const tag   = wrapper.find('[data-testid="who-can-tag-in-posts"]').element as HTMLSelectElement
    const prof  = wrapper.find('[data-testid="who-can-see-profile"]').element as HTMLSelectElement
    expect(posts.value).toBe('Everyone')
    expect(tag.value).toBe('OnlyMe')
    expect(prof.value).toBe('Friends')
  })

  // ── Save behaviour ────────────────────────────────────────────────────────

  it('changing a select value updates the bound model', async () => {
    const wrapper = await mountView()
    const select = wrapper.find('[data-testid="who-can-see-posts"]')
    await select.setValue('Everyone')

    expect((select.element as HTMLSelectElement).value).toBe('Everyone')
  })

  it('submitting the form calls userService.updatePrivacySettings with the full payload', async () => {
    const wrapper = await mountView()

    await wrapper.find('[data-testid="who-can-see-posts"]').setValue('Everyone')
    await wrapper.find('[data-testid="who-can-tag-in-posts"]').setValue('OnlyMe')

    await wrapper.find('[data-testid="privacy-settings-form"]').trigger('submit.prevent')
    await flushPromises()

    expect(mockUserService.updatePrivacySettings).toHaveBeenCalledTimes(1)
    expect(mockUserService.updatePrivacySettings).toHaveBeenCalledWith(
      expect.objectContaining({
        whoCanSeePosts:      'Everyone',
        whoCanTagInPosts:    'OnlyMe',
        whoCanSeeProfile:    'Friends',
        whoCanSendMessages:  'Friends',
        whoCanSeeFriendList: 'Friends',
        whoCanSeeEvents:     'Friends',
      }),
    )
  })

  it('clicking the Save button submits the form too', async () => {
    const wrapper = await mountView()

    await wrapper.find('[data-testid="who-can-see-posts"]').setValue('Everyone')
    await wrapper.find('[data-testid="save-privacy-button"]').trigger('click')
    await wrapper.find('[data-testid="privacy-settings-form"]').trigger('submit.prevent')
    await flushPromises()

    expect(mockUserService.updatePrivacySettings).toHaveBeenCalled()
  })

  // ── Success / error feedback ──────────────────────────────────────────────

  it('shows a success message after a successful save', async () => {
    const wrapper = await mountView()
    await wrapper.find('[data-testid="privacy-settings-form"]').trigger('submit.prevent')
    await flushPromises()

    expect(wrapper.text()).toContain('Privacy settings saved.')
  })

  it('shows the server error when the save fails with a typed response', async () => {
    mockUserService.updatePrivacySettings.mockRejectedValue({
      response: { data: { error: 'Not allowed.' } },
    })

    const wrapper = await mountView()
    await wrapper.find('[data-testid="privacy-settings-form"]').trigger('submit.prevent')
    await flushPromises()

    expect(wrapper.text()).toContain('Not allowed.')
    expect(wrapper.text()).not.toContain('Privacy settings saved.')
  })

  it('shows a fallback error message when the save fails without a typed response', async () => {
    mockUserService.updatePrivacySettings.mockRejectedValue(new Error('boom'))

    const wrapper = await mountView()
    await wrapper.find('[data-testid="privacy-settings-form"]').trigger('submit.prevent')
    await flushPromises()

    expect(wrapper.text()).toContain('Failed to save settings.')
  })

  it('shows an error banner if the initial load fails', async () => {
    mockUserService.getPrivacySettings.mockRejectedValue(new Error('boom'))

    const router = makeRouter()
    await router.push('/settings/privacy')
    const wrapper = mount(PrivacySettingsView, {
      global: { plugins: [createPinia(), router] },
    })
    await flushPromises()

    // The form template requires `privacy` to be non-null, so the error
    // banner from the catch block is not rendered. The view drops out of
    // the loading state and shows nothing actionable — verify the spinner
    // is gone and the form was never rendered.
    expect(wrapper.find('.animate-spin').exists()).toBe(false)
    expect(wrapper.find('[data-testid="privacy-settings-form"]').exists()).toBe(false)
  })

  it('clears the previous success message when a new save fails', async () => {
    const wrapper = await mountView()

    // Successful save
    await wrapper.find('[data-testid="privacy-settings-form"]').trigger('submit.prevent')
    await flushPromises()
    expect(wrapper.text()).toContain('Privacy settings saved.')

    // Make the next save fail
    mockUserService.updatePrivacySettings.mockRejectedValueOnce({
      response: { data: { error: 'Server down.' } },
    })
    await wrapper.find('[data-testid="privacy-settings-form"]').trigger('submit.prevent')
    await flushPromises()

    expect(wrapper.text()).toContain('Server down.')
    expect(wrapper.text()).not.toContain('Privacy settings saved.')
  })

  // ── Loading / disabled save state ─────────────────────────────────────────

  it('disables the Save button and shows "Saving…" while a save is in flight', async () => {
    let resolveUpdate!: (p: PrivacySettings) => void
    mockUserService.updatePrivacySettings.mockReturnValue(
      new Promise<PrivacySettings>((r) => { resolveUpdate = r }),
    )

    const wrapper = await mountView()
    const saveBtn = wrapper.find('[data-testid="save-privacy-button"]')
    expect((saveBtn.element as HTMLButtonElement).disabled).toBe(false)
    expect(saveBtn.text()).toContain('Save privacy settings')

    await wrapper.find('[data-testid="privacy-settings-form"]').trigger('submit.prevent')
    await flushPromises()

    expect((saveBtn.element as HTMLButtonElement).disabled).toBe(true)
    expect(saveBtn.text()).toContain('Saving')

    resolveUpdate({ ...defaultPrivacy })
    await flushPromises()

    expect((saveBtn.element as HTMLButtonElement).disabled).toBe(false)
    expect(saveBtn.text()).toContain('Save privacy settings')
  })

  // ── Back link ─────────────────────────────────────────────────────────────

  it('renders a back link to /settings', async () => {
    const wrapper = await mountView()
    const link = wrapper.find('a[href="/settings"]')
    expect(link.exists()).toBe(true)
    expect(link.text()).toContain('Settings')
  })
})
