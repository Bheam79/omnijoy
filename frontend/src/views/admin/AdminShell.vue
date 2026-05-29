<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const auth  = useAuthStore()

const isAdmin = computed(() => auth.user?.role === 'Admin')

interface NavItem {
  label: string
  to: string
  testid: string
  /** When true, only Admin role sees this link. */
  adminOnly?: boolean
}

const navItems = computed<NavItem[]>(() => {
  const all: NavItem[] = [
    { label: 'Reports',   to: '/admin/reports',   testid: 'admin-nav-reports' },
    { label: 'Users',     to: '/admin/users',     testid: 'admin-nav-users' },
    { label: 'Audit Log', to: '/admin/audit-log', testid: 'admin-nav-audit', adminOnly: true },
  ]
  return all.filter(i => !i.adminOnly || isAdmin.value)
})

function isActive(item: NavItem): boolean {
  return route.path === item.to || route.path.startsWith(item.to + '/')
}
</script>

<template>
  <div class="flex gap-6">
    <!-- Admin-section sidebar -->
    <aside class="w-56 shrink-0 hidden md:block">
      <div class="bg-slate-800 border border-slate-700 rounded-xl p-2">
        <div class="px-3 py-2 text-xs font-semibold text-gray-500 uppercase tracking-wide">
          Admin
        </div>
        <nav class="space-y-0.5">
          <RouterLink
            v-for="item in navItems"
            :key="item.to"
            :to="item.to"
            :data-testid="item.testid"
            class="block px-3 py-2 rounded-lg text-sm font-medium transition-colors"
            :class="isActive(item)
              ? 'bg-indigo-900/50 text-indigo-300'
              : 'text-slate-300 hover:bg-slate-700'"
          >
            {{ item.label }}
          </RouterLink>
        </nav>
      </div>
    </aside>

    <!-- Routed content -->
    <div class="flex-1 min-w-0">
      <RouterView />
    </div>
  </div>
</template>
