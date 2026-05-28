<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import TopNav from './TopNav.vue'
import Sidebar from './Sidebar.vue'

const sidebarOpen = ref(false)
const route = useRoute()

// Auto-close sidebar on mobile when navigating to a new route
watch(() => route.path, () => {
  sidebarOpen.value = false
})
</script>

<template>
  <div class="min-h-screen bg-gray-50">
    <!-- Fixed top navigation -->
    <TopNav
      :sidebar-open="sidebarOpen"
      @toggle-sidebar="sidebarOpen = !sidebarOpen"
    />

    <!-- Left sidebar (fixed; mobile: slide-in drawer) -->
    <Sidebar
      :open="sidebarOpen"
      @close="sidebarOpen = false"
    />

    <!-- Main content
         pt-16  → clears the fixed 64px top nav
         lg:pl-64 → clears the 256px sidebar on large screens -->
    <main class="pt-16 lg:pl-64 min-h-screen">
      <div class="max-w-3xl mx-auto px-4 py-6">
        <slot />
      </div>
    </main>
  </div>
</template>
