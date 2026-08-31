import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { RouterLinkStub } from '@vue/test-utils'
import { reactive } from 'vue'
import type { CommentDto } from '@/services/commentService'

// ── Auth store mock ────────────────────────────────────────────────────────────

const mockAuth = vi.hoisted(() => ({
  user: { id: 'user-1', displayName: 'Alice', avatarUrl: undefined } as
    | { id: string; displayName: string; avatarUrl?: string }
    | null,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuth,
}))

// ── Comments store mock ───────────────────────────────────────────────────────

const mockByPost = reactive<Record<string, {
  items: CommentDto[]
  page: number
  hasMore: boolean
  loading: boolean
  replies: Record<string, CommentDto[]>
  repliesLoading: Record<string, boolean>
}>>({})

const mockCommentsStore = vi.hoisted(() => ({
  loadReplies:   vi.fn().mockResolvedValue(undefined),
  createComment: vi.fn().mockResolvedValue(undefined),
  updateComment: vi.fn().mockResolvedValue(undefined),
  deleteComment: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/stores/comments', () => ({
  useCommentsStore: () => ({
    byPost:        mockByPost,
    loadReplies:   mockCommentsStore.loadReplies,
    createComment: mockCommentsStore.createComment,
    updateComment: mockCommentsStore.updateComment,
    deleteComment: mockCommentsStore.deleteComment,
  }),
}))

// ── vue-router mock ───────────────────────────────────────────────────────────

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>()
  return { ...actual, RouterLink: RouterLinkStub }
})

// ── CommentComposer stub ──────────────────────────────────────────────────────

vi.mock('@/components/post/CommentComposer.vue', () => ({
  default: {
    name:     'CommentComposer',
    emits:    ['submitted', 'cancelled'],
    template: '<div data-testid="comment-composer-stub" />',
  },
}))

// Import after mocks
import CommentItem from '../CommentItem.vue'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeComment(overrides: Partial<CommentDto> = {}): CommentDto {
  return {
    id:              'comment-1',
    postId:          'post-1',
    author:          { id: 'user-2', displayName: 'Bob' },
    parentCommentId: null,
    content:         'Hello, world!',
    mentions:        [],
    replyCount:      0,
    createdAt:       new Date(Date.now() - 60_000).toISOString(), // 1 min ago
    updatedAt:       new Date().toISOString(),
    isDeleted:       false,
    ...overrides,
  }
}

