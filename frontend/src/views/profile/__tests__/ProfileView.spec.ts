import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ProfileView from '../ProfileView.vue'
import type { UserProfile } from '@/services/userService'

// ── vue-router mock ──────────────────────────────────────────────────────────
//
// useRoute returns a live getter so tests can update `params.userId` between
// runs without re-importing.

const mockRouteState = vi.hoisted(() => ({
  params: { userId: 'user-2' } as Record<string, string>,
}))

vi.mock('vue-router', async () => {
  const actual = await vi.importActual<typeof import('vue-router')>('vue-router')
  return {
    ...actual,
    useRoute:  () => ({ get params() { return mockRouteState.params } }),
    useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  }
})

// ── userService mock ─────────────────────────────────────────────────────────

const mockUserService = vi.hoisted(() => ({
  getProfile:            vi.fn(),
  updateProfile:         vi.fn(),
  uploadAvatar:          vi.fn(),
  uploadCover:           vi.fn(),
  getPrivacySettings:    vi.fn(),
  updatePrivacySettings: vi.fn(),
}))

vi.mock('@/services/userService', () => ({
  userService: mockUserService,
}))

// ── auth store mock ──────────────────────────────────────────────────────────

const authStore = vi.hoisted(() => ({
  user: {
    id:            'user-1',
    email:         'me@example.com',
    displayName:   'Me',
    avatarUrl:     null as string | null,
    gender:        'NotDisclosed' as const,
    showBirthDate: false,
    createdAt:     '2024-01-01T00:00:00Z',
  } as { id: string; displayName: string; email: string; avatarUrl: string | null; gender: string; showBirthDate: boolean; createdAt: string } | null,
  setUser: vi.fn(),
}))

vi.mock('@/stores/auth', () => ({ useAuthStore: () => authStore }))

// ── chat store mock ──────────────────────────────────────────────────────────

