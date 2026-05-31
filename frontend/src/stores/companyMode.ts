import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { CompanyPageDto } from '@/services/companyPageService'

/**
 * Company Mode store.
 *
 * When active, all post and event creation defaults to the active company
 * instead of the personal user identity.  The UI hides the "Posting as" /
 * "Organizer" selectors because the company context is already given.
 *
 * State is kept in memory only (not persisted across page reload) — the
 * user re-activates it by visiting the company page.
 */
export const useCompanyModeStore = defineStore('companyMode', () => {
  const activeCompany = ref<CompanyPageDto | null>(null)

  const isActive = computed(() => activeCompany.value !== null)

  function activate(company: CompanyPageDto) {
    activeCompany.value = company
  }

  function deactivate() {
    activeCompany.value = null
  }

  return { activeCompany, isActive, activate, deactivate }
})
