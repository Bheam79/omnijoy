import api from './api'

// ── DTOs ──────────────────────────────────────────────────────────────────────

export type SlugOwnerType = 'user' | 'company'

export interface SlugResolutionDto {
  /** 'user' or 'company' */
  type: SlugOwnerType
  /** The owner's UUID */
  id: string
}

export interface SlugAvailabilityDto {
  available: boolean
  reason?: 'invalid' | 'reserved' | 'taken'
}

// ── Service ───────────────────────────────────────────────────────────────────

export const slugService = {
  /**
   * Resolves a vanity URL slug to its owner.
   * Returns `null` if the slug is not assigned (404 response).
   */
  async resolve(slug: string): Promise<SlugResolutionDto | null> {
    try {
      const { data } = await api.get<SlugResolutionDto>(`/api/slugs/resolve/${encodeURIComponent(slug)}`)
      return data
    } catch (e: unknown) {
      const ax = e as { response?: { status?: number } }
      if (ax.response?.status === 404) return null
      throw e
    }
  },

  /**
   * Checks whether a candidate slug is available.
   */
  async check(slug: string): Promise<SlugAvailabilityDto> {
    const { data } = await api.get<SlugAvailabilityDto>('/api/slugs/check', {
      params: { slug },
    })
    return data
  },

  /**
   * Sets (or clears) the vanity slug for the current user.
   * Pass `null` to revert to the default `/profile/{id}` URL.
   */
  async setUserSlug(slug: string | null): Promise<void> {
    await api.put('/api/users/me/slug', { slug })
  },

  /**
   * Sets (or clears) the vanity slug for a company page.
   * Pass `null` to revert to the default `/company/{id}` URL.
   */
  async setCompanySlug(companyId: string, slug: string | null): Promise<void> {
    await api.put(`/api/company-pages/${companyId}/slug`, { slug })
  },
}

export default slugService
