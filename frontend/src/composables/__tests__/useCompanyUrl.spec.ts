import { describe, it, expect } from 'vitest'
import { useCompanyUrl } from '@/composables/useCompanyUrl'

describe('useCompanyUrl', () => {
  it('returns the vanity URL when urlSlug is present', () => {
    expect(useCompanyUrl({ id: 'cmp123', urlSlug: 'acmecorp' })).toBe('/acmecorp')
  })

  it('returns /company/{id} when urlSlug is null', () => {
    expect(useCompanyUrl({ id: 'cmp123', urlSlug: null })).toBe('/company/cmp123')
  })

  it('returns /company/{id} when urlSlug is undefined', () => {
    expect(useCompanyUrl({ id: 'cmp123', urlSlug: undefined })).toBe('/company/cmp123')
  })

  it('returns /company/{id} when urlSlug is not provided', () => {
    expect(useCompanyUrl({ id: 'cmp123' })).toBe('/company/cmp123')
  })

  it('returns /company/{id} when urlSlug is empty string', () => {
    expect(useCompanyUrl({ id: 'cmp123', urlSlug: '' })).toBe('/company/cmp123')
  })

  it('preserves the slug exactly (already lowercased by backend)', () => {
    expect(useCompanyUrl({ id: 'cmp123', urlSlug: 'globex_corp' })).toBe('/globex_corp')
  })
})
