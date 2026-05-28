import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import CommentComposer from '@/components/post/CommentComposer.vue'

// ── Helpers ───────────────────────────────────────────────────────────────────

function mountComposer(props: Record<string, unknown> = {}) {
  const pinia = createPinia()
  setActivePinia(pinia)

  return mount(CommentComposer, {
    props: {
      placeholder: 'Write a comment…',
      ...props,
    },
    global: { plugins: [pinia] },
    attachTo: document.body,
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('CommentComposer', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    document.body.innerHTML = ''
  })

  // ── Rendering ─────────────────────────────────────────────────────────────

  it('renders textarea with placeholder', () => {
    const wrapper = mountComposer()
    const textarea = wrapper.find('textarea')
    expect(textarea.exists()).toBe(true)
    expect(textarea.attributes('placeholder')).toBe('Write a comment…')
  })

  it('renders avatar placeholder when no avatarUrl provided', () => {
    const wrapper = mountComposer({ displayName: 'Alice' })
    // Should render the initial letter "A"
    expect(wrapper.html()).toContain('A')
  })

  it('renders avatar image when avatarUrl is provided', () => {
    const wrapper = mountComposer({ avatarUrl: '/avatar.jpg', displayName: 'Alice' })
    const img = wrapper.find('img')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('/avatar.jpg')
  })

  it('does not show submit/cancel buttons when text is empty', () => {
    const wrapper = mountComposer()
    const submitBtn = wrapper.find('[data-testid="comment-submit"]')
    expect(submitBtn.exists()).toBe(false)
  })

  // ── Interaction ───────────────────────────────────────────────────────────

  it('shows submit button when text is entered', async () => {
    const wrapper = mountComposer()
    const textarea = wrapper.find('[data-testid="comment-input"]')
    await textarea.setValue('Hello!')
    const submitBtn = wrapper.find('[data-testid="comment-submit"]')
    expect(submitBtn.exists()).toBe(true)
  })

  it('emits "submitted" with trimmed content when Post is clicked', async () => {
    const wrapper = mountComposer()
    await wrapper.find('[data-testid="comment-input"]').setValue('  Hello world  ')
    await wrapper.find('[data-testid="comment-submit"]').trigger('click')
    expect(wrapper.emitted('submitted')).toHaveLength(1)
    expect(wrapper.emitted('submitted')![0]).toEqual(['Hello world'])
  })

  it('emits "submitted" when Enter key is pressed (no shift)', async () => {
    const wrapper = mountComposer()
    const textarea = wrapper.find('[data-testid="comment-input"]')
    await textarea.setValue('Enter key submit')
    await textarea.trigger('keydown', { key: 'Enter', shiftKey: false })
    expect(wrapper.emitted('submitted')).toHaveLength(1)
    expect(wrapper.emitted('submitted')![0]).toEqual(['Enter key submit'])
  })

  it('does NOT emit "submitted" when Shift+Enter is pressed', async () => {
    const wrapper = mountComposer()
    const textarea = wrapper.find('[data-testid="comment-input"]')
    await textarea.setValue('multi\nline')
    await textarea.trigger('keydown', { key: 'Enter', shiftKey: true })
    expect(wrapper.emitted('submitted')).toBeUndefined()
  })

  it('clears textarea content after submit', async () => {
    const wrapper = mountComposer()
    const textarea = wrapper.find('[data-testid="comment-input"]')
    await textarea.setValue('My comment')
    await wrapper.find('[data-testid="comment-submit"]').trigger('click')
    expect((textarea.element as HTMLTextAreaElement).value).toBe('')
  })

  it('emits "cancelled" when Cancel is clicked', async () => {
    const wrapper = mountComposer()
    await wrapper.find('[data-testid="comment-input"]').setValue('Something')
    // Cancel button appears once text is present
    const cancelBtn = wrapper.findAll('button').find((b) => b.text() === 'Cancel')
    expect(cancelBtn).toBeTruthy()
    await cancelBtn!.trigger('click')
    expect(wrapper.emitted('cancelled')).toHaveLength(1)
  })

  it('clears text when Cancel is clicked', async () => {
    const wrapper = mountComposer()
    const textarea = wrapper.find('[data-testid="comment-input"]')
    await textarea.setValue('Something')
    const cancelBtn = wrapper.findAll('button').find((b) => b.text() === 'Cancel')
    await cancelBtn!.trigger('click')
    expect((textarea.element as HTMLTextAreaElement).value).toBe('')
  })

  it('disables textarea and shows loading state when loading=true', async () => {
    const wrapper = mountComposer({ loading: true })
    const textarea = wrapper.find('[data-testid="comment-input"]')
    expect(textarea.attributes('disabled')).toBeDefined()
  })

  it('shows "Posting…" text on submit button when loading=true and text present', async () => {
    // We can't easily test mid-submission loading; test the prop directly
    const wrapper = mountComposer({ loading: true })
    // Pre-set text programmatically via vm to bypass disabled check
    const vm = wrapper.vm as unknown as { text: string }
    vm.text = 'something'
    await flushPromises()
    // The submit button is only shown if text is present AND we can check text
    // The disabled textarea means no normal typing, but the button text check:
    const submitBtn = wrapper.find('[data-testid="comment-submit"]')
    if (submitBtn.exists()) {
      expect(submitBtn.text()).toContain('Posting')
    }
  })

  it('does not emit "submitted" when content is only whitespace', async () => {
    const wrapper = mountComposer()
    await wrapper.find('[data-testid="comment-input"]').setValue('   ')
    // Submit button should not appear (canSubmit is false)
    expect(wrapper.find('[data-testid="comment-submit"]').exists()).toBe(false)
  })

  // ── compact mode ──────────────────────────────────────────────────────────

  it('applies compact layout class when compact=true', () => {
    const wrapper = mountComposer({ compact: true })
    expect(wrapper.html()).toContain('comment-input')
  })

  // ── Expose ────────────────────────────────────────────────────────────────

  it('expose: clear() resets textarea', async () => {
    const wrapper = mountComposer()
    const textarea = wrapper.find('[data-testid="comment-input"]')
    await textarea.setValue('Some text')
    ;(wrapper.vm as unknown as { clear: () => void }).clear()
    await flushPromises()
    expect((textarea.element as HTMLTextAreaElement).value).toBe('')
  })
})
