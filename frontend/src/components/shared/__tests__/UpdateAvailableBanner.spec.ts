import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import UpdateAvailableBanner from '@/components/shared/UpdateAvailableBanner.vue'

describe('UpdateAvailableBanner', () => {
  it('is hidden when visible=false', () => {
    const wrapper = mount(UpdateAvailableBanner, { props: { visible: false } })
    expect(wrapper.find('[data-testid="update-available-banner"]').exists()).toBe(false)
  })

  it('is visible when visible=true', async () => {
    const wrapper = mount(UpdateAvailableBanner, { props: { visible: true } })
    expect(wrapper.find('[data-testid="update-available-banner"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('A new version of Omnijoy is available')
  })

  it('emits reload and calls window.location.reload when "Reload now" is clicked', async () => {
    const reloadSpy = vi.fn()
    Object.defineProperty(window, 'location', {
      value: { reload: reloadSpy },
      writable: true,
    })

    const wrapper = mount(UpdateAvailableBanner, { props: { visible: true } })
    await wrapper.find('[data-testid="update-banner-reload"]').trigger('click')

    expect(wrapper.emitted('reload')).toBeTruthy()
    expect(reloadSpy).toHaveBeenCalledTimes(1)
  })

  it('emits dismiss when dismiss button is clicked', async () => {
    const wrapper = mount(UpdateAvailableBanner, { props: { visible: true } })
    await wrapper.find('[data-testid="update-banner-dismiss"]').trigger('click')

    expect(wrapper.emitted('dismiss')).toBeTruthy()
  })
})
