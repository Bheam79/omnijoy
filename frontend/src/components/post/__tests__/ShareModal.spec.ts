import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { RouterLinkStub } from '@vue/test-utils'
import ShareModal from '@/components/post/ShareModal.vue'
import type { PostDto } from '@/services/postService'

// ── Mock shareService ─────────────────────────────────────────────────────────

const mockSharePost = vi.hoisted(() => vi.fn())

vi.mock('@/services/shareService', () => ({
  shareService: {
    sharePost: mockSharePost,
  },
}))

// ── Mock friendsStore ─────────────────────────────────────────────────────────

const mockLoadFriends = vi.hoisted(() => vi.fn())
const friendsMock = vi.hoisted(() => ({
  friends: [] as { user: { id: string; displayName: string } }[],
  loadingFriends: false,
  loadFriends: mockLoadFriends,
}))

vi.mock('@/stores/friends', () => ({
  useFriendsStore: () => friendsMock,
}))

// ── Mock feedStore ────────────────────────────────────────────────────────────

const mockPrependSharedPost = vi.hoisted(() => vi.fn())

vi.mock('@/stores/feed', () => ({
  useFeedStore: () => ({
    prependSharedPost: mockPrependSharedPost,
  }),
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makePost(overrides: Partial<PostDto> = {}): PostDto {
  return {
    id:        'post-123',
    author:    { id: 'author-1', displayName: 'Alice' },
    content:   'Hello world!',
    postType:  'Text',
    privacy:   'Friends',
    media:     [],
    isSavedByMe: false,
    createdAt: '2024-06-01T12:00:00Z',
    updatedAt: '2024-06-01T12:00:00Z',
    ...overrides,
  }
}

function mountModal(post: PostDto, open = true) {
  const pinia = createPinia()
  setActivePinia(pinia)

  return mount(ShareModal, {
    props:  { post, modelValue: open },
    global: {
      plugins: [pinia],
      stubs:   { RouterLink: RouterLinkStub, Teleport: true },
    },
    attachTo: document.body,
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ShareModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    friendsMock.friends = []
    friendsMock.loadingFriends = false
  })

  // ── Rendering ─────────────────────────────────────────────────────────────

  it('renders when modelValue is true', () => {
    const wrapper = mountModal(makePost())
    expect(wrapper.text()).toContain('Share post')
  })

  it('does not render when modelValue is false', () => {
    const wrapper = mountModal(makePost(), false)
    expect(wrapper.text()).not.toContain('Share post')
  })

  it('renders target type selector with three options', () => {
    const wrapper = mountModal(makePost())
    const select = wrapper.find('[aria-label="Share destination"]')
    expect(select.exists()).toBe(true)
    const options = select.findAll('option')
    expect(options).toHaveLength(3)
    expect(options[0].text()).toBe('My Wall')
    expect(options[1].text()).toBe("A Friend's Wall")
    expect(options[2].text()).toBe('A Company Page')
  })

  it('renders message textarea', () => {
    const wrapper = mountModal(makePost())
    const ta = wrapper.find('[aria-label="Share message"]')
    expect(ta.exists()).toBe(true)
  })

  it('renders Share and Cancel buttons', () => {
    const wrapper = mountModal(makePost())
    expect(wrapper.text()).toContain('Share')
    expect(wrapper.text()).toContain('Cancel')
  })

  // ── Target type switching ──────────────────────────────────────────────────

  it('shows friend picker when FriendWall is selected', async () => {
    const wrapper = mountModal(makePost())
    const select = wrapper.find('[aria-label="Share destination"]')
    await select.setValue('FriendWall')
    expect(wrapper.find('[aria-label="Select friend"]').exists()).toBe(true)
  })

  it('shows company page input when CompanyPage is selected', async () => {
    const wrapper = mountModal(makePost())
    const select = wrapper.find('[aria-label="Share destination"]')
    await select.setValue('CompanyPage')
    expect(wrapper.find('[aria-label="Company page ID"]').exists()).toBe(true)
  })

  it('hides friend picker when OwnWall is selected', async () => {
    const wrapper = mountModal(makePost())
    const select = wrapper.find('[aria-label="Share destination"]')
    await select.setValue('FriendWall')
    await select.setValue('OwnWall')
    expect(wrapper.find('[aria-label="Select friend"]').exists()).toBe(false)
  })

  // ── Validation ────────────────────────────────────────────────────────────

  it('Share button is enabled for OwnWall (no targetId needed)', () => {
    const wrapper = mountModal(makePost())
    const shareBtn = wrapper.findAll('button').find(b => b.text().includes('Share'))
    expect(shareBtn!.attributes('disabled')).toBeUndefined()
  })

  it('Share button is disabled when FriendWall is selected and no friend chosen', async () => {
    const wrapper = mountModal(makePost())
    const select = wrapper.find('[aria-label="Share destination"]')
    await select.setValue('FriendWall')
    const shareBtn = wrapper.findAll('button').find(b => b.text() === 'Share')
    expect(shareBtn!.attributes('disabled')).toBeDefined()
  })

  // ── Submission ────────────────────────────────────────────────────────────

  it('calls shareService.sharePost with OwnWall payload on submit', async () => {
    const sharedPostMock = {
      id:           'share-1',
      sharer:       { id: 'author-1', displayName: 'Alice' },
      targetType:   'OwnWall',
      originalPost: makePost(),
      createdAt:    '2024-06-01T12:00:00Z',
    }
    mockSharePost.mockResolvedValue(sharedPostMock)

    const wrapper = mountModal(makePost())
    const shareBtn = wrapper.findAll('button').find(b => b.text().includes('Share'))
    await shareBtn!.trigger('click')
    await flushPromises()

    expect(mockSharePost).toHaveBeenCalledWith('post-123', {
      targetType: 'OwnWall',
      targetId:   undefined,
      message:    undefined,
    })
  })

  it('includes message in payload when message is provided', async () => {
    const sharedPostMock = {
      id:           'share-2',
      sharer:       { id: 'author-1', displayName: 'Alice' },
      targetType:   'OwnWall',
      originalPost: makePost(),
      createdAt:    '2024-06-01T12:00:00Z',
    }
    mockSharePost.mockResolvedValue(sharedPostMock)

    const wrapper = mountModal(makePost())
    const ta = wrapper.find('[aria-label="Share message"]')
    await ta.setValue('Check this out!')

    const shareBtn = wrapper.findAll('button').find(b => b.text().includes('Share'))
    await shareBtn!.trigger('click')
    await flushPromises()

    expect(mockSharePost).toHaveBeenCalledWith('post-123', {
      targetType: 'OwnWall',
      targetId:   undefined,
      message:    'Check this out!',
    })
  })

  it('calls prependSharedPost after successful share', async () => {
    const sharedPostMock = {
      id:           'share-3',
      sharer:       { id: 'author-1', displayName: 'Alice' },
      targetType:   'OwnWall',
      originalPost: makePost(),
      createdAt:    '2024-06-01T12:00:00Z',
    }
    mockSharePost.mockResolvedValue(sharedPostMock)

    const wrapper = mountModal(makePost())
    const shareBtn = wrapper.findAll('button').find(b => b.text().includes('Share'))
    await shareBtn!.trigger('click')
    await flushPromises()

    expect(mockPrependSharedPost).toHaveBeenCalledWith(sharedPostMock)
  })

  it('emits update:modelValue false after successful share (closes modal)', async () => {
    const sharedPostMock = {
      id:           'share-4',
      sharer:       { id: 'author-1', displayName: 'Alice' },
      targetType:   'OwnWall',
      originalPost: makePost(),
      createdAt:    '2024-06-01T12:00:00Z',
    }
    mockSharePost.mockResolvedValue(sharedPostMock)

    const wrapper = mountModal(makePost())
    const shareBtn = wrapper.findAll('button').find(b => b.text().includes('Share'))
    await shareBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toContainEqual([false])
  })

  it('shows error message when sharePost fails', async () => {
    mockSharePost.mockRejectedValue({
      response: { data: { error: 'Cannot share this post.' } },
    })

    const wrapper = mountModal(makePost())
    const shareBtn = wrapper.findAll('button').find(b => b.text().includes('Share'))
    await shareBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Cannot share this post.')
  })

  // ── Close behaviour ───────────────────────────────────────────────────────

  it('emits update:modelValue false when Cancel is clicked', async () => {
    const wrapper = mountModal(makePost())
    const cancelBtn = wrapper.findAll('button').find(b => b.text() === 'Cancel')
    await cancelBtn!.trigger('click')
    expect(wrapper.emitted('update:modelValue')).toContainEqual([false])
  })

  it('calls loadFriends when modal opens with empty friend list', async () => {
    // modelValue starts false; open by re-mounting with true
    const wrapper = mountModal(makePost(), false)
    await wrapper.setProps({ modelValue: true })
    await flushPromises()
    expect(mockLoadFriends).toHaveBeenCalledWith(true)
  })

  it('lists friends in the dropdown when FriendWall is selected', async () => {
    friendsMock.friends = [
      { user: { id: 'bob-1', displayName: 'Bob' } },
      { user: { id: 'carol-2', displayName: 'Carol' } },
    ]

    const wrapper = mountModal(makePost())
    const select = wrapper.find('[aria-label="Share destination"]')
    await select.setValue('FriendWall')

    const friendSelect = wrapper.find('[aria-label="Select friend"]')
    expect(friendSelect.text()).toContain('Bob')
    expect(friendSelect.text()).toContain('Carol')
  })
})
