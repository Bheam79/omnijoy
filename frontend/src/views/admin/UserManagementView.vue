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
    case 'Moderator': return 'bg-yellow-100 text-yellow-800'
    case 'User':      return 'bg-gray-100 text-gray-700'
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
    <h1 class="text-2xl font-bold text-gray-900">User management</h1>

    <!-- Search -->
    <form class="flex gap-2" @submit.prevent="runSearch">
      <input
        v-model="searchInput"
        type="search"
        placeholder="Search by name or email…"
        aria-label="Search users"
        class="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500"
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
      class="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2"
      role="alert"
    >
      {{ actionError }}
    </p>

    <!-- Table -->
    <div v-if="store.usersLoading" class="text-sm text-gray-500">Loading users…</div>
    <div
      v-else-if="store.users.length === 0"
      class="bg-white border border-gray-200 rounded-xl p-8 text-center text-gray-500"
    >
      No users found.
    </div>
    <div v-else class="overflow-x-auto bg-white border border-gray-200 rounded-xl">
      <table class="min-w-full text-sm" aria-label="Users">
        <thead class="bg-gray-50 text-gray-600">
          <tr>
            <th class="px-3 py-2 text-left">Name</th>
            <th class="px-3 py-2 text-left">Email</th>
            <th class="px-3 py-2 text-left">Role</th>
            <th class="px-3 py-2 text-left">Status</th>
            <th class="px-3 py-2 text-right">Actions</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="u in store.users" :key="u.id" class="hover:bg-gray-50">
            <td class="px-3 py-2 text-gray-800">{{ u.displayName }}</td>
            <td class="px-3 py-2 text-gray-600">{{ u.email }}</td>
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
                  class="border border-gray-300 rounded-lg px-2 py-1 text-xs focus:ring-2 focus:ring-blue-500"
                  @change="setRole(u, $event)"
                >
                  <option value="User">User</option>
                  <option value="Moderator">Moderator</option>
                  <option value="Admin">Admin</option>
                </select>

                <button
                  class="px-3 py-1 text-xs font-medium rounded-lg disabled:opacity-50"
                  :class="u.isBanned
                    ? 'text-green-700 border border-green-300 hover:bg-green-50'
                    : 'text-red-700 border border-red-300 hover:bg-red-50'"
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
        class="px-3 py-1.5 text-sm font-medium text-gray-700 border border-gray-300 rounded-lg disabled:opacity-50"
        :disabled="store.usersPage === 1 || store.usersLoading"
        @click="changePage(-1)"
      >
        Previous
      </button>
      <span class="text-sm text-gray-600">Page {{ store.usersPage }}</span>
      <button
        class="px-3 py-1.5 text-sm font-medium text-gray-700 border border-gray-300 rounded-lg disabled:opacity-50"
        :disabled="!store.usersHasMore || store.usersLoading"
        @click="changePage(1)"
      >
        Next
      </button>
    </div>
  </div>
</template>
