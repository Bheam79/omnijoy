<script setup lang="ts">
import { onMounted } from 'vue'
import { useAdminStore } from '@/stores/admin'

const store = useAdminStore()

const actionOptions: { value: string | null; label: string }[] = [
  { value: null,            label: 'All actions' },
  { value: 'BanUser',       label: 'Ban user' },
  { value: 'UnbanUser',     label: 'Unban user' },
  { value: 'ChangeRole',    label: 'Change role' },
  { value: 'DismissReport', label: 'Dismiss report' },
  { value: 'ReviewReport',  label: 'Review report' },
  { value: 'RemovePost',    label: 'Remove post' },
  { value: 'RemoveComment', label: 'Remove comment' },
]

onMounted(() => {
  store.loadAuditLog(1)
})

async function onActionChange(e: Event) {
  const value = (e.target as HTMLSelectElement).value
  store.auditActionFilter = value || null
  await store.loadAuditLog(1)
}

async function changePage(delta: number) {
  const next = store.auditPage + delta
  if (next < 1) return
  await store.loadAuditLog(next)
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString()
}
</script>

<template>
  <div class="space-y-4">
    <h1 class="text-2xl font-bold text-gray-900">Audit log</h1>

    <!-- Filter -->
    <div class="flex items-center gap-2">
      <label class="text-sm font-medium text-gray-600">Action</label>
      <select
        :value="store.auditActionFilter ?? ''"
        aria-label="Filter by action"
        class="border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:ring-2 focus:ring-blue-500"
        @change="onActionChange"
      >
        <option v-for="opt in actionOptions" :key="String(opt.value)" :value="opt.value ?? ''">
          {{ opt.label }}
        </option>
      </select>
    </div>

    <!-- Loading -->
    <div v-if="store.auditLoading" class="text-sm text-gray-500">Loading audit log…</div>
    <div
      v-else-if="store.auditLog.length === 0"
      class="bg-white border border-gray-200 rounded-xl p-8 text-center text-gray-500"
    >
      No audit entries match the current filter.
    </div>
    <div v-else class="overflow-x-auto bg-white border border-gray-200 rounded-xl">
      <table class="min-w-full text-sm" aria-label="Audit log">
        <thead class="bg-gray-50 text-gray-600">
          <tr>
            <th class="px-3 py-2 text-left">When</th>
            <th class="px-3 py-2 text-left">Actor</th>
            <th class="px-3 py-2 text-left">Action</th>
            <th class="px-3 py-2 text-left">Target</th>
            <th class="px-3 py-2 text-left">Notes</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="entry in store.auditLog" :key="entry.id" class="hover:bg-gray-50">
            <td class="px-3 py-2 text-gray-500 whitespace-nowrap">
              {{ formatDate(entry.createdAt) }}
            </td>
            <td class="px-3 py-2 text-gray-800">{{ entry.actorDisplayName }}</td>
            <td class="px-3 py-2">
              <span class="inline-block px-2 py-0.5 text-xs font-semibold rounded bg-indigo-100 text-indigo-800">
                {{ entry.action }}
              </span>
            </td>
            <td class="px-3 py-2 text-gray-700">
              {{ entry.targetType }}
              <span class="font-mono text-xs text-gray-500">{{ entry.targetId.substring(0, 8) }}</span>
            </td>
            <td class="px-3 py-2 text-gray-700 max-w-xs truncate">
              {{ entry.notes || '—' }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Pagination -->
    <div v-if="store.auditLog.length > 0" class="flex items-center justify-between">
      <button
        class="px-3 py-1.5 text-sm font-medium text-gray-700 border border-gray-300 rounded-lg disabled:opacity-50"
        :disabled="store.auditPage === 1 || store.auditLoading"
        @click="changePage(-1)"
      >
        Previous
      </button>
      <span class="text-sm text-gray-600">Page {{ store.auditPage }}</span>
      <button
        class="px-3 py-1.5 text-sm font-medium text-gray-700 border border-gray-300 rounded-lg disabled:opacity-50"
        :disabled="!store.auditHasMore || store.auditLoading"
        @click="changePage(1)"
      >
        Next
      </button>
    </div>
  </div>
</template>
