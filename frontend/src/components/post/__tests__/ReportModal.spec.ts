import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ReportModal from '@/components/post/ReportModal.vue'

// ── Mock reportService ────────────────────────────────────────────────────────

const mockSubmitReport = vi.hoisted(() => vi.fn())

vi.mock('@/services/reportService', () => ({
  reportService: {
    submitReport: mockSubmitReport,
  },
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function mountModal(
  targetType: 'Post' | 'User' | 'Comment' = 'Post',
  targetId = 'target-123',
  open = true,
) {
  const pinia = createPinia()
  setActivePinia(pinia)

  return mount(ReportModal, {
    props:  { modelValue: open, targetType, targetId },
    global: {
      plugins: [pinia],
      stubs:   { Teleport: true },
    },
    attachTo: document.body,
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ReportModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── Rendering ─────────────────────────────────────────────────────────────

  it('renders when modelValue is true', () => {
    const wrapper = mountModal()
    expect(wrapper.text()).toContain('Report post')
  })

  it('does not render when modelValue is false', () => {
    const wrapper = mountModal('Post', 'target-123', false)
    expect(wrapper.text()).not.toContain('Report post')
  })

  it('shows "Report user" title when targetType is User', () => {
    const wrapper = mountModal('User')
    expect(wrapper.text()).toContain('Report user')
  })

  it('shows "Report comment" title when targetType is Comment', () => {
    const wrapper = mountModal('Comment')
    expect(wrapper.text()).toContain('Report comment')
  })

  it('renders reason dropdown', () => {
    const wrapper = mountModal()
    const select = wrapper.find('[aria-label="Report reason"]')
    expect(select.exists()).toBe(true)
  })

  it('renders all six reason options', () => {
    const wrapper = mountModal()
    const select = wrapper.find('[aria-label="Report reason"]')
    const options = select.findAll('option')
    expect(options).toHaveLength(6)
    const texts = options.map(o => o.text())
    expect(texts).toContain('Spam')
    expect(texts).toContain('Harassment')
    expect(texts).toContain('Misinformation')
    expect(texts).toContain('Nudity')
    expect(texts).toContain('Violence')
    expect(texts).toContain('Other')
  })

  it('renders notes textarea', () => {
    const wrapper = mountModal()
    const ta = wrapper.find('[aria-label="Report notes"]')
    expect(ta.exists()).toBe(true)
  })

  it('renders Submit report and Cancel buttons', () => {
    const wrapper = mountModal()
    expect(wrapper.text()).toContain('Submit report')
    expect(wrapper.text()).toContain('Cancel')
  })

  // ── Submission ────────────────────────────────────────────────────────────

  it('calls submitReport with correct payload on submit', async () => {
    mockSubmitReport.mockResolvedValue({ id: 'rpt-1', status: 'Pending' })

    const wrapper = mountModal('Post', 'post-abc')
    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Submit report'))
    await submitBtn!.trigger('click')
    await flushPromises()

    expect(mockSubmitReport).toHaveBeenCalledWith({
      targetType: 'Post',
      targetId:   'post-abc',
      reason:     'Spam',   // default
      notes:      undefined,
    })
  })

  it('includes notes in payload when notes is provided', async () => {
    mockSubmitReport.mockResolvedValue({ id: 'rpt-2', status: 'Pending' })

    const wrapper = mountModal()
    const ta = wrapper.find('[aria-label="Report notes"]')
    await ta.setValue('This is bad content')

    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Submit report'))
    await submitBtn!.trigger('click')
    await flushPromises()

    expect(mockSubmitReport).toHaveBeenCalledWith(
      expect.objectContaining({ notes: 'This is bad content' })
    )
  })

  it('sends selected reason in payload', async () => {
    mockSubmitReport.mockResolvedValue({ id: 'rpt-3', status: 'Pending' })

    const wrapper = mountModal()
    const select = wrapper.find('[aria-label="Report reason"]')
    await select.setValue('Harassment')

    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Submit report'))
    await submitBtn!.trigger('click')
    await flushPromises()

    expect(mockSubmitReport).toHaveBeenCalledWith(
      expect.objectContaining({ reason: 'Harassment' })
    )
  })

  it('shows confirmation message after successful submit', async () => {
    mockSubmitReport.mockResolvedValue({ id: 'rpt-4', status: 'Pending' })

    const wrapper = mountModal()
    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Submit report'))
    await submitBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Report submitted')
    expect(wrapper.text()).toContain("Thank you for helping keep the community safe")
  })

  it('emits reported event after successful submit', async () => {
    mockSubmitReport.mockResolvedValue({ id: 'rpt-5', status: 'Pending' })

    const wrapper = mountModal()
    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Submit report'))
    await submitBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('reported')).toBeTruthy()
  })

  it('shows error message when submitReport fails', async () => {
    mockSubmitReport.mockRejectedValue({
      response: { data: { error: 'You have already reported this content.' } },
    })

    const wrapper = mountModal()
    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Submit report'))
    await submitBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('You have already reported this content.')
  })

  it('shows generic error for network failures', async () => {
    mockSubmitReport.mockRejectedValue({ message: 'Network Error' })

    const wrapper = mountModal()
    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Submit report'))
    await submitBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Network Error')
  })

  // ── Close behaviour ───────────────────────────────────────────────────────

  it('emits update:modelValue false when Cancel is clicked', async () => {
    const wrapper = mountModal()
    const cancelBtn = wrapper.findAll('button').find(b => b.text() === 'Cancel')
    await cancelBtn!.trigger('click')
    expect(wrapper.emitted('update:modelValue')).toContainEqual([false])
  })

  it('emits update:modelValue false when Done is clicked after success', async () => {
    mockSubmitReport.mockResolvedValue({ id: 'rpt-6', status: 'Pending' })

    const wrapper = mountModal()
    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Submit report'))
    await submitBtn!.trigger('click')
    await flushPromises()

    const doneBtn = wrapper.findAll('button').find(b => b.text() === 'Done')
    await doneBtn!.trigger('click')
    expect(wrapper.emitted('update:modelValue')).toContainEqual([false])
  })

  it('submit button shows "Submitting…" while in-flight', async () => {
    let resolve!: (value: unknown) => void
    mockSubmitReport.mockReturnValue(new Promise(r => { resolve = r }))

    const wrapper = mountModal()
    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Submit report'))
    await submitBtn!.trigger('click')
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('Submitting…')
    // Resolve to avoid unhandled promise
    resolve({ id: 'x', status: 'Pending' })
    await flushPromises()
  })
})
