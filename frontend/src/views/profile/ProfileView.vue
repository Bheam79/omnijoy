<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import {
  userService,
  type UserProfile,
  type UpdateProfilePayload,
  type PrivacySettings,
  type UpdatePrivacyPayload,
} from '@/services/userService'

const route = useRoute()
const auth = useAuthStore()

// ── Profile data ──────────────────────────────────────────────────────────────

const profile = ref<UserProfile | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

async function loadProfile() {
  const userId = route.params.userId as string
  loading.value = true
  error.value = null
  try {
    profile.value = await userService.getProfile(userId)
  } catch (e: unknown) {
    const ax = e as { response?: { status?: number; data?: { error?: string } } }
    if (ax.response?.status === 403) error.value = 'This profile is private.'
    else if (ax.response?.status === 404) error.value = 'User not found.'
    else error.value = 'Failed to load profile.'
  } finally {
    loading.value = false
  }
}

onMounted(loadProfile)

// ── Upload error ──────────────────────────────────────────────────────────────

const uploadError = ref<string | null>(null)
const avatarUploading = ref(false)
const coverUploading = ref(false)

async function handleAvatarUpload(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  avatarUploading.value = true
  uploadError.value = null
  try {
    const updated = await userService.uploadAvatar(file)
    auth.setUser(updated)
    if (profile.value) profile.value.avatarUrl = updated.avatarUrl
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    uploadError.value = ax.response?.data?.error ?? 'Failed to upload avatar.'
  } finally {
    avatarUploading.value = false
    input.value = ''
  }
}

async function handleCoverUpload(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  coverUploading.value = true
  uploadError.value = null
  try {
    const updated = await userService.uploadCover(file)
    auth.setUser(updated)
    if (profile.value) profile.value.coverUrl = updated.coverUrl
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    uploadError.value = ax.response?.data?.error ?? 'Failed to upload cover photo.'
  } finally {
    coverUploading.value = false
    input.value = ''
  }
}

// ── Edit profile modal ────────────────────────────────────────────────────────

const editOpen = ref(false)
const editSaving = ref(false)
const editError = ref<string | null>(null)
const editForm = ref({
  displayName: '',
  bio: '',
  gender: 'NotDisclosed',
  birthDate: '',
  showBirthDate: false,
})

function openEdit() {
  if (!profile.value) return
  editForm.value = {
    displayName: profile.value.displayName,
    bio: profile.value.bio ?? '',
    gender: profile.value.gender,
    birthDate: profile.value.birthDate ?? '',
    showBirthDate: profile.value.showBirthDate,
  }
  editError.value = null
  editOpen.value = true
}

async function saveProfile() {
  editError.value = null
  editSaving.value = true
  try {
    const payload: UpdateProfilePayload = {
      displayName: editForm.value.displayName,
      bio: editForm.value.bio,
      gender: editForm.value.gender,
      birthDate: editForm.value.birthDate || '',
      showBirthDate: editForm.value.showBirthDate,
    }
    const updated = await userService.updateProfile(payload)
    auth.setUser(updated)
    if (profile.value) {
      profile.value.displayName = updated.displayName
      profile.value.bio = updated.bio
      profile.value.gender = updated.gender
      profile.value.birthDate = updated.birthDate
      profile.value.showBirthDate = updated.showBirthDate
    }
    editOpen.value = false
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    editError.value = ax.response?.data?.error ?? 'Failed to save changes.'
  } finally {
    editSaving.value = false
  }
}

// ── Privacy settings modal ────────────────────────────────────────────────────

const privacyOpen = ref(false)
const privacyLoading = ref(false)
const privacySaving = ref(false)
const privacyError = ref<string | null>(null)
const privacy = ref<PrivacySettings | null>(null)

async function openPrivacy() {
  privacyOpen.value = true
  if (privacy.value) return
  privacyLoading.value = true
  privacyError.value = null
  try {
    privacy.value = await userService.getPrivacySettings()
  } catch {
    privacyError.value = 'Failed to load privacy settings.'
  } finally {
    privacyLoading.value = false
  }
}

