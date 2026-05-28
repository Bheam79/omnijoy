import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import {
  adminService,
  type AdminUserDto,
  type ModerationLogDto,
  type ReportStatus,
} from '@/services/adminService'
import type { ReportDto, ReportTargetType } from '@/services/reportService'

/**
 * Holds the admin/moderator panel state: report queue with filter +
 * multi-select, audit log paging, and the currently-selected report.
 *
 * One store covers all three admin views since they share filters and
 * selection state when bouncing between the queue and the detail drawer.
 */
export const useAdminStore = defineStore('admin', () => {
  // ── Report queue ──────────────────────────────────────────────────────────

  const reports = ref<ReportDto[]>([])
  const reportsPage = ref(1)
  const reportsPageSize = ref(20)
  const reportsHasMore = ref(false)
  const reportsLoading = ref(false)
  const reportsError = ref<string | null>(null)

  const statusFilter = ref<ReportStatus | null>('Pending')
  const typeFilter = ref<ReportTargetType | null>(null)

  /** Selected report IDs for the multi-select toolbar. */
  const selectedIds = ref<Set<string>>(new Set())

  const allSelected = computed(() =>
    reports.value.length > 0 && reports.value.every(r => selectedIds.value.has(r.id)),
  )
  const selectionCount = computed(() => selectedIds.value.size)

  async function loadReports(page = 1) {
    reportsLoading.value = true
    reportsError.value = null
    try {
      const result = await adminService.listReports(
        page,
        reportsPageSize.value,
        statusFilter.value,
        typeFilter.value,
      )
      reports.value = result.items
      reportsPage.value = result.page
      reportsHasMore.value = result.hasMore
      // Selection invalidates whenever the underlying page changes
      selectedIds.value = new Set()
    } catch (e: unknown) {
      reportsError.value = extractError(e)
      throw e
    } finally {
      reportsLoading.value = false
    }
  }

  function setStatusFilter(s: ReportStatus | null) {
    statusFilter.value = s
  }

  function setTypeFilter(t: ReportTargetType | null) {
    typeFilter.value = t
  }

  function toggleSelected(id: string) {
    const next = new Set(selectedIds.value)
    if (next.has(id)) next.delete(id)
    else next.add(id)
    selectedIds.value = next
  }

  function selectAll() {
    selectedIds.value = new Set(reports.value.map(r => r.id))
  }

  function clearSelection() {
    selectedIds.value = new Set()
  }

  function toggleAll() {
    if (allSelected.value) clearSelection()
    else selectAll()
  }

  async function bulkUpdateStatus(status: ReportStatus) {
    const ids = Array.from(selectedIds.value)
    if (ids.length === 0) return
    await adminService.bulkUpdateReportStatus(ids, status)
    // Drop them locally — they're no longer Pending and would be filtered out
    // on the next reload anyway. Reloading also refreshes the pagination.
    await loadReports(reportsPage.value)
  }

  async function updateReportStatus(id: string, status: ReportStatus) {
    const updated = await adminService.updateReportStatus(id, status)
    const idx = reports.value.findIndex(r => r.id === id)
    if (idx >= 0) reports.value[idx] = updated
    return updated
  }

  // ── Selected report drawer ────────────────────────────────────────────────

  const selectedReport = ref<ReportDto | null>(null)
  const drawerOpen = ref(false)

  function openDrawer(report: ReportDto) {
    selectedReport.value = report
    drawerOpen.value = true
  }

  function closeDrawer() {
    drawerOpen.value = false
    // Defer clearing until after the close animation so the drawer
    // doesn't blank out as it slides off
    setTimeout(() => {
      if (!drawerOpen.value) selectedReport.value = null
    }, 200)
  }

  // ── Users ────────────────────────────────────────────────────────────────

  const users = ref<AdminUserDto[]>([])
  const usersPage = ref(1)
  const usersPageSize = ref(20)
  const usersHasMore = ref(false)
  const usersQuery = ref<string>('')
  const usersLoading = ref(false)

  function upsertUser(u: AdminUserDto) {
    const idx = users.value.findIndex(x => x.id === u.id)
    if (idx >= 0) users.value[idx] = u
    else users.value.unshift(u)
  }

  async function loadUsers(page = 1) {
    usersLoading.value = true
    try {
      const result = await adminService.listUsers(page, usersPageSize.value, usersQuery.value || null)
      users.value = result.items
      usersPage.value = result.page
      usersHasMore.value = result.hasMore
    } finally {
      usersLoading.value = false
    }
  }

  function setUsersQuery(q: string) {
    usersQuery.value = q
  }

  async function changeUserRole(userId: string, role: import('@/types').UserRole) {
    const u = await adminService.changeUserRole(userId, role)
    upsertUser(u)
    return u
  }

  async function banUser(userId: string, notes?: string) {
    const u = await adminService.banUser(userId, notes ?? null)
    upsertUser(u)
    return u
  }

  async function unbanUser(userId: string, notes?: string) {
    const u = await adminService.unbanUser(userId, notes ?? null)
    upsertUser(u)
    return u
  }

  // ── Audit log ────────────────────────────────────────────────────────────

  const auditLog = ref<ModerationLogDto[]>([])
  const auditPage = ref(1)
  const auditPageSize = ref(20)
  const auditHasMore = ref(false)
  const auditLoading = ref(false)
  const auditActorFilter = ref<string | null>(null)
  const auditActionFilter = ref<string | null>(null)

  async function loadAuditLog(page = 1) {
    auditLoading.value = true
    try {
      const result = await adminService.getAuditLog(
        page,
        auditPageSize.value,
        auditActorFilter.value,
        auditActionFilter.value,
      )
      auditLog.value = result.items
      auditPage.value = result.page
      auditHasMore.value = result.hasMore
    } finally {
      auditLoading.value = false
    }
  }

  function reset() {
    reports.value = []
    reportsPage.value = 1
    reportsHasMore.value = false
    selectedIds.value = new Set()
    selectedReport.value = null
    drawerOpen.value = false
    users.value = []
    auditLog.value = []
    auditPage.value = 1
    auditHasMore.value = false
  }

  return {
    // reports queue
    reports,
    reportsPage,
    reportsPageSize,
    reportsHasMore,
    reportsLoading,
    reportsError,
    statusFilter,
    typeFilter,
    selectedIds,
    allSelected,
    selectionCount,
    loadReports,
    setStatusFilter,
    setTypeFilter,
    toggleSelected,
    selectAll,
    clearSelection,
    toggleAll,
    bulkUpdateStatus,
    updateReportStatus,
    // drawer
    selectedReport,
    drawerOpen,
    openDrawer,
    closeDrawer,
    // users
    users,
    usersPage,
    usersPageSize,
    usersHasMore,
    usersQuery,
    usersLoading,
    loadUsers,
    setUsersQuery,
    changeUserRole,
    banUser,
    unbanUser,
    // audit log
    auditLog,
    auditPage,
    auditPageSize,
    auditHasMore,
    auditLoading,
    auditActorFilter,
    auditActionFilter,
    loadAuditLog,
    // util
    reset,
  }
})

// ── Helpers ───────────────────────────────────────────────────────────────────

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const ax = e as { response?: { data?: { error?: string } }; message?: string }
    return ax.response?.data?.error ?? ax.message ?? 'An unexpected error occurred.'
  }
  return 'An unexpected error occurred.'
}
