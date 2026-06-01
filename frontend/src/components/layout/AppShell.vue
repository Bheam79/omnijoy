<script setup lang="ts">
import { ref, watch, onMounted, computed } from 'vue'
import { useRoute } from 'vue-router'
import TopNav from './TopNav.vue'
import Sidebar from './Sidebar.vue'
import CompanySidebar from './CompanySidebar.vue'
import MessengerPopup from '@/components/chat/MessengerPopup.vue'
import { useLiveStore } from '@/stores/live'
import { useCompanyModeStore } from '@/stores/companyMode'

const sidebarOpen = ref(false)
const route = useRoute()
const liveStore = useLiveStore()
const companyMode = useCompanyModeStore()

/** Wide-content routes manage their own max-width; standard routes use the default wrapper. */
const isWideContent = computed(() => !!route.meta.wideContent)

// Auto-close sidebar on mobile when navigating to a new route
watch(() => route.path, () => {
  sidebarOpen.value = false
})

// Prefetch active streams so the sidebar Live badge is accurate
onMounted(() => {
  liveStore.loadActiveStreams().catch(() => {})
})
</script>

<template>
  <div class="min-h-screen bg-slate-950">
    <!-- Fixed top navigation -->
    <TopNav
      :sidebar-open="sidebarOpen"
      @toggle-sidebar="sidebarOpen = !sidebarOpen"
    />

    <!-- Left sidebar:
         - Regular Sidebar when not in company mode
         - CompanySidebar when acting as a company (starts below the banner at top-24) -->
    <Sidebar
      v-if="!companyMode.isActive"
      :open="sidebarOpen"
      @close="sidebarOpen = false"
    />
    <CompanySidebar
      v-else
      :open="sidebarOpen"
      @close="sidebarOpen = false"
    />

    <!-- Main content
         pt-16    → clears the fixed 64px top nav
         pt-24    → pt-16 + ~32px company mode banner when active
         lg:pl-64 → clears the 256px sidebar on large screens (always present now) -->
    <main
      class="min-h-screen transition-[padding-top,padding-left] duration-200"
      :class="[
        companyMode.isActive ? 'pt-24' : 'pt-16',
        'lg:pl-64',
      ]"
    >
      <!-- Standard layout: centered, max-w-3xl with padding -->
      <div v-if="!isWideContent" class="max-w-3xl mx-auto px-4 py-6">
        <slot />
      </div>
      <!-- Wide layout: view manages its own max-width and padding -->
      <slot v-else />
    </main>

    <!-- Messenger popup (fixed bottom-right, always present when authenticated) -->
    <MessengerPopup />
  </div>
</template>
