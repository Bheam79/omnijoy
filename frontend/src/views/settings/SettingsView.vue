<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'
import { RouterLink } from 'vue-router'

const auth = useAuthStore()
</script>

<template>
  <div>
    <h1 class="text-2xl font-bold text-gray-900 mb-6">Settings</h1>

    <div class="space-y-2">

      <!-- Profile -->
      <RouterLink
        v-if="auth.user"
        :to="`/profile/${auth.user.id}`"
        class="flex items-center justify-between bg-white rounded-xl border border-gray-100 px-5 py-4 hover:border-indigo-200 transition-colors group"
      >
        <div class="flex items-center gap-4">
          <div class="h-10 w-10 rounded-full bg-indigo-50 flex items-center justify-center text-indigo-600">
            <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"/>
            </svg>
          </div>
          <div>
            <p class="text-sm font-medium text-gray-900">My Profile</p>
            <p class="text-xs text-gray-500">Edit name, bio, avatar, cover photo</p>
          </div>
        </div>
        <svg class="h-4 w-4 text-gray-400 group-hover:text-indigo-500 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
        </svg>
      </RouterLink>

      <!-- Privacy -->
      <RouterLink
        to="/settings/privacy"
        class="flex items-center justify-between bg-white rounded-xl border border-gray-100 px-5 py-4 hover:border-indigo-200 transition-colors group"
      >
        <div class="flex items-center gap-4">
          <div class="h-10 w-10 rounded-full bg-emerald-50 flex items-center justify-center text-emerald-600">
            <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
            </svg>
          </div>
          <div>
            <p class="text-sm font-medium text-gray-900">Privacy</p>
            <p class="text-xs text-gray-500">Control who can see your posts, profile, and messages</p>
          </div>
        </div>
        <svg class="h-4 w-4 text-gray-400 group-hover:text-indigo-500 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
        </svg>
      </RouterLink>

      <!-- Notifications -->
      <RouterLink
        to="/settings/notifications"
        class="flex items-center justify-between bg-white rounded-xl border border-gray-100 px-5 py-4 hover:border-indigo-200 transition-colors group"
      >
        <div class="flex items-center gap-4">
          <div class="h-10 w-10 rounded-full bg-rose-50 flex items-center justify-center text-rose-600">
            <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"/>
            </svg>
          </div>
          <div>
            <p class="text-sm font-medium text-gray-900">Notifications</p>
            <p class="text-xs text-gray-500">Choose which events trigger notifications</p>
          </div>
        </div>
        <svg class="h-4 w-4 text-gray-400 group-hover:text-indigo-500 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
        </svg>
      </RouterLink>

      <!-- Account security -->
      <RouterLink
        to="/settings/account"
        class="flex items-center justify-between bg-white rounded-xl border border-gray-100 px-5 py-4 hover:border-indigo-200 transition-colors group"
      >
        <div class="flex items-center gap-4">
          <div class="h-10 w-10 rounded-full bg-amber-50 flex items-center justify-center text-amber-600">
            <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"/>
            </svg>
          </div>
          <div>
            <p class="text-sm font-medium text-gray-900">Account</p>
            <p class="text-xs text-gray-500">Change email, password, or deactivate / delete your account</p>
          </div>
        </div>
        <svg class="h-4 w-4 text-gray-400 group-hover:text-indigo-500 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
        </svg>
      </RouterLink>

    </div>
  </div>
</template>
