import { describe, it, expect } from 'vitest'
import { useProfileUrl } from '@/composables/useProfileUrl'

describe('useProfileUrl', () => {
  it('returns the vanity URL when urlSlug is present', () => {
    expect(useProfileUrl({ id: 'abc123', urlSlug: 'johndoe' })).toBe('/johndoe')
  })

  it('returns /profile/{id} when urlSlug is null', () => {
    expect(useProfileUrl({ id: 'abc123', urlSlug: null })).toBe('/profile/abc123')
  })

  it('returns /profile/{id} when urlSlug is undefined', () => {
    expect(useProfileUrl({ id: 'abc123', urlSlug: undefined })).toBe('/profile/abc123')
  })

  it('returns /profile/{id} when urlSlug is not provided', () => {
    expect(useProfileUrl({ id: 'abc123' })).toBe('/profile/abc123')
  })

  it('returns /profile/{id} when urlSlug is empty string', () => {
    // Empty string is falsy → fall back
    expect(useProfileUrl({ id: 'abc123', urlSlug: '' })).toBe('/profile/abc123')
  })

  it('preserves the slug exactly (already lowercased by backend)', () => {
    expect(useProfileUrl({ id: 'abc123', urlSlug: 'alice_smith' })).toBe('/alice_smith')
  })
})
