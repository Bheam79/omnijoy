<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, RouterView } from 'vue-router'
import AppShell from '@/components/layout/AppShell.vue'

const route = useRoute()

/** Use the authenticated app shell for any route tagged layout: 'app' */
const useAppLayout = computed(() => route.meta.layout === 'app')
</script>

<template>
  <AppShell v-if="useAppLayout">
    <RouterView />
  </AppShell>
  <RouterView v-else />
</template>
