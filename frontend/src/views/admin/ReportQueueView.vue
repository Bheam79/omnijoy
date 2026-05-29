<script setup lang="ts">
import { onMounted, computed } from 'vue'
import { useAdminStore } from '@/stores/admin'
import type { ReportStatus } from '@/services/adminService'
import type { ReportDto, ReportTargetType } from '@/services/reportService'
import ReportDetailDrawer from '@/components/admin/ReportDetailDrawer.vue'

const store = useAdminStore()

onMounted(() => {
  // Errors are surfaced via store.reportsError; swallow the rejection here
  // so onMounted doesn't propagate an unhandled rejection.
  store.loadReports(1).catch(() => {})
})

const statusOptions: { value: ReportStatus | null; label: string }[] = [
  { value: 'Pending',   label: 'Pending' },
  { value: 'Reviewed',  label: 'Reviewed' },
  { value: 'Dismissed', label: 'Dismissed' },
  { value: null,        label: 'All' },
]

const typeOptions: { value: ReportTargetType | null; label: string }[] = [
  { value: null,      label: 'All types' },
  { value: 'Post',    label: 'Post' },
  { value: 'User',    label: 'User' },
  { value: 'Comment', label: 'Comment' },
]

async function onStatusChange(s: ReportStatus | null) {
  store.setStatusFilter(s)
  await store.loadReports(1)
}

async function onTypeChange(t: ReportTargetType | null) {
  store.setTypeFilter(t)
  await store.loadReports(1)
}

async function changePage(delta: number) {
  const next = store.reportsPage + delta
  if (next < 1) return
  await store.loadReports(next)
}

async function bulkDismiss() {
  await store.bulkUpdateStatus('Dismissed')
}
async function bulkReview() {
  await store.bulkUpdateStatus('Reviewed')
}

function badgeClass(type: ReportTargetType): string {
  switch (type) {
    case 'Post':    return 'bg-blue-900 text-blue-300'
    case 'User':    return 'bg-purple-900 text-purple-800'
    case 'Comment': return 'bg-amber-100 text-amber-800'
  }
}

