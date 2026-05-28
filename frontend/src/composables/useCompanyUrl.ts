/**
 * Returns the canonical URL for a company page.
 *
 * If the company has a vanity slug  → /{urlSlug}
 * Otherwise                         → /company/{id}
 *
 * Usage:
 *   const url = useCompanyUrl({ id: company.id, urlSlug: company.urlSlug })
 *   // or reactively:
 *   const url = computed(() => useCompanyUrl(company.value))
 */
export interface CompanyUrlInput {
  id: string
  urlSlug?: string | null
}

export function useCompanyUrl(company: CompanyUrlInput): string {
  if (company.urlSlug) {
    return `/${company.urlSlug}`
  }
  return `/company/${company.id}`
}
