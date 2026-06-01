import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import ReactionPicker from '@/components/post/ReactionPicker.vue'

// Use fake timers for hover delay testing
vi.useFakeTimers()

function mountPicker(props: {
  currentReaction?: string | null
  disabled?: boolean
} = {}) {
  return mount(ReactionPicker, {
    props: {
      currentReaction: props.currentReaction ?? null,
      disabled:        props.disabled ?? false,
    } as Record<string, unknown>,
  })
}

describe('ReactionPicker', () => {
  beforeEach(() => {
    vi.clearAllTimers()
  })

  afterEach(() => {
    vi.clearAllTimers()
  })

  // ── Rendering ─────────────────────────────────────────────────────────────

  it('renders the main reaction button', () => {
    const wrapper = mountPicker()
    const btn = wrapper.find('[data-testid="reaction-button"]')
    expect(btn.exists()).toBe(true)
  })

  it('shows "Like" label when no reaction is active', () => {
    const wrapper = mountPicker()
    expect(wrapper.text()).toContain('Like')
  })

  it('shows active reaction emoji and label when currentReaction is set', () => {
    const wrapper = mountPicker({ currentReaction: 'Love' })
    expect(wrapper.text()).toContain('❤️')
    expect(wrapper.text()).toContain('Love')
  })

  it('does not show picker popup initially', () => {
    const wrapper = mountPicker()
    expect(wrapper.find('[data-testid="reaction-picker-popup"]').exists()).toBe(false)
  })

  // ── Hover shows picker ────────────────────────────────────────────────────

  it('shows picker popup after hovering for 500ms', async () => {
    const wrapper = mountPicker()
    const btn = wrapper.find('[data-testid="reaction-button"]')

    await btn.trigger('mouseenter')
    vi.advanceTimersByTime(500)
    await flushPromises()

    expect(wrapper.find('[data-testid="reaction-picker-popup"]').exists()).toBe(true)
  })

  it('does not show picker if hover is cancelled before 500ms', async () => {
    const wrapper = mountPicker()
    const btn = wrapper.find('[data-testid="reaction-button"]')

    await btn.trigger('mouseenter')
    vi.advanceTimersByTime(300)
    await btn.trigger('mouseleave')
    vi.advanceTimersByTime(500)
    await flushPromises()

    expect(wrapper.find('[data-testid="reaction-picker-popup"]').exists()).toBe(false)
  })

  // ── Picking a reaction ────────────────────────────────────────────────────

  it('emits "react" with the correct type when an emoji is clicked', async () => {
    const wrapper = mountPicker()

    // Open picker
    await wrapper.find('[data-testid="reaction-button"]').trigger('mouseenter')
    vi.advanceTimersByTime(500)
    await flushPromises()

    // Click Love emoji
    const loveBtn = wrapper.find('[data-testid="pick-love"]')
    expect(loveBtn.exists()).toBe(true)
    await loveBtn.trigger('click')

    expect(wrapper.emitted('react')).toHaveLength(1)
    expect(wrapper.emitted('react')![0]).toEqual(['Love'])
  })

  it('emits "remove" when clicking the already-active reaction in picker', async () => {
    const wrapper = mountPicker({ currentReaction: 'Like' })

    // Open picker
    await wrapper.find('[data-testid="reaction-button"]').trigger('mouseenter')
    vi.advanceTimersByTime(500)
    await flushPromises()

    // Click the active Like emoji → should remove
    await wrapper.find('[data-testid="pick-like"]').trigger('click')

    expect(wrapper.emitted('remove')).toHaveLength(1)
    expect(wrapper.emitted('react')).toBeFalsy()
  })

  it('closes picker after selecting a reaction', async () => {
    const wrapper = mountPicker()

    await wrapper.find('[data-testid="reaction-button"]').trigger('mouseenter')
    vi.advanceTimersByTime(500)
    await flushPromises()

    await wrapper.find('[data-testid="pick-love"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="reaction-picker-popup"]').exists()).toBe(false)
  })

  // ── Button click behaviour ────────────────────────────────────────────────

  it('emits "react" with Like when button clicked and no current reaction', async () => {
    const wrapper = mountPicker({ currentReaction: null })
    await wrapper.find('[data-testid="reaction-button"]').trigger('click')
    expect(wrapper.emitted('react')![0]).toEqual(['Like'])
  })

  it('emits "remove" when button clicked and reaction is already set', async () => {
    const wrapper = mountPicker({ currentReaction: 'Love' })
    await wrapper.find('[data-testid="reaction-button"]').trigger('click')
    expect(wrapper.emitted('remove')).toHaveLength(1)
  })

  // ── Disabled state ────────────────────────────────────────────────────────

  it('does not open picker when disabled', async () => {
    const wrapper = mountPicker({ disabled: true })
    const btn = wrapper.find('[data-testid="reaction-button"]')

    await btn.trigger('mouseenter')
    vi.advanceTimersByTime(500)
    await flushPromises()

    expect(wrapper.find('[data-testid="reaction-picker-popup"]').exists()).toBe(false)
  })

  it('does not emit events when disabled and clicked', async () => {
    const wrapper = mountPicker({ disabled: true })
    await wrapper.find('[data-testid="reaction-button"]').trigger('click')
    expect(wrapper.emitted('react')).toBeFalsy()
    expect(wrapper.emitted('remove')).toBeFalsy()
  })

  // ── mouseup selects reaction (hold-and-release flow) ─────────────────────

  it('emits "react" when mouseup fires on an emoji (hold-and-release from Like button)', async () => {
    const wrapper = mountPicker()

    // Open picker via hover
    await wrapper.find('[data-testid="reaction-button"]').trigger('mouseenter')
    vi.advanceTimersByTime(500)
    await flushPromises()

    // User released the mouse over the Haha emoji (no prior mousedown on it)
    const hahaBtn = wrapper.find('[data-testid="pick-haha"]')
    expect(hahaBtn.exists()).toBe(true)
    await hahaBtn.trigger('mouseup')

    expect(wrapper.emitted('react')).toHaveLength(1)
    expect(wrapper.emitted('react')![0]).toEqual(['Haha'])
  })

  it('does not double-emit when mouseup is followed by a click (normal mouse click)', async () => {
    const wrapper = mountPicker()

    // Open picker via hover
    await wrapper.find('[data-testid="reaction-button"]').trigger('mouseenter')
    vi.advanceTimersByTime(500)
    await flushPromises()

    const wowBtn = wrapper.find('[data-testid="pick-wow"]')
    // In a real browser a click is preceded by mouseup; simulate that sequence.
    await wowBtn.trigger('mouseup')   // sets pickedViaMouseUp flag
    await wowBtn.trigger('click')     // suppressed by pickedViaMouseUp

    // Should only emit once
    expect(wrapper.emitted('react')).toHaveLength(1)
    expect(wrapper.emitted('react')![0]).toEqual(['Wow'])
  })

  it('emits "react" via click alone (keyboard activation) when mouseup has not fired', async () => {
    const wrapper = mountPicker()

    // Open picker via hover
    await wrapper.find('[data-testid="reaction-button"]').trigger('mouseenter')
    vi.advanceTimersByTime(500)
    await flushPromises()

    // Keyboard-activated click: no preceding mouseup, so pickedViaMouseUp is false
    await wrapper.find('[data-testid="pick-angry"]').trigger('click')

    expect(wrapper.emitted('react')).toHaveLength(1)
    expect(wrapper.emitted('react')![0]).toEqual(['Angry'])
  })

  // ── All reaction types present in picker ──────────────────────────────────

  it('shows all six reaction types in picker popup', async () => {
    const wrapper = mountPicker()

    await wrapper.find('[data-testid="reaction-button"]').trigger('mouseenter')
    vi.advanceTimersByTime(500)
    await flushPromises()

    const types = ['like', 'love', 'haha', 'wow', 'sad', 'angry']
    for (const type of types) {
      expect(wrapper.find(`[data-testid="pick-${type}"]`).exists()).toBe(true)
    }
  })
})
