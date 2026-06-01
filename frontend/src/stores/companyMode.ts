import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { CompanyPageDto } from '@/services/companyPageService'
import type { EventDto } from '@/services/eventService'

/**
 * Company Mode store — navigation-context bucket for the active company and event.
 *
 * When active, all post and event creation defaults to the active company
 * instead of the personal user identity.  The UI hides the "Posting as" /
 * "Organizer" selectors because the company context is already given.
 *
 * State is kept in memory only (not persisted across page reload) — the
 * user re-activates it by visiting the company page.
 *
 * `activeEvent` is set by event views (EventDetailView, EventEditView, etc.)
 * so both CompanySidebar (company mode) and Sidebar (personal mode) can show
 * event-contextual navigation without an extra network call.
 *
 * `canManageActiveEvent` is true when the current user may manage the active
 * event (personal creator, or company Owner/Admin).  Sidebar uses this to
 * decide whether to render the Event management section.
 */
export const useCompanyModeStore = defineStore('companyMode', () => {
  const activeCompany         = ref<CompanyPageDto | null>(null)
  const activeEvent           = ref<EventDto | null>(null)
  const canManageActiveEvent  = ref(false)

  const isActive = computed(() => activeCompany.value !== null)

  function activate(company: CompanyPageDto) {
    activeCompany.value = company
  }

  function deactivate() {
    activeCompany.value        = null
    activeEvent.value          = null
    canManageActiveEvent.value = false
  }

  function setActiveEvent(event: EventDto | null, canManage = false) {
    activeEvent.value          = event
    canManageActiveEvent.value = canManage
  }

  return { activeCompany, activeEvent, canManageActiveEvent, isActive, activate, deactivate, setActiveEvent }
})
