import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { ReactionCountDto } from '@/services/reactionService'

// Import after mocks (none needed — ReactionsModal is pure props + emits)
import ReactionsModal from '../ReactionsModal.vue'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeCount(reactionType: ReactionCountDto['reactionType'], count: number): ReactionCountDto {
  return { reactionType, count }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountModal(counts: ReactionCountDto[] = [], totalCount = 0) {
  return mount(ReactionsModal, {
    props:  { counts, totalCount },
    global: {
      plugins: [createPinia()],
      stubs:   {
        Teleport: { template: '<div><slot /></div>' },
      },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ReactionsModal', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  // ── Structure ─────────────────────────────────────────────────────────────

  it('renders the modal with data-testid="reactions-modal"', () => {
    const wrapper = mountModal()

    expect(wrapper.find('[data-testid="reactions-modal"]').exists()).toBe(true)
  })

  it('renders the "Reactions" heading', () => {
    const wrapper = mountModal()

    expect(wrapper.text()).toContain('Reactions')
  })

  // ── Empty state ───────────────────────────────────────────────────────────

  it('shows "No reactions yet" empty state when totalCount is 0', () => {
    const wrapper = mountModal([], 0)

    expect(wrapper.text()).toContain('No reactions yet. Be the first!')
  })

  it('does not render reaction rows when totalCount is 0', () => {
    const wrapper = mountModal([], 0)

    expect(wrapper.find('[data-testid^="reaction-row-"]').exists()).toBe(false)
  })

  it('does not show the empty state when totalCount > 0', () => {
    const wrapper = mountModal([makeCount('Like', 3)], 3)

    expect(wrapper.text()).not.toContain('No reactions yet')
  })

  // ── Total count ───────────────────────────────────────────────────────────

  it('renders the total reaction count when totalCount > 0', () => {
    const wrapper = mountModal([makeCount('Like', 5)], 5)

    expect(wrapper.text()).toContain('5')
    expect(wrapper.text()).toContain('Total reactions')
  })

  it('renders total count of 1 correctly', () => {
    const wrapper = mountModal([makeCount('Love', 1)], 1)

    expect(wrapper.text()).toContain('1')
  })

  // ── Per-type reaction rows ────────────────────────────────────────────────

  it('renders a row for each reaction type in counts', () => {
    const counts = [
      makeCount('Like', 10),
      makeCount('Love', 5),
      makeCount('Haha', 2),
    ]
    const wrapper = mountModal(counts, 17)

    expect(wrapper.find('[data-testid="reaction-row-like"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="reaction-row-love"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="reaction-row-haha"]').exists()).toBe(true)
  })

  it('renders the correct emoji for each reaction type', () => {
    const wrapper = mountModal([makeCount('Like', 1), makeCount('Love', 1)], 2)

    expect(wrapper.text()).toContain('👍') // Like
    expect(wrapper.text()).toContain('❤️') // Love
  })

  it('renders the correct label for each reaction type', () => {
    const wrapper = mountModal([makeCount('Haha', 3), makeCount('Sad', 1)], 4)

    expect(wrapper.text()).toContain('Haha')
    expect(wrapper.text()).toContain('Sad')
  })

  it('renders the count for each reaction type row', () => {
    const counts = [makeCount('Like', 10), makeCount('Angry', 2)]
    const wrapper = mountModal(counts, 12)

    const likeRow = wrapper.find('[data-testid="reaction-row-like"]')
    expect(likeRow.text()).toContain('10')

    const angryRow = wrapper.find('[data-testid="reaction-row-angry"]')
    expect(angryRow.text()).toContain('2')
  })

  it('renders emoji alongside the label in each reaction row', () => {
    const wrapper = mountModal([makeCount('Wow', 7)], 7)

    const wowRow = wrapper.find('[data-testid="reaction-row-wow"]')
    expect(wowRow.text()).toContain('😮') // Wow emoji
    expect(wowRow.text()).toContain('Wow')
    expect(wowRow.text()).toContain('7')
  })

  it('renders all 6 reaction types when all are present in counts', () => {
    const counts = [
      makeCount('Like',  5),
      makeCount('Love',  3),
      makeCount('Haha',  2),
      makeCount('Wow',   1),
      makeCount('Sad',   1),
      makeCount('Angry', 1),
    ]
    const wrapper = mountModal(counts, 13)

    const types = ['like', 'love', 'haha', 'wow', 'sad', 'angry']
    for (const t of types) {
      expect(wrapper.find(`[data-testid="reaction-row-${t}"]`).exists()).toBe(true)
    }
  })

  it('only renders rows for counts that are provided', () => {
    // Only Like is in counts
    const wrapper = mountModal([makeCount('Like', 4)], 4)

    // Love row should NOT be rendered
    expect(wrapper.find('[data-testid="reaction-row-love"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="reaction-row-like"]').exists()).toBe(true)
  })

  // ── Close button ──────────────────────────────────────────────────────────

  it('renders the close button with data-testid="reactions-modal-close"', () => {
    const wrapper = mountModal()

    expect(wrapper.find('[data-testid="reactions-modal-close"]').exists()).toBe(true)
  })

  it('emits "close" when the Close button is clicked', async () => {
    const wrapper = mountModal()

    await wrapper.find('[data-testid="reactions-modal-close"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('Close button has aria-label="Close"', () => {
    const wrapper = mountModal()

    const closeBtn = wrapper.find('[aria-label="Close"]')
    expect(closeBtn.exists()).toBe(true)
  })

  // ── Backdrop click ────────────────────────────────────────────────────────

  it('emits "close" when the backdrop is clicked', async () => {
    const wrapper = mountModal()

    const backdrop = wrapper.find('[data-testid="reactions-modal"]')
    // Simulate self-click (not a child)
    await backdrop.trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
