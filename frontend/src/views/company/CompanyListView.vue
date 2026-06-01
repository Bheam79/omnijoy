<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import { companyPageService, type CompanyPageDto } from '@/services/companyPageService'
import PlacePicker from '@/components/shared/PlacePicker.vue'
import type { PlaceSelection } from '@/types/places'

const pages = ref<CompanyPageDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const hasMore = ref(false)
const page = ref(1)
const loadingMore = ref(false)
const showCreateModal = ref(false)
const activeTab = ref<'all' | 'mine'>('all')

// ── Create form state ─────────────────────────────────────────────────────────
const createName = ref('')
const createDesc = ref('')
const createLogo = ref<File | null>(null)
const createCover = ref<File | null>(null)
const logoPreview = ref<string | null>(null)
const coverPreview = ref<string | null>(null)
const createAddress = ref<PlaceSelection | null>(null)
const creating = ref(false)
const createError = ref<string | null>(null)

async function loadPages(tab: 'all' | 'mine' = activeTab.value, pageNum = 1) {
  if (pageNum === 1) {
    loading.value = true
    pages.value = []
  } else {
    loadingMore.value = true
  }
  error.value = null
  try {
    const result = await companyPageService.getPages({ mine: tab === 'mine', page: pageNum })
    if (pageNum === 1) {
      pages.value = result.items
    } else {
      pages.value.push(...result.items)
    }
    hasMore.value = result.hasMore
    page.value = pageNum
  } catch (e: unknown) {
    error.value = extractError(e)
  } finally {
    loading.value = false
    loadingMore.value = false
  }
}

function setTab(tab: 'all' | 'mine') {
  activeTab.value = tab
  loadPages(tab, 1)
}

onMounted(() => loadPages())

function onLogoChange(e: Event) {
  const f = (e.target as HTMLInputElement).files?.[0]
  if (!f) return
  createLogo.value = f
  logoPreview.value = URL.createObjectURL(f)
}

function onCoverChange(e: Event) {
  const f = (e.target as HTMLInputElement).files?.[0]
  if (!f) return
  createCover.value = f
  coverPreview.value = URL.createObjectURL(f)
}

async function handleCreate() {
  createError.value = null
  if (!createName.value.trim()) {
    createError.value = 'Name is required.'
    return
  }
  creating.value = true
  try {
    const p = await companyPageService.createPage({
      name:             createName.value.trim(),
      description:      createDesc.value.trim() || undefined,
      logo:             createLogo.value  ?? undefined,
      cover:            createCover.value ?? undefined,
      addressPlaceId:   createAddress.value && createAddress.value.placeId !== 'manual' && createAddress.value.placeId !== 'stored'
                          ? createAddress.value.placeId : null,
      addressText:      createAddress.value?.formattedAddress ?? createAddress.value?.displayName ?? null,
      addressCity:      createAddress.value?.city ?? null,
      addressCountry:   createAddress.value?.country ?? null,
      addressLatitude:  createAddress.value?.latitude ?? null,
      addressLongitude: createAddress.value?.longitude ?? null,
    })
    pages.value.unshift(p)
    showCreateModal.value = false
    resetCreateForm()
  } catch (e: unknown) {
    createError.value = extractError(e)
  } finally {
    creating.value = false
  }
}

function resetCreateForm() {
  createName.value    = ''
  createDesc.value    = ''
  createLogo.value    = null
  createCover.value   = null
  createAddress.value = null
  if (logoPreview.value)  URL.revokeObjectURL(logoPreview.value)
  if (coverPreview.value) URL.revokeObjectURL(coverPreview.value)
  logoPreview.value  = null
  coverPreview.value = null
  createError.value  = null
}

function removeCoverPreview() {
  if (coverPreview.value) URL.revokeObjectURL(coverPreview.value)
  coverPreview.value = null
  createCover.value  = null
}

async function toggleFollow(p: CompanyPageDto) {
  try {
    const updated = p.isFollowing
      ? await companyPageService.unfollow(p.id)
      : await companyPageService.follow(p.id)
    const idx = pages.value.findIndex(x => x.id === p.id)
    if (idx !== -1) pages.value[idx] = updated
  } catch { /* swallow */ }
}

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const ae = e as { response?: { data?: { error?: string } }; message?: string }
    return ae.response?.data?.error ?? ae.message ?? 'An error occurred.'
  }
  return 'An error occurred.'
}
</script>