const chatStore = vi.hoisted(() => ({
  openConversationWith: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/stores/chat', () => ({ useChatStore: () => chatStore }))

// ── Child component stubs ────────────────────────────────────────────────────
//
// FriendButton owns its own state machine (Pending / Add Friend / Friends) and
// is unit-tested separately — here we only verify ProfileView wires it up with
// the right `user-id` prop. ReportModal is stubbed for the same reason.

vi.mock('@/components/friends/FriendButton.vue', () => ({
  default: {
    name:     'FriendButton',
    props:    ['userId'],
    template: '<div data-testid="friend-button-stub" :data-user-id="userId" />',
  },
}))

vi.mock('@/components/post/ReportModal.vue', () => ({
  default: {
    name:     'ReportModal',
    props:    ['modelValue', 'targetType', 'targetId'],
    emits:    ['update:modelValue'],
    template: '<div data-testid="report-modal-stub" :data-target-type="targetType" :data-target-id="targetId" />',
  },
}))

// ── Helpers ──────────────────────────────────────────────────────────────────

function makeProfile(over: Partial<UserProfile> = {}): UserProfile {
  return {
    id:            'user-2',
    displayName:   'Alice',
    avatarUrl:     undefined,
    coverUrl:      undefined,
    bio:           'Hello world',
    gender:        'NotDisclosed',
    birthDate:     undefined,
    showBirthDate: false,
    friendCount:   12,
    isOwnProfile:  false,
    isFriend:      false,
    createdAt:     '2024-01-01T00:00:00Z',
    ...over,
  }
}

function mountView() {
  return mount(ProfileView, {
    global: {
      plugins: [createPinia()],
    },
  })
}

// ── Tests ────────────────────────────────────────────────────────────────────

describe('ProfileView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()

    // Default route param
    mockRouteState.params = { userId: 'user-2' }

    // Default auth user
    authStore.user = {
      id:            'user-1',
      email:         'me@example.com',
      displayName:   'Me',
      avatarUrl:     null,
      gender:        'NotDisclosed',
      showBirthDate: false,
      createdAt:     '2024-01-01T00:00:00Z',
    }

    // Default service responses
    mockUserService.getProfile.mockResolvedValue(makeProfile())
    mockUserService.updateProfile.mockResolvedValue({})
    mockUserService.uploadAvatar.mockResolvedValue({})
    mockUserService.uploadCover.mockResolvedValue({})
    mockUserService.getPrivacySettings.mockResolvedValue({
      whoCanSeePosts:      'Everyone',
      whoCanSeeProfile:    'Everyone',
      whoCanSendMessages:  'Everyone',
      whoCanSeeFriendList: 'Everyone',
      whoCanSeeEvents:     'Everyone',
      whoCanTagInPosts:    'Everyone',
    })
  })

  // ── Loading state ──────────────────────────────────────────────────────────

  it('shows the loading spinner before the profile resolves', () => {
    // Pending promise → never resolves during this test
    mockUserService.getProfile.mockReturnValueOnce(new Promise(() => {}))
    const wrapper = mountView()

    expect(wrapper.find('.animate-spin').exists()).toBe(true)
    // No profile body yet
    expect(wrapper.find('[data-testid="profile-display-name"]').exists()).toBe(false)
  })

  it('fetches the profile for the userId from the route on mount', async () => {
    mockRouteState.params = { userId: 'route-user-xyz' }
    mountView()
    await flushPromises()

    expect(mockUserService.getProfile).toHaveBeenCalledWith('route-user-xyz')
  })

  // ── Rendering display data ─────────────────────────────────────────────────

  it('renders the user display name and bio after load', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({
      displayName: 'Alice Wonderland',
      bio:         'Curious explorer of strange tea-parties.',
    }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="profile-display-name"]').text()).toBe('Alice Wonderland')
    expect(wrapper.find('[data-testid="profile-bio"]').text())
      .toContain('Curious explorer of strange tea-parties.')
  })

  it('renders the avatar img when avatarUrl is set', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({
      avatarUrl:   'https://cdn.example.com/avatar.jpg',
      displayName: 'Alice',
    }))
    const wrapper = mountView()
    await flushPromises()

    const avatar = wrapper.find('img[alt="Alice"]')
    expect(avatar.exists()).toBe(true)
    expect(avatar.attributes('src')).toBe('https://cdn.example.com/avatar.jpg')
  })

  it('renders initials when avatarUrl is missing', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({
      avatarUrl:   undefined,
      displayName: 'Bob Builder',
    }))
    const wrapper = mountView()
    await flushPromises()

    // Initials computed from first letters of words, capped at 2
    expect(wrapper.text()).toContain('BB')
  })

  it('renders the cover photo when coverUrl is set', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({
      coverUrl: 'https://cdn.example.com/cover.jpg',
    }))
    const wrapper = mountView()
    await flushPromises()

    const cover = wrapper.find('img[alt="Cover"]')
    expect(cover.exists()).toBe(true)
    expect(cover.attributes('src')).toBe('https://cdn.example.com/cover.jpg')
  })

  // ── Counts ─────────────────────────────────────────────────────────────────

  it('renders the friend count', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({ friendCount: 47 }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('47')
    expect(wrapper.text()).toContain('friends')
  })

  it('shows mutual friend count when greater than 0', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({
      friendCount:       10,
      mutualFriendCount: 4,
    }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('4')
    expect(wrapper.text()).toContain('mutual')
  })

  it('does not render mutual count when undefined or 0', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({
      friendCount:       10,
      mutualFriendCount: 0,
    }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).not.toContain('mutual')
  })

  it('shows the year the user joined', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({
      createdAt: '2021-07-15T00:00:00Z',
    }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Joined 2021')
  })

  it('shows gender label when not "NotDisclosed"', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({ gender: 'Female' }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Female')
  })

  it('hides gender when "NotDisclosed"', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({ gender: 'NotDisclosed' }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).not.toContain('Not disclosed')
    expect(wrapper.text()).not.toContain('NotDisclosed')
  })

  // ── Own profile vs other user ──────────────────────────────────────────────

  it('shows the Edit profile button when isOwnProfile is true', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({
      id:           'user-1',
      isOwnProfile: true,
    }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="profile-edit-button"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="friend-button-stub"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="profile-message-button"]').exists()).toBe(false)
  })

  it('shows the FriendButton (with route userId) when isOwnProfile is false', async () => {
    mockRouteState.params = { userId: 'user-2' }
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({
      id:           'user-2',
      isOwnProfile: false,
    }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="profile-edit-button"]').exists()).toBe(false)
    const friendBtn = wrapper.find('[data-testid="friend-button-stub"]')
    expect(friendBtn.exists()).toBe(true)
    expect(friendBtn.attributes('data-user-id')).toBe('user-2')
  })

  it('shows the Message button when viewing someone else', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({ isOwnProfile: false }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="profile-message-button"]').exists()).toBe(true)
  })

  it("clicking Message opens a conversation via the chat store", async () => {
    mockRouteState.params = { userId: 'user-2' }
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({
      id:           'user-2',
      isOwnProfile: false,
    }))
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="profile-message-button"]').trigger('click')

    expect(chatStore.openConversationWith).toHaveBeenCalledWith('user-2')
  })

  it('renders the ReportModal stub for other users with correct target props', async () => {
    mockRouteState.params = { userId: 'user-2' }
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({
      id:           'user-2',
      isOwnProfile: false,
    }))
    const wrapper = mountView()
    await flushPromises()

    const modal = wrapper.find('[data-testid="report-modal-stub"]')
    expect(modal.exists()).toBe(true)
    expect(modal.attributes('data-target-type')).toBe('User')
    expect(modal.attributes('data-target-id')).toBe('user-2')
  })

  it('does NOT render the ReportModal when viewing own profile', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({ isOwnProfile: true }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="report-modal-stub"]').exists()).toBe(false)
  })

  // ── Posts section ─────────────────────────────────────────────────────────

  it('renders the Posts heading and empty placeholder', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile())
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Posts')
    expect(wrapper.text()).toContain('No posts yet')
  })

  // ── Error states ───────────────────────────────────────────────────────────

  it('shows a "User not found" message on 404', async () => {
    mockUserService.getProfile.mockRejectedValueOnce({ response: { status: 404 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('User not found')
    // No profile body rendered
    expect(wrapper.find('[data-testid="profile-display-name"]').exists()).toBe(false)
  })

  it('shows a "private profile" message on 403', async () => {
    mockUserService.getProfile.mockRejectedValueOnce({ response: { status: 403 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('This profile is private.')
  })

  it('shows a generic failure message on other errors', async () => {
    mockUserService.getProfile.mockRejectedValueOnce(new Error('network down'))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Failed to load profile')
  })

  it('clears loading=false after a failed fetch', async () => {
    mockUserService.getProfile.mockRejectedValueOnce({ response: { status: 404 } })
    const wrapper = mountView()
    await flushPromises()

    // Spinner from the initial loading state should be gone (error UI took over).
    // The error icon container is the only div that still uses bg-slate-700 with
    // an svg, so easier: just confirm the spinner class isn't present anymore.
    expect(wrapper.find('div.animate-spin').exists()).toBe(false)
  })

  // ── Edit profile modal ────────────────────────────────────────────────────

  it('clicking Edit profile opens the edit modal pre-filled with current values', async () => {
    mockUserService.getProfile.mockResolvedValueOnce(makeProfile({
      isOwnProfile: true,
      displayName:  'Me Myself',
      bio:          'My short bio',
      gender:       'Male',
    }))
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="profile-edit-button"]').trigger('click')
    await flushPromises()

    // Modal is teleported to body — query the global document
    const heading = document.body.querySelector('h2')
    expect(heading?.textContent).toContain('Edit profile')

    const nameInput = document.body.querySelector('input[type="text"]') as HTMLInputElement | null
    expect(nameInput?.value).toBe('Me Myself')
  })
})
