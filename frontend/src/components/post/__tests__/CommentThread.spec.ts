import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { RouterLinkStub } from '@vue/test-utils'
import { reactive } from 'vue'
import CommentThread from '@/components/post/CommentThread.vue'
import { useAuthStore } from '@/stores/auth'
import type { CommentDto } from '@/services/commentService'

// ── Mock commentsStore ────────────────────────────────────────────────────────

// Use reactive() so that direct mutations to nested arrays are tracked by Vue
const mockCommentsState = reactive<Record<string, {
  items: CommentDto[]
  page: number
  hasMore: boolean
  loading: boolean
  replies: Record<string, CommentDto[]>
  repliesLoading: Record<string, boolean>
}>>({})

const mockStore = {
  byPost: mockCommentsState,
  loadComments: vi.fn().mockResolvedValue(undefined),
  loadMore: vi.fn().mockResolvedValue(undefined),
  loadReplies: vi.fn().mockResolvedValue(undefined),
  createComment: vi.fn().mockResolvedValue(undefined),
  updateComment: vi.fn().mockResolvedValue(undefined),
  deleteComment: vi.fn().mockResolvedValue(undefined),
  applyNewComment: vi.fn(),
  ensurePost: vi.fn(),
}

vi.mock('@/stores/comments', () => ({
  useCommentsStore: () => mockStore,
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeComment(overrides: Partial<CommentDto> = {}): CommentDto {
  return {
    id: `comment-${Math.random().toString(36).slice(2)}`,
    postId: 'post-1',
    author: { id: 'author-1', displayName: 'Alice' },
    parentCommentId: null,
    content: 'Test comment',
    replyCount: 0,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    isDeleted: false,
    ...overrides,
  }
}

function setPostState(postId: string, partial: Partial<typeof mockCommentsState[string]> = {}) {
  mockCommentsState[postId] = {
    items: [],
    page: 1,
    hasMore: false,
    loading: false,
    replies: {},
    repliesLoading: {},
    ...partial,
  }
}

function mountThread(propsOverrides: Record<string, unknown> = {}, loggedIn = true) {
  const pinia = createPinia()
  setActivePinia(pinia)

  if (loggedIn) {
    const authStore = useAuthStore(pinia)
    authStore.setTokens('tok', 'ref')
    authStore.setUser({
      id: 'current-user',
      email: 'user@test.com',
      displayName: 'Current User',
      gender: 'NotDisclosed' as const,
      showBirthDate: false,
      createdAt: '2024-01-01T00:00:00Z',
    })
  }

  return mount(CommentThread, {
    props: {
      postId: 'post-1',
      ...propsOverrides,
    },
    global: {
      plugins: [pinia],
      stubs: { RouterLink: RouterLinkStub },
    },
    attachTo: document.body,
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('CommentThread', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    document.body.innerHTML = ''
    // Clear localStorage so auth state doesn't persist between tests
    localStorage.clear()
    // Clear reactive store state
    for (const key in mockCommentsState) {
      delete mockCommentsState[key]
    }
  })

  // ── Collapsed state ───────────────────────────────────────────────────────

  it('renders a toggle button in collapsed state', () => {
    const wrapper = mountThread()
    const toggle = wrapper.find('[data-testid="toggle-thread"]')
    expect(toggle.exists()).toBe(true)
  })

  it('shows "Comments" label when collapsed and no comments loaded', () => {
    const wrapper = mountThread()
    expect(wrapper.text()).toContain('Comments')
  })

  it('shows comment count label when comments are loaded', () => {
    setPostState('post-1', { items: [makeComment(), makeComment()] })
    const wrapper = mountThread()
    expect(wrapper.text()).toContain('2 comments')
  })

  it('does not show expanded panel when collapsed', () => {
    const wrapper = mountThread()
    expect(wrapper.find('[data-testid="empty-comments"]').exists()).toBe(false)
  })

  // ── Expanding ─────────────────────────────────────────────────────────────

  it('expands on toggle click and calls loadComments if not loaded', async () => {
    const wrapper = mountThread()
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()
    expect(mockStore.loadComments).toHaveBeenCalledWith('post-1')
  })

  it('does not call loadComments if already loaded', async () => {
    setPostState('post-1', { items: [makeComment()], loading: false })
    const wrapper = mountThread()
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()
    expect(mockStore.loadComments).not.toHaveBeenCalled()
  })

  it('shows "Hide comments" label when expanded', async () => {
    setPostState('post-1', { items: [makeComment()] })
    const wrapper = mountThread()
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Hide comments')
  })

  // ── Empty state ───────────────────────────────────────────────────────────

  it('shows empty state message when no comments', async () => {
    setPostState('post-1', { items: [], loading: false })
    const wrapper = mountThread()
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="empty-comments"]').exists()).toBe(true)
  })

  // ── Comments list ─────────────────────────────────────────────────────────

  it('renders comment items when comments exist', async () => {
    setPostState('post-1', {
      items: [makeComment({ content: 'First comment' }), makeComment({ content: 'Second comment' })],
    })
    const wrapper = mountThread()
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('First comment')
    expect(wrapper.text()).toContain('Second comment')
  })

  // ── Composer ──────────────────────────────────────────────────────────────

  it('shows CommentComposer for logged-in users when expanded', async () => {
    setPostState('post-1', { items: [] })
    const wrapper = mountThread()
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()
    // CommentComposer has a textarea
    expect(wrapper.find('[data-testid="comment-input"]').exists()).toBe(true)
  })

  it('hides CommentComposer for anonymous users', async () => {
    setPostState('post-1', { items: [] })
    const wrapper = mountThread({}, false) // not logged in
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="comment-input"]').exists()).toBe(false)
  })

  it('calls createComment when CommentComposer emits submitted', async () => {
    setPostState('post-1', { items: [] })
    const wrapper = mountThread()
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()

    const textarea = wrapper.find('[data-testid="comment-input"]')
    await textarea.setValue('New comment')
    await wrapper.find('[data-testid="comment-submit"]').trigger('click')
    await flushPromises()

    expect(mockStore.createComment).toHaveBeenCalledWith('post-1', { content: 'New comment' })
  })

  // ── Load more ─────────────────────────────────────────────────────────────

  it('shows Load more button when hasMore is true', async () => {
    setPostState('post-1', { items: [makeComment()], hasMore: true })
    const wrapper = mountThread()
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="load-more"]').exists()).toBe(true)
  })

  it('hides Load more button when hasMore is false', async () => {
    setPostState('post-1', { items: [makeComment()], hasMore: false })
    const wrapper = mountThread()
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="load-more"]').exists()).toBe(false)
  })

  it('calls loadMore when Load more button is clicked', async () => {
    setPostState('post-1', { items: [makeComment()], hasMore: true })
    const wrapper = mountThread()
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="load-more"]').trigger('click')
    expect(mockStore.loadMore).toHaveBeenCalledWith('post-1')
  })

  // ── Real-time new comment ─────────────────────────────────────────────────

  it('new comment appears after applyNewComment is called on the store', async () => {
    const initialComment = makeComment({ content: 'Initial' })
    setPostState('post-1', { items: [initialComment] })

    const wrapper = mountThread()
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()

    // Simulate SignalR pushing a new comment via the store
    const newComment = makeComment({ content: 'Real-time comment' })
    mockCommentsState['post-1'].items.unshift(newComment)
    await flushPromises()

    expect(wrapper.text()).toContain('Real-time comment')
  })

  // ── v-model:expanded ─────────────────────────────────────────────────────

  it('emits update:expanded=true when opened', async () => {
    const wrapper = mountThread()
    await wrapper.find('[data-testid="toggle-thread"]').trigger('click')
    await flushPromises()
    expect(wrapper.emitted('update:expanded')).toBeTruthy()
    expect(wrapper.emitted('update:expanded')![0]).toEqual([true])
  })

  it('emits update:expanded=false when closed again', async () => {
    setPostState('post-1', { items: [] })
    const wrapper = mountThread()
    const toggle = wrapper.find('[data-testid="toggle-thread"]')
    await toggle.trigger('click')
    await flushPromises()
    await toggle.trigger('click')
    await flushPromises()
    const emitted = wrapper.emitted('update:expanded')!
    expect(emitted[emitted.length - 1]).toEqual([false])
  })

  // ── Expose: openThread() ──────────────────────────────────────────────────

  it('expose: openThread() expands the thread', async () => {
    setPostState('post-1', { items: [] })
    const wrapper = mountThread()
    await (wrapper.vm as unknown as { openThread: () => Promise<void> }).openThread()
    await flushPromises()
    expect(wrapper.find('[data-testid="empty-comments"]').exists()).toBe(true)
  })
})