async function savePrivacy() {
  if (!privacy.value) return
  privacySaving.value = true
  privacyError.value = null
  try {
    const payload: UpdatePrivacyPayload = { ...privacy.value }
    privacy.value = await userService.updatePrivacySettings(payload)
    privacyOpen.value = false
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    privacyError.value = ax.response?.data?.error ?? 'Failed to save privacy settings.'
  } finally {
    privacySaving.value = false
  }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

const initials = computed(() => {
  const name = profile.value?.displayName ?? '?'
  return name.split(' ').map((w: string) => w[0]).slice(0, 2).join('').toUpperCase()
})

const genderLabel: Record<string, string> = {
  Male: 'Male',
  Female: 'Female',
  NotDisclosed: 'Not disclosed',
}

const privacyOptions = [
  { value: 'Everyone', label: 'Everyone' },
  { value: 'FriendsOfFriends', label: 'Friends of friends' },
  { value: 'Friends', label: 'Friends only' },
  { value: 'OnlyMe', label: 'Only me' },
]

const privacyFields: Array<{ key: keyof PrivacySettings; label: string }> = [
  { key: 'whoCanSeePosts', label: 'Who can see my posts' },
  { key: 'whoCanSeeProfile', label: 'Who can see my profile' },
  { key: 'whoCanSendMessages', label: 'Who can message me' },
  { key: 'whoCanSeeFriendList', label: 'Who can see my friends' },
  { key: 'whoCanSeeEvents', label: 'Who can see my events' },
  { key: 'whoCanTagInPosts', label: 'Who can tag me in posts' },
]
</script>

<template>
  <div class="pb-16">

    <!-- Loading -->
    <div v-if="loading" class="flex justify-center py-24">
      <div class="h-8 w-8 rounded-full border-2 border-indigo-500 border-t-transparent animate-spin" />
    </div>

    <!-- Error -->
    <div v-else-if="error" class="text-center py-24">
      <div class="inline-flex h-16 w-16 rounded-full bg-gray-100 items-center justify-center mb-4">
        <svg class="h-8 w-8 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M12 9v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
        </svg>
      </div>
      <p class="text-gray-600 font-medium">{{ error }}</p>
    </div>

    <!-- Profile content -->
    <template v-else-if="profile">

      <!-- Cover photo -->
      <div class="relative -mx-4 -mt-6 h-48 sm:h-56 bg-gradient-to-br from-indigo-400 to-violet-500 overflow-hidden group">
        <img v-if="profile.coverUrl" :src="profile.coverUrl" alt="Cover" class="w-full h-full object-cover" />

        <!-- Cover upload overlay -->
        <label
          v-if="profile.isOwnProfile"
          class="absolute inset-0 flex items-center justify-center bg-black/30 opacity-0 group-hover:opacity-100 transition-opacity cursor-pointer"
        >
          <div class="flex items-center gap-2 bg-white/90 rounded-lg px-4 py-2 text-sm font-medium text-gray-700">
            <span v-if="coverUploading" class="h-4 w-4 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin inline-block" />
            <svg v-else class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/>
            </svg>
            <span>{{ coverUploading ? 'Uploading…' : 'Change cover photo' }}</span>
          </div>
          <input type="file" accept="image/*" class="sr-only" :disabled="coverUploading" @change="handleCoverUpload" />
        </label>
      </div>

      <!-- Header row: avatar + action buttons -->
      <div class="relative px-4">

        <!-- Avatar -->
        <div class="absolute -top-12 left-4">
          <div class="relative group">
            <div class="h-24 w-24 rounded-full ring-4 ring-white bg-indigo-100 overflow-hidden">
              <img v-if="profile.avatarUrl" :src="profile.avatarUrl" :alt="profile.displayName" class="h-full w-full object-cover" />
              <div v-else class="h-full w-full flex items-center justify-center text-2xl font-bold text-indigo-600">
                {{ initials }}
              </div>
            </div>
            <!-- Avatar upload overlay -->
            <label v-if="profile.isOwnProfile" class="absolute inset-0 rounded-full flex items-center justify-center bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity cursor-pointer">
              <span v-if="avatarUploading" class="h-5 w-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
              <svg v-else class="h-6 w-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z"/>
              </svg>
              <input type="file" accept="image/*" class="sr-only" :disabled="avatarUploading" @change="handleAvatarUpload" />
            </label>
          </div>
        </div>

        <!-- Buttons -->
        <div class="flex justify-end pt-3 gap-2">
          <template v-if="profile.isOwnProfile">
            <button
              class="flex items-center gap-1.5 text-sm font-medium bg-gray-100 hover:bg-gray-200 text-gray-700 px-3 py-2 rounded-lg transition-colors"
              @click="openPrivacy"
            >
              <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
              </svg>
              Privacy
            </button>
            <button
              class="flex items-center gap-1.5 text-sm font-medium bg-indigo-600 hover:bg-indigo-700 text-white px-3 py-2 rounded-lg transition-colors"
              @click="openEdit"
            >
              <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"/>
              </svg>
              Edit profile
            </button>
          </template>
          <template v-else>
            <button class="flex items-center gap-1.5 text-sm font-medium bg-indigo-600 hover:bg-indigo-700 text-white px-3 py-2 rounded-lg transition-colors">
              <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z"/>
              </svg>
              {{ profile.isFriend ? 'Friends' : 'Add friend' }}
            </button>
            <button class="flex items-center gap-1.5 text-sm font-medium bg-gray-100 hover:bg-gray-200 text-gray-700 px-3 py-2 rounded-lg transition-colors">
              <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 12h.01M12 12h.01M16 12h.01M21 8a9 9 0 11-18 0 9 9 0 0118 0z"/>
              </svg>
              Message
            </button>
          </template>
        </div>

        <!-- Upload error -->
        <p v-if="uploadError" class="mt-2 text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
          {{ uploadError }}
        </p>

        <!-- Name + info -->
        <div class="mt-14 pb-4 border-b border-gray-200">
          <h1 class="text-2xl font-bold text-gray-900">{{ profile.displayName }}</h1>
          <p v-if="profile.bio" class="mt-1 text-sm text-gray-600 leading-relaxed">{{ profile.bio }}</p>

          <div class="mt-3 flex flex-wrap gap-x-5 gap-y-2 text-sm text-gray-500">
            <span class="flex items-center gap-1.5">
              <svg class="h-4 w-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z"/>
              </svg>
              <strong class="font-medium text-gray-700">{{ profile.friendCount }}</strong> friends
              <template v-if="profile.mutualFriendCount !== undefined && profile.mutualFriendCount > 0">
                · <strong class="font-medium text-gray-700">{{ profile.mutualFriendCount }}</strong> mutual
              </template>
            </span>

            <span v-if="profile.gender !== 'NotDisclosed'" class="flex items-center gap-1.5">
              <svg class="h-4 w-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"/>
              </svg>
              {{ genderLabel[profile.gender] ?? profile.gender }}
            </span>

            <span v-if="profile.birthDate" class="flex items-center gap-1.5">
              <svg class="h-4 w-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
              </svg>
              {{ profile.birthDate }}
            </span>

            <span class="flex items-center gap-1.5">
              <svg class="h-4 w-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
              </svg>
              Joined {{ new Date(profile.createdAt).getFullYear() }}
            </span>
          </div>
        </div>
      </div>

      <!-- Posts timeline stub -->
      <div class="mt-6 px-4">
        <h2 class="text-base font-semibold text-gray-700 mb-4">Posts</h2>
        <div class="text-center py-16 text-gray-400">
          <svg class="h-12 w-12 mx-auto mb-3 text-gray-200" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M19 20H5a2 2 0 01-2-2V6a2 2 0 012-2h10a2 2 0 012 2v1m2 13a2 2 0 01-2-2V7m2 13a2 2 0 002-2V9a2 2 0 00-2-2h-2m-4-3H9M7 16h6M7 8h6v4H7V8z"/>
          </svg>
          <p class="text-sm">No posts yet.</p>
        </div>
      </div>

    </template>
  </div>

  <!-- ── Edit Profile Modal ──────────────────────────────────────────────────── -->
  <Teleport to="body">
    <Transition enter-active-class="transition ease-out duration-200" enter-from-class="opacity-0" enter-to-class="opacity-100" leave-active-class="transition ease-in duration-150" leave-from-class="opacity-100" leave-to-class="opacity-0">
      <div v-if="editOpen" class="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/50" @click.self="editOpen = false">
        <div class="bg-white rounded-2xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-y-auto">
          <div class="flex items-center justify-between px-6 py-4 border-b border-gray-100">
            <h2 class="text-lg font-semibold text-gray-900">Edit profile</h2>
            <button class="p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg" @click="editOpen = false">
              <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>
            </button>
          </div>

          <form class="px-6 py-5 space-y-4" @submit.prevent="saveProfile">
            <div v-if="editError" class="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">{{ editError }}</div>

            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Display name <span class="text-red-500">*</span></label>
              <input v-model="editForm.displayName" type="text" maxlength="128" placeholder="Your name" class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent" />
            </div>

            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Bio</label>
              <textarea v-model="editForm.bio" maxlength="2000" rows="3" placeholder="Tell people a bit about yourself…" class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent resize-none" />
              <p class="mt-1 text-xs text-gray-400 text-right">{{ (editForm.bio ?? '').length }}/2000</p>
            </div>

            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Gender</label>
              <select v-model="editForm.gender" class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent">
                <option value="NotDisclosed">Prefer not to say</option>
                <option value="Male">Male</option>
                <option value="Female">Female</option>
              </select>
            </div>

            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Date of birth</label>
              <input v-model="editForm.birthDate" type="date" class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent" />
            </div>

            <div v-if="editForm.birthDate" class="flex items-center gap-3">
              <button
                type="button"
                class="relative inline-flex h-5 w-9 items-center rounded-full transition-colors focus:outline-none"
                :class="editForm.showBirthDate ? 'bg-indigo-600' : 'bg-gray-200'"
                @click="editForm.showBirthDate = !editForm.showBirthDate"
              >
                <span class="inline-block h-3.5 w-3.5 rounded-full bg-white shadow transition-transform" :class="editForm.showBirthDate ? 'translate-x-4' : 'translate-x-1'" />
              </button>
              <span class="text-sm text-gray-600">Show my birthday on my profile</span>
            </div>

            <div class="flex justify-end gap-3 pt-2">
              <button type="button" class="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg" @click="editOpen = false">Cancel</button>
              <button type="submit" :disabled="editSaving" class="px-4 py-2 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 rounded-lg">
                <span v-if="editSaving">Saving…</span><span v-else>Save changes</span>
              </button>
            </div>
          </form>
        </div>
      </div>
    </Transition>
  </Teleport>

  <!-- ── Privacy Settings Modal ─────────────────────────────────────────────── -->
  <Teleport to="body">
    <Transition enter-active-class="transition ease-out duration-200" enter-from-class="opacity-0" enter-to-class="opacity-100" leave-active-class="transition ease-in duration-150" leave-from-class="opacity-100" leave-to-class="opacity-0">
      <div v-if="privacyOpen" class="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/50" @click.self="privacyOpen = false">
        <div class="bg-white rounded-2xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-y-auto">
          <div class="flex items-center justify-between px-6 py-4 border-b border-gray-100">
            <h2 class="text-lg font-semibold text-gray-900">Privacy settings</h2>
            <button class="p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg" @click="privacyOpen = false">
              <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>
            </button>
          </div>

          <div class="px-6 py-5">
            <div v-if="privacyLoading" class="flex justify-center py-8">
              <div class="h-6 w-6 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin" />
            </div>

            <template v-else-if="privacy">
              <div v-if="privacyError" class="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">{{ privacyError }}</div>

              <form class="space-y-4" @submit.prevent="savePrivacy">
                <div v-for="field in privacyFields" :key="field.key">
                  <label class="block text-sm font-medium text-gray-700 mb-1">{{ field.label }}</label>
                  <select v-model="privacy[field.key]" class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent">
                    <option v-for="opt in privacyOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
                  </select>
                </div>

                <div class="flex justify-end gap-3 pt-2">
                  <button type="button" class="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg" @click="privacyOpen = false">Cancel</button>
                  <button type="submit" :disabled="privacySaving" class="px-4 py-2 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 rounded-lg">
                    <span v-if="privacySaving">Saving…</span><span v-else>Save</span>
                  </button>
                </div>
              </form>
            </template>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
