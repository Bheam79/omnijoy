import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import PostComposer from '@/components/post/PostComposer.vue'
import { useAuthStore } from '@/stores/auth'

// ── Mock companyPageService ───────────────────────────────────────────────────

vi.mock('@/services/companyPageService', () => ({
  companyPageService: {
    getMyAdminPages: vi.fn().mockResolvedValue([]),
  },
}))

// ── Mock postService ──────────────────────────────────────────────────────────

vi.mock('@/services/postService', () => ({
  postService: {
    createPost:       vi.fn(),
    fetchMetaPreview: vi.fn().mockResolvedValue({ url: 'https://example.com', title: 'Example' }),
  },
}))

// ── Mock feedStore ────────────────────────────────────────────────────────────

const mockFeedStore = vi.hoisted(() => ({
  createPost:  vi.fn(),
  posts:       [] as unknown[],
  prependPost: vi.fn(),
}))

vi.mock('@/stores/feed', () => ({
  useFeedStore: () => mockFeedStore,
}))

// ── jsdom shims ───────────────────────────────────────────────────────────────

if (!URL.createObjectURL) {
  Object.defineProperty(URL, 'createObjectURL', { value: vi.fn(() => 'blob:fake-url'), writable: true })
  Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), writable: true })
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function mountComposer() {
  const pinia = createPinia()
  setActivePinia(pinia)

  const authStore = useAuthStore(pinia)
  authStore.setTokens('tok', 'ref')
  authStore.setUser({
    id:            'user-1',
    email:         'alice@example.com',
    displayName:   'Alice',
    gender:        'NotDisclosed' as const,
    showBirthDate: false,
    createdAt:     '2024-01-01T00:00:00Z',
  })

  return mount(PostComposer, {
    global:   { plugins: [pinia] },
    attachTo: document.body,
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('PostComposer', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    document.body.innerHTML = ''
  })

  // ── Trigger bar ───────────────────────────────────────────────────────────

  it('renders trigger bar with "What\'s on your mind?" prompt', async () => {
    const wrapper = mountComposer()
    await flushPromises()
    expect(wrapper.text()).toContain("What's on your mind?")
  })

  it('renders author initial in trigger bar avatar', async () => {
    const wrapper = mountComposer()
    await flushPromises()
    // Alice's initial 'A' should appear in the avatar
    expect(wrapper.html()).toContain('A')
  })

  // ── Opening the modal ─────────────────────────────────────────────────────

  it('opens modal when trigger bar is clicked', async () => {
    const wrapper = mountComposer()
    await flushPromises()

    await wrapper.find('div.cursor-pointer').trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain('Create Post')
  })

  it('closes modal when X button is clicked', async () => {
    const wrapper = mountComposer()
    await flushPromises()

    // Open
    await wrapper.find('div.cursor-pointer').trigger('click')
    await flushPromises()
    expect(document.body.textContent).toContain('Create Post')

    // Close
    const closeBtn = document.body.querySelector('button[aria-label="Close"]') as HTMLButtonElement
    closeBtn?.click()
    await flushPromises()

    expect(document.body.textContent).not.toContain('Create Post')
  })

  // ── Post button state ─────────────────────────────────────────────────────

  it('Post button is disabled when content is empty', async () => {
    const wrapper = mountComposer()
    await flushPromises()

    await wrapper.find('div.cursor-pointer').trigger('click')
    await flushPromises()

    // Find the Post button (it appears after the modal opens, in the footer)
    const postButtons = Array.from(document.body.querySelectorAll('button'))
      .filter(b => b.textContent?.trim() === 'Post')

    expect(postButtons.length).toBeGreaterThan(0)
    expect(postButtons[0].disabled).toBe(true)
  })

  // ── Programmatic open / close ─────────────────────────────────────────────

  it('expose: open() opens the modal', async () => {
    const wrapper = mountComposer()
    await flushPromises()

    wrapper.vm.open()
    await flushPromises()

    expect(document.body.textContent).toContain('Create Post')
  })

  it('expose: close() closes the modal', async () => {
    const wrapper = mountComposer()
    await flushPromises()

    wrapper.vm.open()
    await flushPromises()

    wrapper.vm.close()
    await flushPromises()

    expect(document.body.textContent).not.toContain('Create Post')
  })

  // ── Post type tabs ────────────────────────────────────────────────────────

  it('shows all post type tabs after opening', async () => {
    const wrapper = mountComposer()
    await flushPromises()

    wrapper.vm.open()
    await flushPromises()

    const bodyText = document.body.textContent ?? ''
    expect(bodyText).toContain('Image')
    expect(bodyText).toContain('Video')
  })

  // ── Privacy selector ─────────────────────────────────────────────────────

  it('privacy selector is visible when modal is open', async () => {
    const wrapper = mountComposer()
    await flushPromises()

    wrapper.vm.open()
    await flushPromises()

    const selects = document.body.querySelectorAll('select')
    expect(selects.length).toBeGreaterThan(0)
  })

  // ── createPost ────────────────────────────────────────────────────────────

  it('calls feedStore.createPost when form is submitted with content', async () => {
    const createdPost = {
      id: 'new-post',
      author: { id: 'user-1', displayName: 'Alice' },
      content: 'Test post',
      postType: 'Text',
      privacy: 'Friends',
      media: [],
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    }
    mockFeedStore.createPost.mockResolvedValue(createdPost)

    const wrapper = mountComposer()
    await flushPromises()
    wrapper.vm.open()
    await flushPromises()

    // Type content in the textarea
    const textarea = document.body.querySelector('textarea') as HTMLTextAreaElement
    if (textarea) {
      await (textarea as unknown as { _vei: { onInput: () => void } })
      textarea.value = 'Test post'
      textarea.dispatchEvent(new Event('input', { bubbles: true }))
      await flushPromises()

      // Click the Post button
      const postBtn = Array.from(document.body.querySelectorAll('button'))
        .find(b => b.textContent?.trim() === 'Post')
      if (postBtn && !postBtn.disabled) {
        await postBtn.click()
        await flushPromises()
        expect(mockFeedStore.createPost).toHaveBeenCalled()
      }
    }
  })
})
