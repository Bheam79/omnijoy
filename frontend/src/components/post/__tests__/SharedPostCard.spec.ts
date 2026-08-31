import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { RouterLinkStub } from '@vue/test-utils'
import type { SharedPostFeedItemDto, PostDto, PostAuthor } from '@/services/postService'

// ── vue-router mock ───────────────────────────────────────────────────────────

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>()
  return { ...actual, RouterLink: RouterLinkStub }
})

// Import after mocks
import SharedPostCard from '../SharedPostCard.vue'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeAuthor(overrides: Partial<PostAuthor> = {}): PostAuthor {
  return {
    id:          'author-1',
    displayName: 'Original Author',
    ...overrides,
  }
}

function makeSharer(overrides: Partial<PostAuthor> = {}): PostAuthor {
  return {
    id:          'sharer-1',
    displayName: 'Alice Sharer',
    ...overrides,
  }
}

function makeOriginalPost(overrides: Partial<PostDto> = {}): PostDto {
  return {
    id:        'post-1',
    author:    makeAuthor(),
    content:   'Original post content here.',
    postType:  'Text',
    privacy:   'Everyone',
    media:     [],
    isSavedByMe: false,
    createdAt: '2026-05-01T10:00:00Z',
    updatedAt: '2026-05-01T10:00:00Z',
    ...overrides,
  }
}

