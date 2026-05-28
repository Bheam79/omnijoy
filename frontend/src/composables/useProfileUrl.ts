/**
 * Returns the canonical URL for a user profile.
 *
 * If the user has a vanity slug  → /{urlSlug}
 * Otherwise                      → /profile/{id}
 *
 * Usage:
 *   const url = useProfileUrl({ id: user.id, urlSlug: user.urlSlug })
 *   // or reactively:
 *   const url = computed(() => useProfileUrl(user.value))
 */
export interface ProfileUrlInput {
  id: string
  urlSlug?: string | null
}

export function useProfileUrl(user: ProfileUrlInput): string {
  if (user.urlSlug) {
    return `/${user.urlSlug}`
  }
  return `/profile/${user.id}`
}