function setPostState(postId: string, replies: Record<string, CommentDto[]> = {}) {
  mockByPost[postId] = {
    items:          [],
    page:           1,
    hasMore:        false,
    loading:        false,
    replies,
    repliesLoading: {},
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountItem(comment: CommentDto, isReply = false) {
  return mount(CommentItem, {
    props:  { comment, isReply },
    global: {
      plugins: [createPinia()],
      stubs:   {
        RouterLink: RouterLinkStub,
        Teleport:   { template: '<div><slot /></div>' },
      },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('CommentItem', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    // Clean up reactive store state
    for (const key of Object.keys(mockByPost)) delete mockByPost[key]
    mockAuth.user = { id: 'user-1', displayName: 'Alice' }
    vi.spyOn(window, 'confirm').mockReturnValue(true)
  })

  // ── Basic rendering ───────────────────────────────────────────────────────

  it('renders the author display name', () => {
    const wrapper = mountItem(makeComment())

    expect(wrapper.text()).toContain('Bob')
  })

  it('renders the comment body text', () => {
    const wrapper = mountItem(makeComment({ content: 'Test comment content' }))

    expect(wrapper.text()).toContain('Test comment content')
  })

  it('renders only persisted comment mentions as profile links', () => {
    const wrapper = mountItem(makeComment({
      content: '@bob-old and @unresolved',
      mentions: [{
        matchedSlug: 'bob-old',
        userId: 'mentioned-2',
        displayName: 'Bob Mentioned',
        urlSlug: 'bob-current',
      }],
    }))
    const mentionLink = wrapper.findAllComponents(RouterLinkStub)
      .find(link => link.text() === '@bob-old')

    expect(mentionLink?.props('to')).toBe('/bob-current')
    expect(wrapper.text()).toContain('@unresolved')
  })

  it('renders a formatted relative timestamp (1 min ago → "1m")', () => {
    const wrapper = mountItem(makeComment())

    expect(wrapper.text()).toContain('1m')
  })

  it('renders "just now" for very recent comments', () => {
    const wrapper = mountItem(makeComment({
      createdAt: new Date(Date.now() - 5_000).toISOString(),
    }))

    expect(wrapper.text()).toContain('just now')
  })

  it('has data-testid="comment-item" on the root element', () => {
    const wrapper = mountItem(makeComment())

    expect(wrapper.find('[data-testid="comment-item"]').exists()).toBe(true)
  })

  // ── Deleted comment ───────────────────────────────────────────────────────

  it('does not render author name for deleted comments', () => {
    const wrapper = mountItem(makeComment({ isDeleted: true, content: '' }))

    // RouterLink with author name not shown — check Bob is not present
    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links.every(l => !l.text().includes('Bob'))).toBe(true)
  })

  it('hides Edit, Delete and Reply buttons for deleted comments', () => {
    const wrapper = mountItem(makeComment({ isDeleted: true, content: '' }))

    expect(wrapper.find('[data-testid="edit-button"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="delete-button"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="reply-button"]').exists()).toBe(false)
  })

  // ── Avatar ────────────────────────────────────────────────────────────────

  it('renders initials avatar when author has no avatarUrl', () => {
    const wrapper = mountItem(makeComment({ author: { id: 'user-2', displayName: 'Bob' } }))

    // First letter of 'Bob'
    expect(wrapper.text()).toContain('B')
    expect(wrapper.find('img').exists()).toBe(false)
  })

  it('renders avatar img when author has avatarUrl', () => {
    const wrapper = mountItem(makeComment({
      author: { id: 'user-2', displayName: 'Bob', avatarUrl: 'https://cdn.example.com/bob.jpg' },
    }))

    const img = wrapper.find('img')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('https://cdn.example.com/bob.jpg')
  })

  // ── Own vs other comments — Edit / Delete visibility ─────────────────────

  it('shows Edit and Delete buttons for own comments', () => {
    // auth user is user-1; make comment authored by user-1
    const wrapper = mountItem(makeComment({ author: { id: 'user-1', displayName: 'Alice' } }))

    expect(wrapper.find('[data-testid="edit-button"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="delete-button"]').exists()).toBe(true)
  })

  it('hides Edit and Delete buttons for other users\' comments', () => {
    // auth user is user-1; comment authored by user-2
    const wrapper = mountItem(makeComment({ author: { id: 'user-2', displayName: 'Bob' } }))

    expect(wrapper.find('[data-testid="edit-button"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="delete-button"]').exists()).toBe(false)
  })

  it('hides Edit and Delete when user is not logged in', () => {
    mockAuth.user = null
    const wrapper = mountItem(makeComment({ author: { id: 'user-2', displayName: 'Bob' } }))

    expect(wrapper.find('[data-testid="edit-button"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="delete-button"]').exists()).toBe(false)
  })

  // ── Edit mode ─────────────────────────────────────────────────────────────

  it('clicking Edit button shows the edit textarea pre-filled with comment text', async () => {
    const wrapper = mountItem(makeComment({
      author: { id: 'user-1', displayName: 'Alice' },
      content: 'Original text',
    }))

    await wrapper.find('[data-testid="edit-button"]').trigger('click')

    const textarea = wrapper.find('[data-testid="edit-input"]')
    expect(textarea.exists()).toBe(true)
    expect((textarea.element as HTMLTextAreaElement).value).toBe('Original text')
  })

  it('Edit mode hides the Edit button', async () => {
    const wrapper = mountItem(makeComment({ author: { id: 'user-1', displayName: 'Alice' } }))

    await wrapper.find('[data-testid="edit-button"]').trigger('click')

    expect(wrapper.find('[data-testid="edit-button"]').exists()).toBe(false)
  })

  it('clicking Save calls commentsStore.updateComment with updated text', async () => {
    const wrapper = mountItem(makeComment({
      id:      'comment-1',
      postId:  'post-1',
      author:  { id: 'user-1', displayName: 'Alice' },
      content: 'Original text',
    }))

    await wrapper.find('[data-testid="edit-button"]').trigger('click')

    const textarea = wrapper.find('[data-testid="edit-input"]')
    await textarea.setValue('Edited text')

    await wrapper.find('[data-testid="edit-save"]').trigger('click')
    await flushPromises()

    expect(mockCommentsStore.updateComment).toHaveBeenCalledOnce()
    expect(mockCommentsStore.updateComment).toHaveBeenCalledWith('post-1', 'comment-1', 'Edited text')
  })

  it('Save button is disabled when edit textarea is empty', async () => {
    const wrapper = mountItem(makeComment({ author: { id: 'user-1', displayName: 'Alice' } }))

    await wrapper.find('[data-testid="edit-button"]').trigger('click')
    await wrapper.find('[data-testid="edit-input"]').setValue('')

    const saveBtn = wrapper.find('[data-testid="edit-save"]')
    expect(saveBtn.attributes('disabled')).toBeDefined()
  })

  it('clicking Cancel exits edit mode without calling updateComment', async () => {
    const wrapper = mountItem(makeComment({ author: { id: 'user-1', displayName: 'Alice' } }))

    await wrapper.find('[data-testid="edit-button"]').trigger('click')

    // Click Cancel
    const buttons = wrapper.findAll('button')
    const cancelBtn = buttons.find(b => b.text() === 'Cancel')
    expect(cancelBtn).toBeDefined()
    await cancelBtn!.trigger('click')

    expect(mockCommentsStore.updateComment).not.toHaveBeenCalled()
    // Edit mode is closed
    expect(wrapper.find('[data-testid="edit-input"]').exists()).toBe(false)
  })

  // ── Delete ────────────────────────────────────────────────────────────────

  it('clicking Delete shows confirm dialog and calls commentsStore.deleteComment', async () => {
    const wrapper = mountItem(makeComment({
      id:     'comment-1',
      postId: 'post-1',
      author: { id: 'user-1', displayName: 'Alice' },
    }))

    await wrapper.find('[data-testid="delete-button"]').trigger('click')
    await flushPromises()

    expect(window.confirm).toHaveBeenCalledWith('Delete this comment?')
    expect(mockCommentsStore.deleteComment).toHaveBeenCalledWith('post-1', 'comment-1')
  })

  it('does NOT call deleteComment when confirm is cancelled', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    const wrapper = mountItem(makeComment({ author: { id: 'user-1', displayName: 'Alice' } }))

    await wrapper.find('[data-testid="delete-button"]').trigger('click')
    await flushPromises()

    expect(mockCommentsStore.deleteComment).not.toHaveBeenCalled()
  })

  // ── Reply button ──────────────────────────────────────────────────────────

  it('renders the Reply button for top-level comments when user is logged in', () => {
    const wrapper = mountItem(makeComment({ replyCount: 0 }), false)

    expect(wrapper.find('[data-testid="reply-button"]').exists()).toBe(true)
  })

  it('does not render the Reply button when isReply=true', () => {
    const wrapper = mountItem(makeComment(), true)

    expect(wrapper.find('[data-testid="reply-button"]').exists()).toBe(false)
  })

  it('does not render the Reply button when user is not logged in', () => {
    mockAuth.user = null
    const wrapper = mountItem(makeComment(), false)

    expect(wrapper.find('[data-testid="reply-button"]').exists()).toBe(false)
  })

  it('clicking the Reply button shows the reply composer', async () => {
    const wrapper = mountItem(makeComment(), false)

    await wrapper.find('[data-testid="reply-button"]').trigger('click')

    expect(wrapper.find('[data-testid="comment-composer-stub"]').exists()).toBe(true)
  })

  it('clicking Reply again toggles the composer off', async () => {
    const wrapper = mountItem(makeComment(), false)

    await wrapper.find('[data-testid="reply-button"]').trigger('click')
    await wrapper.find('[data-testid="reply-button"]').trigger('click')

    expect(wrapper.find('[data-testid="comment-composer-stub"]').exists()).toBe(false)
  })

  // ── Show replies toggle ───────────────────────────────────────────────────

  it('renders the "Show N replies" toggle when replyCount > 0 and not a reply', () => {
    const wrapper = mountItem(makeComment({ replyCount: 3 }), false)

    const toggleBtn = wrapper.find('[data-testid="toggle-replies"]')
    expect(toggleBtn.exists()).toBe(true)
    expect(toggleBtn.text()).toContain('Show 3 replies')
  })

  it('uses singular "reply" when replyCount is 1', () => {
    const wrapper = mountItem(makeComment({ replyCount: 1 }), false)

    expect(wrapper.find('[data-testid="toggle-replies"]').text()).toContain('Show 1 reply')
  })

  it('does not show replies toggle when replyCount is 0', () => {
    const wrapper = mountItem(makeComment({ replyCount: 0 }), false)

    expect(wrapper.find('[data-testid="toggle-replies"]').exists()).toBe(false)
  })

  it('does not show replies toggle when isReply=true', () => {
    const wrapper = mountItem(makeComment({ replyCount: 5 }), true)

    expect(wrapper.find('[data-testid="toggle-replies"]').exists()).toBe(false)
  })

  // ── Nested replies rendering ──────────────────────────────────────────────

  it('renders nested CommentItem components when replies are loaded', async () => {
    const reply1 = makeComment({ id: 'reply-1', content: 'First reply', replyCount: 0 })
    const reply2 = makeComment({ id: 'reply-2', content: 'Second reply', replyCount: 0 })
    setPostState('post-1', { 'comment-1': [reply1, reply2] })

    const comment = makeComment({ id: 'comment-1', replyCount: 2 })
    const wrapper = mountItem(comment, false)

    // Click toggle to show replies (they are already loaded in the store)
    await wrapper.find('[data-testid="toggle-replies"]').trigger('click')
    await flushPromises()

    // Both replies should be rendered as nested CommentItem components
    const nestedItems = wrapper.findAll('[data-testid="comment-item"]')
    // One for the parent + two nested replies
    expect(nestedItems.length).toBeGreaterThanOrEqual(3)
    expect(wrapper.text()).toContain('First reply')
    expect(wrapper.text()).toContain('Second reply')
  })

  it('calls loadReplies when toggle is clicked and replies are not yet loaded', async () => {
    // No replies loaded in store yet
    setPostState('post-1', {})

    const comment = makeComment({ id: 'comment-1', replyCount: 2 })
    const wrapper = mountItem(comment, false)

    await wrapper.find('[data-testid="toggle-replies"]').trigger('click')
    await flushPromises()

    expect(mockCommentsStore.loadReplies).toHaveBeenCalledWith('post-1', 'comment-1')
  })

  it('shows "Hide replies" after expanding', async () => {
    setPostState('post-1', { 'comment-1': [] })

    const comment = makeComment({ id: 'comment-1', replyCount: 2 })
    const wrapper = mountItem(comment, false)

    await wrapper.find('[data-testid="toggle-replies"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="toggle-replies"]').text()).toContain('Hide replies')
  })
})
