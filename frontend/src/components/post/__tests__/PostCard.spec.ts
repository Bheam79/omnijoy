import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { RouterLinkStub } from '@vue/test-utils'
import PostCard from '@/components/post/PostCard.vue'
import { useAuthStore } from '@/stores/auth'

// ── Mock feedStore ────────────────────────────────────────────────────────────

vi.mock('@/stores/feed', () => ({
  useFeedStore: () => ({
    deletePost: vi.fn().mockResolvedValue(undefined),
  }),
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makePost(overrides: Record<string, unknown> = {}) {
  return {
    id:        'post-1',
    author:    { id: 'author-1', displayName: 'Alice' },
    content:   'Hello world!',
    postType:  'Text' as const,
    privacy:   'Friends' as const,
    media:     [],
    createdAt: '2024-06-01T12:00:00Z',
    updatedAt: '2024-06-01T12:00:00Z',
    ...overrides,
  }
}

function mountCard(post: ReturnType<typeof makePost>, currentUserId = 'other-user') {
  const pinia = createPinia()
  setActivePinia(pinia)

  const authStore = useAuthStore(pinia)
  authStore.setTokens('tok', 'ref')
  authStore.setUser({
    id:            currentUserId,
    email:         'user@example.com',
    displayName:   'Current User',
    gender:        'NotDisclosed' as const,
    showBirthDate: false,
    createdAt:     '2024-01-01T00:00:00Z',
  })

  return mount(PostCard, {
    props:  { post },
    global: {
      plugins: [pinia],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('PostCard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    Object.defineProperty(navigator, 'clipboard', {
      value:    { writeText: vi.fn().mockResolvedValue(undefined) },
      writable: true,
    })
  })

  // ── Rendering ─────────────────────────────────────────────────────────────

  it('renders author display name', () => {
    const wrapper = mountCard(makePost())
    expect(wrapper.text()).toContain('Alice')
  })

  it('renders post content for Text post', () => {
    const wrapper = mountCard(makePost({ content: 'This is my post content' }))
    expect(wrapper.text()).toContain('This is my post content')
  })

  it('renders the Share button', () => {
    const wrapper = mountCard(makePost())
    const buttons = wrapper.findAll('button')
    const shareButton = buttons.find(b => b.text().includes('Share'))
    expect(shareButton).toBeTruthy()
  })

  it('renders the Like button', () => {
    const wrapper = mountCard(makePost())
    expect(wrapper.text()).toContain('Like')
  })

  it('renders the Comment button', () => {
    const wrapper = mountCard(makePost())
    expect(wrapper.text()).toContain('Comment')
  })

  // ── Own vs. others' posts ─────────────────────────────────────────────────

  it('shows options button (three dots) for own post', () => {
    const post = makePost({ author: { id: 'me', displayName: 'Me' } })
    const wrapper = mountCard(post, 'me')

    const optionsButton = wrapper.find('button[aria-label="Post options"]')
    expect(optionsButton.exists()).toBe(true)
  })

  it('hides options button for other users posts', () => {
    const post = makePost({ author: { id: 'someone-else', displayName: 'Someone Else' } })
    const wrapper = mountCard(post, 'me')

    const optionsButton = wrapper.find('button[aria-label="Post options"]')
    expect(optionsButton.exists()).toBe(false)
  })

  // ── Privacy badge ─────────────────────────────────────────────────────────

  it('shows privacy label for own post', () => {
    const post = makePost({ author: { id: 'me', displayName: 'Me' }, privacy: 'Friends' })
    const wrapper = mountCard(post, 'me')
    expect(wrapper.text()).toContain('Friends')
  })

  // ── Share link ────────────────────────────────────────────────────────────

  it('copies share link to clipboard on Share click', async () => {
    const wrapper = mountCard(makePost())
    const buttons  = wrapper.findAll('button')
    const shareBtn = buttons.find(b => b.text().includes('Share'))
    await shareBtn!.trigger('click')
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
      expect.stringContaining('/share/posts/post-1'),
    )
  })

  // ── TextOnBackground post ─────────────────────────────────────────────────

  it('renders TextOnBackground content with styled container', () => {
    const post = makePost({
      postType: 'TextOnBackground' as const,
      content:  'Styled text',
    })
    const wrapper = mountCard(post)
    expect(wrapper.text()).toContain('Styled text')
  })

  // ── Image post ────────────────────────────────────────────────────────────

  it('renders image media for Image post', () => {
    const post = makePost({
      postType: 'Image' as const,
      media:    [{ id: 'm1', mediaType: 'Image', url: '/img.jpg', order: 0 }],
    })
    const wrapper = mountCard(post)
    const img = wrapper.find('img[alt="Image 1"]')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('/img.jpg')
  })

  // ── Link preview ──────────────────────────────────────────────────────────

  it('renders link preview card when post has linkPreview', () => {
    const post = makePost({
      linkPreview: {
        url:   'https://example.com',
        title: 'Example Site',
      },
    })
    const wrapper = mountCard(post)
    expect(wrapper.text()).toContain('Example Site')
  })
})
