<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { useAdminStore } from '@/stores/admin'
import { adminService } from '@/services/adminService'

const store = useAdminStore()

const acting = ref(false)
const actionError = ref<string | null>(null)
const actionSuccess = ref<string | null>(null)

// Prior-report count for the same target (excluding the current report).
const priorReportCount = ref<number | null>(null)
const loadingPriorCount = ref(false)

// Re-fetch the prior-report count whenever the drawer opens to a new report.
watch(
  () => store.selectedReport,
  async (report) => {
    actionError.value   = null
    actionSuccess.value = null
    priorReportCount.value = null
    if (!report) return

    loadingPriorCount.value = true
    try {
      // List Pending+Reviewed+Dismissed reports against the same target type,
      // then count those matching the same targetId minus the current one.
      // We page through results until we either find the current one or page
      // out — for the drawer's display we cap at one page (20).
      const list = await adminService.listReports(1, 20, null, report.targetType)
      priorReportCount.value = list.items.filter(
        r => r.targetId === report.targetId && r.id !== report.id,
      ).length
    } catch {
      priorReportCount.value = null
    } finally {
      loadingPriorCount.value = false
    }
  },
)

const open = computed(() => store.drawerOpen)
const report = computed(() => store.selectedReport)

async function dismiss() {
  if (!report.value) return
  acting.value = true
  actionError.value = null
  try {
    await store.updateReportStatus(report.value.id, 'Dismissed')
    actionSuccess.value = 'Report dismissed.'
  } catch (e: unknown) {
    actionError.value = extractError(e)
  } finally {
    acting.value = false
  }
}

async function markReviewed() {
  if (!report.value) return
  acting.value = true
  actionError.value = null
  try {
    await store.updateReportStatus(report.value.id, 'Reviewed')
    actionSuccess.value = 'Marked as reviewed.'
  } catch (e: unknown) {
    actionError.value = extractError(e)
  } finally {
    acting.value = false
  }
}

async function banUserAndReview() {
  if (!report.value) return
  acting.value = true
  actionError.value = null
  try {
    // For Post / Comment reports, the targetId is the post/comment id, not
    // the author's user id, so this action is only available on User reports.
    if (report.value.targetType !== 'User') {
      actionError.value = 'Ban is only available for reports targeting a user.'
      return
    }
    await store.banUser(report.value.targetId, `From report ${report.value.id}`)
    await store.updateReportStatus(report.value.id, 'Reviewed')
    actionSuccess.value = 'User banned and report marked reviewed.'
  } catch (e: unknown) {
    actionError.value = extractError(e)
  } finally {
    acting.value = false
  }
}

async function removeContent() {
  // Post/comment removal endpoints are not yet wired through the admin
  // service — surface a clear message so the moderator knows to use the
  // owner-side delete or upcoming bulk removal flow. The report itself
  // is still marked Reviewed so it leaves the queue.
  if (!report.value) return
  acting.value = true
  actionError.value = null
  try {
    await store.updateReportStatus(report.value.id, 'Reviewed')
    actionSuccess.value = report.value.targetType === 'User'
      ? 'Report reviewed.'
      : 'Report reviewed. Use the content removal flow to take down the item.'
  } catch (e: unknown) {
    actionError.value = extractError(e)
  } finally {
    acting.value = false
  }
}