<template>
  <div class="max-w-3xl mx-auto px-4 py-6">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-2xl font-bold text-slate-100">Company Pages</h1>
        <p class="text-sm text-gray-500 mt-0.5">Discover and follow organization pages</p>
      </div>
      <button
        data-testid="create-company-button"
        class="flex items-center gap-2 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-medium rounded-xl transition shadow-sm"
        @click="showCreateModal = true"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
        </svg>
        Create Page
      </button>
    </div>

    <!-- Tabs -->
    <div class="flex gap-1 mb-6 bg-slate-700 rounded-xl p-1 w-fit">
      <button
        class="px-4 py-1.5 rounded-lg text-sm font-medium transition-all cursor-pointer"
        :class="activeTab === 'all' ? 'bg-slate-800 text-indigo-300 shadow-sm' : 'text-slate-400 hover:text-slate-200'"
        @click="setTab('all')"
      >All Pages</button>
      <button
        class="px-4 py-1.5 rounded-lg text-sm font-medium transition-all cursor-pointer"
        :class="activeTab === 'mine' ? 'bg-slate-800 text-indigo-300 shadow-sm' : 'text-slate-400 hover:text-slate-200'"
        @click="setTab('mine')"
      >My Pages</button>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <div v-for="i in 4" :key="i" class="bg-slate-800 rounded-xl border border-slate-700 overflow-hidden animate-pulse">
        <div class="h-24 bg-slate-700"/>
        <div class="p-4 flex gap-3">
          <div class="w-14 h-14 rounded-xl bg-slate-700 -mt-8 shrink-0"/>
          <div class="space-y-2 pt-2 flex-1">
            <div class="h-4 bg-slate-700 rounded w-2/3"/>
            <div class="h-3 bg-slate-700 rounded w-1/2"/>
          </div>
        </div>
      </div>
    </div>

    <!-- Error -->
    <div v-else-if="error" class="bg-red-950 border border-red-800 rounded-xl p-4 text-red-400 text-sm">
      {{ error }}
    </div>

    <!-- Empty -->
    <div v-else-if="pages.length === 0" class="text-center py-16">
      <div class="text-5xl mb-3">🏢</div>
      <h3 class="text-lg font-semibold text-slate-300 mb-1">
        {{ activeTab === 'mine' ? 'No pages yet' : 'No company pages' }}
      </h3>
      <p class="text-sm text-gray-500 mb-4">Create the first company page on Omnijoy!</p>
      <button
        class="px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded-xl hover:bg-indigo-700 transition"
        @click="showCreateModal = true"
      >
        Create a Page
      </button>
    </div>

    <!-- Grid -->
    <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <div
        v-for="p in pages"
        :key="p.id"
        data-testid="company-card"
        class="bg-slate-800 rounded-xl border border-slate-700 overflow-hidden hover:shadow-md transition-shadow"
      >
        <!-- Cover -->
        <RouterLink :to="`/company/${p.id}`">
          <div class="h-24 bg-gradient-to-br from-indigo-400 to-purple-500 overflow-hidden">
            <img v-if="p.coverUrl" :src="p.coverUrl" :alt="p.name" class="w-full h-full object-cover"/>
          </div>
        </RouterLink>

        <div class="px-4 pb-4">
          <!-- Logo + name -->
          <div class="flex items-end gap-3 -mt-7 mb-2">
            <RouterLink :to="`/company/${p.id}`">
              <div class="w-14 h-14 rounded-xl border-2 border-slate-700 bg-slate-800 shadow overflow-hidden shrink-0">
                <img v-if="p.logoUrl" :src="p.logoUrl" :alt="p.name" class="w-full h-full object-cover"/>
                <div
                  v-else
                  class="w-full h-full bg-indigo-600 flex items-center justify-center text-white font-bold text-xl"
                >
                  {{ p.name.charAt(0).toUpperCase() }}
                </div>
              </div>
            </RouterLink>
            <div class="pb-1 min-w-0">
              <RouterLink :to="`/company/${p.id}`" class="font-semibold text-slate-100 hover:text-indigo-300 transition text-sm block truncate">
                {{ p.name }}
              </RouterLink>
              <p class="text-xs text-gray-500">{{ p.followerCount }} follower{{ p.followerCount !== 1 ? 's' : '' }}</p>
            </div>
          </div>

          <!-- Description -->
          <p v-if="p.description" class="text-xs text-slate-400 line-clamp-2 mb-3">{{ p.description }}</p>

          <!-- Role badge + follow button -->
          <div class="flex items-center justify-between">
            <span v-if="p.myRole" class="text-xs font-medium px-2 py-0.5 rounded-full"
              :class="{
                'bg-yellow-900/50 text-yellow-300': p.myRole === 'Owner',
                'bg-blue-900 text-blue-300':   p.myRole === 'Admin',
                'bg-slate-700 text-slate-300':   p.myRole === 'Editor',
              }"
            >{{ p.myRole }}</span>
            <span v-else></span>

            <button
              class="text-xs font-medium px-3 py-1 rounded-full transition border"
              :class="p.isFollowing
                ? 'border-slate-600 text-slate-400 hover:border-red-300 hover:text-red-400'
                : 'border-indigo-300 text-indigo-400 hover:bg-indigo-900/50'"
              @click="toggleFollow(p)"
            >
              {{ p.isFollowing ? 'Unfollow' : 'Follow' }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Load more -->
    <div v-if="hasMore && !loading" class="mt-6 text-center">
      <button
        class="px-6 py-2 text-sm font-medium text-indigo-400 border border-indigo-700 rounded-xl hover:bg-indigo-900/50 transition"
        :disabled="loadingMore"
        @click="loadPages(activeTab, page + 1)"
      >
        <span v-if="loadingMore">Loading…</span>
        <span v-else>Load more</span>
      </button>
    </div>
  </div>

  <!-- Create modal -->
  <Teleport to="body">
    <div
      v-if="showCreateModal"
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm"
      @click.self="showCreateModal = false; resetCreateForm()"
    >
      <div class="bg-slate-800 rounded-2xl shadow-2xl w-full max-w-md max-h-[90vh] flex flex-col">
        <!-- Header -->
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-700">
          <h2 class="text-lg font-bold text-slate-100">Create Company Page</h2>
          <button class="text-slate-500 hover:text-slate-400 p-1 rounded-full hover:bg-slate-700 transition"
            @click="showCreateModal = false; resetCreateForm()"
          >
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <!-- Body -->
        <div class="overflow-y-auto flex-1 px-6 py-4 space-y-4">
          <!-- Cover -->
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-1">Cover Image</label>
            <div v-if="coverPreview" class="relative h-28 rounded-xl overflow-hidden">
              <img :src="coverPreview" class="w-full h-full object-cover"/>
              <button type="button" class="absolute top-2 right-2 bg-black/50 text-white rounded-full p-1"
                @click="removeCoverPreview"
              >
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                </svg>
              </button>
            </div>
            <label v-else class="flex flex-col items-center justify-center h-20 border-2 border-dashed border-slate-600 rounded-xl cursor-pointer hover:border-indigo-400 transition">
              <span class="text-xs text-gray-500">Click to upload cover</span>
              <input type="file" class="hidden" accept="image/*" @change="onCoverChange"/>
            </label>
          </div>

          <!-- Logo -->
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-1">Logo</label>
            <div class="flex items-center gap-3">
              <div class="w-16 h-16 rounded-xl border-2 border-dashed border-slate-600 overflow-hidden flex items-center justify-center bg-slate-950">
                <img v-if="logoPreview" :src="logoPreview" class="w-full h-full object-cover"/>
                <svg v-else class="w-6 h-6 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/>
                </svg>
              </div>
              <label class="text-xs text-indigo-400 cursor-pointer hover:underline">
                Upload logo
                <input type="file" class="hidden" accept="image/*" @change="onLogoChange"/>
              </label>
            </div>
          </div>

          <!-- Name -->
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-1">Name <span class="text-red-500">*</span></label>
            <input v-model="createName" type="text" maxlength="256" placeholder="Company or organization name"
              class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            />
          </div>

          <!-- Description -->
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-1">Description</label>
            <textarea v-model="createDesc" rows="3" placeholder="What does this page represent?"
              class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent resize-none"
            />
          </div>

          <!-- Business address (optional) -->
          <PlacePicker
            v-model="createAddress"
            mode="address"
            label="Business address"
            placeholder="Search for address…"
          />

          <div v-if="createError" class="text-sm text-red-400 bg-red-950 rounded-lg px-3 py-2">
            {{ createError }}
          </div>
        </div>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-slate-700 flex justify-end gap-3">
          <button
            type="button"
            class="px-4 py-2 text-sm font-medium text-slate-300 bg-slate-800 border border-slate-600 rounded-lg hover:bg-slate-700 transition"
            @click="showCreateModal = false; resetCreateForm()"
          >Cancel</button>
          <button
            type="button"
            :disabled="creating"
            class="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 transition disabled:opacity-50"
            @click="handleCreate"
          >
            <span v-if="creating">Creating…</span>
            <span v-else>Create Page</span>
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