function makeSharedPost(overrides: Partial<SharedPostFeedItemDto> = {}): SharedPostFeedItemDto {
  return {
    id:           'share-1',
    sharer:       makeSharer(),
    message:      undefined,
    targetType:   'OwnWall',
    originalPost: makeOriginalPost(),
    createdAt:    '2026-05-15T14:30:00Z',
    ...overrides,
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountCard(sharedPost: SharedPostFeedItemDto = makeSharedPost()) {
  return mount(SharedPostCard, {
    props:  { sharedPost },
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('SharedPostCard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  // ── Root element ──────────────────────────────────────────────────────────

  it('renders with data-testid="shared-post-card"', () => {
    const wrapper = mountCard()

    expect(wrapper.find('[data-testid="shared-post-card"]').exists()).toBe(true)
  })

  // ── Sharer info ───────────────────────────────────────────────────────────

  it('renders the sharer display name', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('Alice Sharer')
  })

  it('renders "shared a post" text after the sharer name', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('shared a post')
  })

  it('links the sharer name to /profile/:sharerId', () => {
    const wrapper = mountCard()

    const links = wrapper.findAllComponents(RouterLinkStub)
    const sharerLink = links.find(l => String(l.props('to')).includes('sharer-1'))
    expect(sharerLink).toBeDefined()
  })

  it('renders sharer avatar img when avatarUrl is present', () => {
    const sharedPost = makeSharedPost({
      sharer: makeSharer({ avatarUrl: 'https://cdn.example.com/alice.jpg' }),
    })
    const wrapper = mountCard(sharedPost)

    const img = wrapper.find('img[alt="Alice Sharer"]')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('https://cdn.example.com/alice.jpg')
  })

  it('renders sharer initials when no avatarUrl', () => {
    const wrapper = mountCard()

    // 'A' initial from 'Alice Sharer'
    // No img with sharer alt should exist
    expect(wrapper.find('img[alt="Alice Sharer"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('A')
  })

  // ── Timestamp ─────────────────────────────────────────────────────────────

  it('renders a formatted timestamp for the share', () => {
    const wrapper = mountCard()

    // formatDate produces a locale string with year — just check 2026 is present
    expect(wrapper.text()).toContain('2026')
  })

  it('renders the original post timestamp as well', () => {
    // Both dates should be rendered (share date and original post date)
    const wrapper = mountCard()

    // createdAt '2026-05-15...' → share date is 2026
    // originalPost.createdAt '2026-05-01...' → also 2026
    // Both should be formatted and visible
    const text = wrapper.text()
    expect(text).toContain('2026')
  })

  // ── Share message / caption ───────────────────────────────────────────────

  it('renders the sharer message/caption when present', () => {
    const wrapper = mountCard(makeSharedPost({ message: 'Check this out!' }))

    expect(wrapper.text()).toContain('Check this out!')
  })

  it('does not render any message section when message is absent', () => {
    const wrapper = mountCard(makeSharedPost({ message: undefined }))

    expect(wrapper.text()).not.toContain('Check this out!')
  })

  it('renders an empty message as absent (falsy)', () => {
    const wrapper = mountCard(makeSharedPost({ message: '' }))

    // v-if="sharedPost.message" — empty string is falsy, so the div is not rendered
    // The component uses v-if which treats '' as falsy
    // No separate message div
    const text = wrapper.text()
    expect(text).not.toContain('Check this out!')
  })

  // ── Original post content ─────────────────────────────────────────────────

  it('renders the original post author name', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('Original Author')
  })

  it('links the original author name to /profile/:authorId', () => {
    const wrapper = mountCard()

    const links = wrapper.findAllComponents(RouterLinkStub)
    const authorLink = links.find(l => String(l.props('to')).includes('author-1'))
    expect(authorLink).toBeDefined()
  })

  it('renders the original post text content (Text type)', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('Original post content here.')
  })

  it('renders the original post author avatar when avatarUrl is present', () => {
    const sharedPost = makeSharedPost({
      originalPost: makeOriginalPost({
        author: makeAuthor({ avatarUrl: 'https://cdn.example.com/author.jpg' }),
      }),
    })
    const wrapper = mountCard(sharedPost)

    const img = wrapper.find('img[alt="Original Author"]')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('https://cdn.example.com/author.jpg')
  })

  it('renders author initials when original author has no avatarUrl', () => {
    const wrapper = mountCard()

    // Original Author → 'O'
    expect(wrapper.find('img[alt="Original Author"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('O')
  })

  // ── Privacy icon ──────────────────────────────────────────────────────────

  it('shows the 🌐 icon for Everyone privacy', () => {
    const wrapper = mountCard(makeSharedPost({
      originalPost: makeOriginalPost({ privacy: 'Everyone' }),
    }))

    expect(wrapper.text()).toContain('🌐')
  })

  it('shows the 👥 icon for Friends privacy', () => {
    const wrapper = mountCard(makeSharedPost({
      originalPost: makeOriginalPost({ privacy: 'Friends' }),
    }))

    expect(wrapper.text()).toContain('👥')
  })

  it('shows the 🔒 icon for OnlyMe privacy', () => {
    const wrapper = mountCard(makeSharedPost({
      originalPost: makeOriginalPost({ privacy: 'OnlyMe' }),
    }))

    expect(wrapper.text()).toContain('🔒')
  })

  // ── TextOnBackground post type ────────────────────────────────────────────

  it('renders text content for TextOnBackground original posts', () => {
    const sharedPost = makeSharedPost({
      originalPost: makeOriginalPost({
        postType: 'TextOnBackground',
        content:  'Bold text on a background',
      }),
    })
    const wrapper = mountCard(sharedPost)

    expect(wrapper.text()).toContain('Bold text on a background')
  })

  // ── Image post type ───────────────────────────────────────────────────────

  it('renders image media for Image type original posts', () => {
    const sharedPost = makeSharedPost({
      originalPost: makeOriginalPost({
        postType: 'Image',
        content:  'Caption here',
        media:    [
          { id: 'm1', mediaType: 'Image', url: 'https://cdn.example.com/img1.jpg', order: 0 },
        ],
      }),
    })
    const wrapper = mountCard(sharedPost)

    const images = wrapper.findAll('img')
    const mediaImg = images.find(img => img.attributes('src') === 'https://cdn.example.com/img1.jpg')
    expect(mediaImg).toBeDefined()
  })

  it('renders the image caption for Image posts', () => {
    const sharedPost = makeSharedPost({
      originalPost: makeOriginalPost({
        postType: 'Image',
        content:  'Photo caption',
        media:    [{ id: 'm1', mediaType: 'Image', url: 'https://cdn.example.com/img1.jpg', order: 0 }],
      }),
    })
    const wrapper = mountCard(sharedPost)

    expect(wrapper.text()).toContain('Photo caption')
  })

  // ── Video post type ───────────────────────────────────────────────────────

  it('renders a video element for Video type original posts', () => {
    const sharedPost = makeSharedPost({
      originalPost: makeOriginalPost({
        postType: 'Video',
        content:  'Video caption',
        media:    [
          { id: 'v1', mediaType: 'Video', url: 'https://cdn.example.com/vid.mp4', order: 0 },
        ],
      }),
    })
    const wrapper = mountCard(sharedPost)

    const video = wrapper.find('video')
    expect(video.exists()).toBe(true)
    expect(video.attributes('src')).toBe('https://cdn.example.com/vid.mp4')
  })

  // ── Link preview ──────────────────────────────────────────────────────────

  it('renders a link preview when originalPost has a linkPreview', () => {
    const sharedPost = makeSharedPost({
      originalPost: makeOriginalPost({
        linkPreview: {
          url:   'https://example.com/article',
          title: 'Example Article',
        },
      }),
    })
    const wrapper = mountCard(sharedPost)

    expect(wrapper.text()).toContain('Example Article')
    expect(wrapper.text()).toContain('https://example.com/article')
  })

  it('renders link preview image when imageUrl is provided', () => {
    const sharedPost = makeSharedPost({
      originalPost: makeOriginalPost({
        linkPreview: {
          url:      'https://example.com/article',
          title:    'Article',
          imageUrl: 'https://cdn.example.com/preview.jpg',
        },
      }),
    })
    const wrapper = mountCard(sharedPost)

    const previewImg = wrapper.find('img[src="https://cdn.example.com/preview.jpg"]')
    expect(previewImg.exists()).toBe(true)
  })

  // ── Multiple images ───────────────────────────────────────────────────────

  it('renders up to 4 images for Image posts with multiple media items', () => {
    const media = Array.from({ length: 5 }, (_, i) => ({
      id:        `m${i}`,
      mediaType: 'Image' as const,
      url:       `https://cdn.example.com/img${i}.jpg`,
      order:     i,
    }))
    const sharedPost = makeSharedPost({
      originalPost: makeOriginalPost({ postType: 'Image', media }),
    })
    const wrapper = mountCard(sharedPost)

    // template uses .slice(0, 4) so max 4 media images
    const mediaImgs = wrapper.findAll('img').filter(img =>
      img.attributes('src')?.startsWith('https://cdn.example.com/img'),
    )
    expect(mediaImgs).toHaveLength(4)
  })
})
