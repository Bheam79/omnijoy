import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import MentionText from '../MentionText.vue'
import type { MentionDto } from '@/types/mentions'

const alice: MentionDto = {
  matchedSlug: 'alice-old',
  userId: 'user-alice',
  displayName: 'Alice Example',
  urlSlug: 'alice-now',
}

function mountText(content: string, mentions: MentionDto[] = [alice]) {
  return mount(MentionText, {
    props: { content, mentions },
    global: { stubs: { RouterLink: RouterLinkStub } },
  })
}

describe('MentionText', () => {
  it('links every resolved occurrence around punctuation using the current vanity slug', () => {
    const wrapper = mountText('Hi, @alice-old! Again: (@ALICE-OLD).')
    const links = wrapper.findAllComponents(RouterLinkStub)

    expect(links).toHaveLength(2)
    expect(links.map(link => link.text())).toEqual(['@alice-old', '@ALICE-OLD'])
    expect(links.every(link => link.props('to') === '/alice-now')).toBe(true)
    expect(wrapper.text()).toBe('Hi, @alice-old! Again: (@ALICE-OLD).')
  })

  it('leaves unknown, blocked, unresolved, and larger handle tokens as plain text', () => {
    const wrapper = mountText('@unknown @blocked @alice-old-extra email@alice-old')

    expect(wrapper.findAllComponents(RouterLinkStub)).toHaveLength(0)
    expect(wrapper.text()).toBe('@unknown @blocked @alice-old-extra email@alice-old')
  })

  it('falls back to the target user id when the current vanity slug is absent', () => {
    const wrapper = mountText('@alice-old', [{ ...alice, urlSlug: null }])

    expect(wrapper.getComponent(RouterLinkStub).props('to')).toBe('/profile/user-alice')
  })

  it('renders content as escaped text rather than executable HTML', () => {
    const content = '<img src=x onerror="alert(1)"> @alice-old <script>alert(2)</script>'
    const wrapper = mountText(content)

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('script').exists()).toBe(false)
    expect(wrapper.text()).toBe(content)
    expect(wrapper.html()).toContain('&lt;img src=x onerror="alert(1)"&gt;')
    expect(wrapper.findAllComponents(RouterLinkStub)).toHaveLength(1)
  })
})