function close() {
  store.closeDrawer()
}

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const ax = e as { response?: { data?: { error?: string } }; message?: string }
    return ax.response?.data?.error ?? ax.message ?? 'An unexpected error occurred.'
  }
  return 'An unexpected error occurred.'
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { hour12: false })
}
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity ease-out duration-200"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition-opacity ease-in duration-150"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="open"
        class="fixed inset-0 z-50 bg-black/40"
        @click.self="close"
      />
    </Transition>

    <Transition
      enter-active-class="transition-transform ease-out duration-200"
      enter-from-class="translate-x-full"
      enter-to-class="translate-x-0"
      leave-active-class="transition-transform ease-in duration-150"
      leave-from-class="translate-x-0"
      leave-to-class="translate-x-full"
    >
      <aside
        v-if="open"
        class="fixed top-0 right-0 bottom-0 z-50 w-full max-w-md bg-slate-800 shadow-xl overflow-y-auto"
        role="dialog"
        aria-modal="true"
        aria-label="Report detail"
      >
        <div v-if="report" class="flex flex-col h-full">
          <!-- Header -->
          <div class="flex items-center justify-between px-5 py-4 border-b border-slate-700">
            <h2 class="text-lg font-semibold text-slate-100">Report detail</h2>
            <button
              class="text-slate-500 hover:text-slate-400 p-1 rounded-full hover:bg-slate-700"
              aria-label="Close report drawer"
              @click="close"
            >
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>

          <!-- Body -->
          <div class="flex-1 px-5 py-4 space-y-4">
            <!-- Target summary -->
            <section>
              <h3 class="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">
                Reported content
              </h3>
              <div class="bg-slate-950 border border-slate-700 rounded-lg p-3 text-sm">
                <p class="text-slate-300">
                  <span class="font-medium">Type:</span> {{ report.targetType }}
                </p>
                <p class="text-slate-300 font-mono text-xs break-all">
                  <span class="font-medium font-sans">ID:</span> {{ report.targetId }}
                </p>
              </div>
            </section>

            <!-- Reporter -->
            <section>
              <h3 class="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">
                Reporter
              </h3>
              <p class="text-sm text-slate-200">
                {{ report.reporterName }}
                <span class="text-gray-500 ml-2 text-xs">
                  · {{ formatDate(report.createdAt) }}
                </span>
              </p>
            </section>

            <!-- Reason + notes -->
            <section>
              <h3 class="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">
                Reason
              </h3>
              <p class="text-sm font-medium text-slate-200">{{ report.reason }}</p>
              <p
                v-if="report.notes"
                class="text-sm text-slate-300 mt-2 whitespace-pre-wrap"
              >
                {{ report.notes }}
              </p>
            </section>

            <!-- Prior reports -->
            <section>
              <h3 class="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">
                Other reports on this target
              </h3>
              <p v-if="loadingPriorCount" class="text-sm text-gray-500">Loading…</p>
              <p v-else-if="priorReportCount === null" class="text-sm text-gray-500">
                Unable to load count.
              </p>
              <p v-else class="text-sm text-slate-200">
                {{ priorReportCount }} other report{{ priorReportCount === 1 ? '' : 's' }}
              </p>
            </section>

            <!-- Status -->
            <section>
              <h3 class="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">
                Current status
              </h3>
              <p class="text-sm font-medium text-slate-200">{{ report.status }}</p>
            </section>

            <!-- Errors / success -->
            <p
              v-if="actionError"
              class="text-sm text-red-400 bg-red-950 border border-red-800 rounded-lg px-3 py-2"
              role="alert"
            >
              {{ actionError }}
            </p>
            <p
              v-if="actionSuccess"
              class="text-sm text-green-400 bg-green-950 border border-green-800 rounded-lg px-3 py-2"
              role="status"
            >
              {{ actionSuccess }}
            </p>
          </div>

          <!-- Footer actions -->
          <div class="px-5 py-3 border-t border-slate-700 grid grid-cols-3 gap-2">
            <button
              class="px-3 py-2 text-sm font-medium text-white bg-red-600 hover:bg-red-700 rounded-lg disabled:opacity-50"
              :disabled="acting"
              @click="removeContent"
            >
              Remove
            </button>
            <button
              class="px-3 py-2 text-sm font-medium text-white bg-orange-600 hover:bg-orange-700 rounded-lg disabled:opacity-50"
              :disabled="acting || report.targetType !== 'User'"
              :title="report.targetType !== 'User' ? 'Ban is only available for user reports' : ''"
              @click="banUserAndReview"
            >
              Ban User
            </button>
            <button
              class="px-3 py-2 text-sm font-medium text-white bg-gray-600 hover:bg-gray-700 rounded-lg disabled:opacity-50"
              :disabled="acting"
              @click="dismiss"
            >
              Dismiss
            </button>
            <button
              class="col-span-3 px-3 py-2 text-sm font-medium text-green-400 border border-green-300 hover:bg-green-950 rounded-lg disabled:opacity-50"
              :disabled="acting"
              @click="markReviewed"
            >
              Mark Reviewed
            </button>
          </div>
        </div>
      </aside>
    </Transition>
  </Teleport>
</template>
