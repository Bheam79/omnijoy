import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { CompanyPageDto } from '@/services/companyPageService'
import type { EventDto } from '@/services/eventService'

/**
 * Company Mode store.
 *
 * When active, all post and event creation defaults to the active company
 * instead of the personal user identity.  The UI hides the "Posting as" /
 * "Organizer" selectors because the company context is already given.
 *
 * State is kept in memory only (not persisted across page reload) — the
 * user re-activates it by visiting the company page.
 *
 * `activeEvent` is set by event views (EventDetailView, EventEditView, etc.)
 * so the CompanySidebar can show event-contextual navigation without an
 * extra network call.
 */
export const useCompanyModeStore = defineStore('companyMode', () => {
  const activeCompany = ref<CompanyPageDto | null>(null)
  const activeEvent   = ref<EventDto | null>(null)

  const isActive = computed(() => activeCompany.value !== null)

  function activate(company: CompanyPageDto) {
    activeCompany.value = company
  }

  function deactivate() {
    activeCompany.value = null
    activeEvent.value   = null
  }

  function setActiveEvent(event: EventDto | null) {
    activeEvent.value = event
  }

  return { activeCompany, activeEvent, isActive, activate, deactivate, setActiveEvent }
})
