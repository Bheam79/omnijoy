<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'
import { useAdminStore } from '@/stores/admin'
import { useAuthStore } from '@/stores/auth'
import type { UserRole } from '@/types'
import type { AdminUserDto } from '@/services/adminService'

const store = useAdminStore()
const auth  = useAuthStore()

const isAdmin = computed(() => auth.user?.role === 'Admin')

const searchInput = ref('')
const actingId = ref<string | null>(null)
const actionError = ref<string | null>(null)

onMounted(() => {
  store.loadUsers(1)
})

async function runSearch() {
  store.setUsersQuery(searchInput.value.trim())
  await store.loadUsers(1)
}

async function changePage(delta: number) {
  const next = store.usersPage + delta
  if (next < 1) return
  await store.loadUsers(next)
}

async function setRole(user: AdminUserDto, e: Event) {
  const role = (e.target as HTMLSelectElement).value as UserRole
  if (role === user.role) return
  actingId.value = user.id
  actionError.value = null
  try {
    await store.changeUserRole(user.id, role)
  } catch (err) {
    actionError.value = extractError(err)
  } finally {
    actingId.value = null
  }
}

async function toggleBan(user: AdminUserDto) {
  actingId.value = user.id
  actionError.value = null
  try {
    if (user.isBanned) await store.unbanUser(user.id)
    else               await store.banUser(user.id)
  } catch (err) {
    actionError.value = extractError(err)
  } finally {
    actingId.value = null
  }
}

function roleBadgeClass(role: UserRole): string {
  switch (role) {
    case 'Admin':     return 'bg-red-100 text-red-800'
    case 'Moderator': return 'bg-yellow-900/50 text-yellow-300'
    case 'User':      return 'bg-slate-700 text-slate-300'
  }
}

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const ax = e as { response?: { data?: { error?: string } }; message?: string }
    return ax.response?.data?.error ?? ax.message ?? 'An unexpected error occurred.'
  }
  return 'An unexpected error occurred.'
}
</script>

<template>
  <div class="space-y-4">
    <h1 class="text-2xl font-bold text-slate-100">User management</h1>

    <!-- Search -->
    <form class="flex gap-2" @submit.prevent="runSearch">
      <input
        v-model="searchInput"
        type="search"
        placeholder="Search by name or email…"
        aria-label="Search users"
        class="flex-1 border border-slate-600 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500"
      >
      <button
        type="submit"
        class="px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg"
      >
        Search
      </button>
    </form>

    <p
      v-if="actionError"
      class="text-sm text-red-400 bg-red-950 border border-red-800 rounded-lg px-3 py-2"
      role="alert"
    >
      {{ actionError }}
    </p>

    <!-- Table -->
    <div v-if="store.usersLoading" class="text-sm text-gray-500">Loading users…</div>
    <div
      v-else-if="store.users.length === 0"
      class="bg-slate-800 border border-slate-700 rounded-xl p-8 text-center text-gray-500"
    >
      No users found.
    </div>
    <div v-else class="overflow-x-auto bg-slate-800 border border-slate-700 rounded-xl">
      <table class="min-w-full text-sm" aria-label="Users">
        <thead class="bg-slate-950 text-slate-400">
          <tr>
            <th class="px-3 py-2 text-left">Name</th>
            <th class="px-3 py-2 text-left">Email</th>
            <th class="px-3 py-2 text-left">Role</th>
            <th class="px-3 py-2 text-left">Status</th>
            <th class="px-3 py-2 text-right">Actions</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-700">
          <tr v-for="u in store.users" :key="u.id" class="hover:bg-slate-700">
            <td class="px-3 py-2 text-slate-200">{{ u.displayName }}</td>
            <td class="px-3 py-2 text-slate-400">{{ u.email }}</td>
            <td class="px-3 py-2">
              <span
                class="inline-block px-2 py-0.5 text-xs font-semibold rounded"
                :class="roleBadgeClass(u.role)"
              >
                {{ u.role }}
              </span>
            </td>
            <td class="px-3 py-2">
              <span
                v-if="u.isBanned"
                class="inline-block px-2 py-0.5 text-xs font-semibold rounded bg-red-100 text-red-800"
              >
                Banned
              </span>
              <span
                v-else
                class="inline-block px-2 py-0.5 text-xs font-semibold rounded bg-green-100 text-green-800"
              >
                Active
              </span>
            </td>
            <td class="px-3 py-2">
              <div class="flex items-center justify-end gap-2">
                <select
                  v-if="isAdmin"
                  :value="u.role"
                  :aria-label="`Change role for ${u.displayName}`"
                  :disabled="actingId === u.id"
                  class="border border-slate-600 rounded-lg px-2 py-1 text-xs focus:ring-2 focus:ring-blue-500"
                  @change="setRole(u, $event)"
                >
                  <option value="User">User</option>
                  <option value="Moderator">Moderator</option>
                  <option value="Admin">Admin</option>
                </select>

                <button
                  class="px-3 py-1 text-xs font-medium rounded-lg disabled:opacity-50"
                  :class="u.isBanned
                    ? 'text-green-400 border border-green-300 hover:bg-green-950'
                    : 'text-red-400 border border-red-300 hover:bg-red-950'"
                  :disabled="actingId === u.id"
                  :aria-label="u.isBanned ? `Unban ${u.displayName}` : `Ban ${u.displayName}`"
                  @click="toggleBan(u)"
                >
                  {{ u.isBanned ? 'Unban' : 'Ban' }}
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Pagination -->
    <div v-if="store.users.length > 0" class="flex items-center justify-between">
      <button
        class="px-3 py-1.5 text-sm font-medium text-slate-300 border border-slate-600 rounded-lg disabled:opacity-50"
        :disabled="store.usersPage === 1 || store.usersLoading"
        @click="changePage(-1)"
      >
        Previous
      </button>
      <span class="text-sm text-slate-400">Page {{ store.usersPage }}</span>
      <button
        class="px-3 py-1.5 text-sm font-medium text-slate-300 border border-slate-600 rounded-lg disabled:opacity-50"
        :disabled="!store.usersHasMore || store.usersLoading"
        @click="changePage(1)"
      >
        Next
      </button>
    </div>
  </div>
</template>