function statusBadgeClass(status: ReportStatus): string {
  switch (status) {
    case 'Pending':   return 'bg-yellow-900/50 text-yellow-300'
    case 'Reviewed':  return 'bg-green-100 text-green-800'
    case 'Dismissed': return 'bg-slate-700 text-slate-300'
  }
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

const hasReports = computed(() => store.reports.length > 0)

function openDetail(report: ReportDto) {
  store.openDrawer(report)
}
</script>

<template>
  <div class="space-y-4">
    <header class="flex items-center justify-between">
      <h1 class="text-2xl font-bold text-slate-100">Report queue</h1>
    </header>

    <!-- Filters -->
    <div class="flex flex-wrap items-center gap-2">
      <label class="text-sm font-medium text-slate-400">Status</label>
      <select
        :value="store.statusFilter ?? ''"
        aria-label="Filter by status"
        class="border border-slate-600 rounded-lg px-3 py-1.5 text-sm focus:ring-2 focus:ring-blue-500"
        @change="(e) => onStatusChange(((e.target as HTMLSelectElement).value || null) as ReportStatus | null)"
      >
        <option v-for="opt in statusOptions" :key="String(opt.value)" :value="opt.value ?? ''">
          {{ opt.label }}
        </option>
      </select>

      <label class="text-sm font-medium text-slate-400 ml-3">Type</label>
      <select
        :value="store.typeFilter ?? ''"
        aria-label="Filter by type"
        class="border border-slate-600 rounded-lg px-3 py-1.5 text-sm focus:ring-2 focus:ring-blue-500"
        @change="(e) => onTypeChange(((e.target as HTMLSelectElement).value || null) as ReportTargetType | null)"
      >
        <option v-for="opt in typeOptions" :key="String(opt.value)" :value="opt.value ?? ''">
          {{ opt.label }}
        </option>
      </select>
    </div>

    <!-- Bulk action toolbar -->
    <div
      v-if="store.selectionCount > 0"
      class="flex items-center justify-between bg-indigo-900/50 border border-indigo-700 rounded-lg px-4 py-2"
      aria-label="Bulk actions"
    >
      <span class="text-sm font-medium text-indigo-800">
        {{ store.selectionCount }} selected
      </span>
      <div class="flex gap-2">
        <button
          class="px-3 py-1.5 text-sm font-medium text-white bg-green-600 hover:bg-green-700 rounded-lg"
          @click="bulkReview"
        >
          Mark Reviewed
        </button>
        <button
          class="px-3 py-1.5 text-sm font-medium text-white bg-gray-600 hover:bg-gray-700 rounded-lg"
          @click="bulkDismiss"
        >
          Dismiss
        </button>
        <button
          class="px-3 py-1.5 text-sm font-medium text-slate-300 hover:bg-slate-700 rounded-lg"
          @click="store.clearSelection"
        >
          Clear
        </button>
      </div>
    </div>

    <!-- Loading / error -->
    <div v-if="store.reportsLoading" class="text-sm text-gray-500">Loading reports…</div>
    <div
      v-else-if="store.reportsError"
      class="text-sm text-red-400 bg-red-950 border border-red-800 rounded-lg px-3 py-2"
    >
      {{ store.reportsError }}
    </div>

    <!-- Table -->
    <div
      v-else-if="hasReports"
      class="overflow-x-auto bg-slate-800 border border-slate-700 rounded-xl"
    >
      <table class="min-w-full text-sm" aria-label="Reports queue">
        <thead class="bg-slate-950 text-slate-400">
          <tr>
            <th class="px-3 py-2 text-left w-10">
              <input
                type="checkbox"
                aria-label="Select all reports"
                :checked="store.allSelected"
                @change="store.toggleAll"
              >
            </th>
            <th class="px-3 py-2 text-left">Type</th>
            <th class="px-3 py-2 text-left">Excerpt</th>
            <th class="px-3 py-2 text-left">Reason</th>
            <th class="px-3 py-2 text-left">Reporter</th>
            <th class="px-3 py-2 text-left">Status</th>
            <th class="px-3 py-2 text-left">Date</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-700">
          <tr
            v-for="report in store.reports"
            :key="report.id"
            class="hover:bg-slate-700 cursor-pointer"
            @click="openDetail(report)"
          >
            <td class="px-3 py-2" @click.stop>
              <input
                type="checkbox"
                :aria-label="`Select report ${report.id}`"
                :checked="store.selectedIds.has(report.id)"
                @change="store.toggleSelected(report.id)"
              >
            </td>
            <td class="px-3 py-2">
              <span
                class="inline-block px-2 py-0.5 text-xs font-semibold rounded"
                :class="badgeClass(report.targetType)"
              >
                {{ report.targetType }}
              </span>
            </td>
            <td class="px-3 py-2 max-w-xs">
              <span class="block truncate text-slate-300">
                {{ report.notes || `Target: ${report.targetId.substring(0, 8)}…` }}
              </span>
            </td>
            <td class="px-3 py-2 text-slate-300">{{ report.reason }}</td>
            <td class="px-3 py-2 text-slate-300">{{ report.reporterName }}</td>
            <td class="px-3 py-2">
              <span
                class="inline-block px-2 py-0.5 text-xs font-semibold rounded"
                :class="statusBadgeClass(report.status)"
              >
                {{ report.status }}
              </span>
            </td>
            <td class="px-3 py-2 text-gray-500 whitespace-nowrap">
              {{ formatDate(report.createdAt) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Empty state -->
    <div
      v-else
      class="bg-slate-800 border border-slate-700 rounded-xl p-8 text-center text-gray-500"
    >
      No reports match the current filters.
    </div>

    <!-- Pagination -->
    <div v-if="hasReports" class="flex items-center justify-between">
      <button
        class="px-3 py-1.5 text-sm font-medium text-slate-300 border border-slate-600 rounded-lg disabled:opacity-50"
        :disabled="store.reportsPage === 1 || store.reportsLoading"
        @click="changePage(-1)"
      >
        Previous
      </button>
      <span class="text-sm text-slate-400">Page {{ store.reportsPage }}</span>
      <button
        class="px-3 py-1.5 text-sm font-medium text-slate-300 border border-slate-600 rounded-lg disabled:opacity-50"
        :disabled="!store.reportsHasMore || store.reportsLoading"
        @click="changePage(1)"
      >
        Next
      </button>
    </div>

    <!-- Detail drawer -->
    <ReportDetailDrawer />
  </div>
</template>
