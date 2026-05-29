<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <div class="min-h-screen bg-slate-950 flex flex-col items-center justify-center px-4 text-center">
    <!-- Illustration area -->
    <div class="mb-6">
      <div class="inline-flex h-24 w-24 rounded-full bg-indigo-900/50 items-center justify-center">
        <svg class="h-12 w-12 text-indigo-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9.172 16.172a4 4 0 015.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
        </svg>
      </div>
    </div>
    <p class="text-7xl font-extrabold text-gray-100 leading-none select-none mb-4">404</p>

    <h1 class="text-2xl font-bold text-slate-100 mb-3">Page not found</h1>
    <p class="text-gray-500 mb-8 max-w-sm leading-relaxed">
      We couldn't find the page you're looking for. It may have moved, been removed, or never existed.
    </p>

    <div class="flex flex-col sm:flex-row gap-3">
      <RouterLink
        :to="auth.isAuthenticated ? '/wall' : '/'"
        class="inline-flex items-center justify-center gap-2 bg-indigo-600 hover:bg-indigo-700 text-white font-medium px-6 py-2.5 rounded-lg text-sm transition-colors"
      >
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18"/>
        </svg>
        {{ auth.isAuthenticated ? 'Back to My Wall' : 'Go home' }}
      </RouterLink>
    </div>
  </div>
</template>
