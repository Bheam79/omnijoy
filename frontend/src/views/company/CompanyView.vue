<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import { companyPageService, type CompanyPageDto, type AdminsResult } from '@/services/companyPageService'
import { postService, type PostDto } from '@/services/postService'
import { useAuthStore } from '@/stores/auth'
import PostCard from '@/components/post/PostCard.vue'

const route = useRoute()
const auth  = useAuthStore()

const page     = ref<CompanyPageDto | null>(null)
const posts    = ref<PostDto[]>([])
const admins   = ref<AdminsResult | null>(null)
const loading  = ref(true)
const error    = ref<string | null>(null)
const activeTab = ref<'posts' | 'about' | 'admins'>('posts')

// ── Admin panel state ─────────────────────────────────────────────────────────
const showAddAdmin  = ref(false)
const addAdminId    = ref('')
const addAdminRole  = ref<'Admin' | 'Editor'>('Editor')
const addAdminError = ref<string | null>(null)
const addingAdmin   = ref(false)

// ── Edit state ────────────────────────────────────────────────────────────────
const showEditModal  = ref(false)
const editName       = ref('')
const editDesc       = ref('')
const editLogoFile   = ref<File | null>(null)
const editCoverFile  = ref<File | null>(null)
const editLogoPreview  = ref<string | null>(null)
const editCoverPreview = ref<string | null>(null)
const editError      = ref<string | null>(null)
const editSaving     = ref(false)

const isAdmin = computed(() =>
  page.value?.myRole === 'Owner' || page.value?.myRole === 'Admin'
)
const isOwner = computed(() => page.value?.myRole === 'Owner')

async function fetchData() {
  loading.value = true
  error.value   = null
  try {
    const id = route.params.id as string
    page.value   = await companyPageService.getPage(id)
    admins.value = await companyPageService.getAdmins(id)

    // Load page's posts (reuse postService.getFeed with a filter would be ideal;
    // for now fetch all posts and client-filter — a dedicated endpoint is out of scope)
    const feedResult = await postService.getFeed(1, 50)
    posts.value = feedResult.items.filter(p => p.companyPageId === id)
  } catch (e: unknown) {
    error.value = extractError(e)
  } finally {
    loading.value = false
  }
}

async function toggleFollow() {
  if (!page.value) return
  try {
    page.value = page.value.isFollowing
      ? await companyPageService.unfollow(page.value.id)
      : await companyPageService.follow(page.value.id)
  } catch { /* swallow */ }
}

function openEdit() {
  if (!page.value) return
  editName.value = page.value.name
  editDesc.value = page.value.description ?? ''
  editLogoFile.value  = null
  editCoverFile.value = null
  editLogoPreview.value  = null
  editCoverPreview.value = null
  editError.value = null
  showEditModal.value = true
}

function onEditLogoChange(e: Event) {
  const f = (e.target as HTMLInputElement).files?.[0]
  if (!f) return
  editLogoFile.value = f
  editLogoPreview.value = URL.createObjectURL(f)
}

function onEditCoverChange(e: Event) {
  const f = (e.target as HTMLInputElement).files?.[0]
  if (!f) return
  editCoverFile.value = f
  editCoverPreview.value = URL.createObjectURL(f)
}

async function saveEdit() {
  if (!page.value) return
  editError.value = null
  editSaving.value = true
  try {
    page.value = await companyPageService.updatePage(page.value.id, {
      name:        editName.value.trim() || undefined,
      description: editDesc.value.trim() || undefined,
      logo:        editLogoFile.value  ?? undefined,
      cover:       editCoverFile.value ?? undefined,
    })
    showEditModal.value = false
  } catch (e: unknown) {
    editError.value = extractError(e)
  } finally {
    editSaving.value = false
  }
}

async function removeAdmin(userId: string) {
  if (!page.value || !confirm('Remove this admin?')) return
  try {
    admins.value = await companyPageService.removeAdmin(page.value.id, userId)
  } catch (e: unknown) {
    alert(extractError(e))
  }
}

async function handleAddAdmin() {
  if (!page.value) return
  addAdminError.value = null
  if (!addAdminId.value.trim()) {
    addAdminError.value = 'User ID is required.'
    return
  }
  addingAdmin.value = true
  try {
    admins.value = await companyPageService.addAdmin(page.value.id, {
      userId: addAdminId.value.trim(),
      role:   addAdminRole.value,
    })
    addAdminId.value = ''
    showAddAdmin.value = false
  } catch (e: unknown) {
    addAdminError.value = extractError(e)
  } finally {
    addingAdmin.value = false
  }
}

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const ae = e as { response?: { data?: { error?: string } }; message?: string }
    return ae.response?.data?.error ?? ae.message ?? 'An error occurred.'
  }
  return 'An error occurred.'
}

onMounted(fetchData)
</script>

<template>
  <div>
    <!-- Loading -->
    <div v-if="loading" class="animate-pulse">
      <div class="h-48 bg-gray-200"/>
      <div class="max-w-2xl mx-auto px-4 -mt-16 pb-6">
        <div class="flex items-end gap-4 mb-4">
          <div class="w-24 h-24 rounded-2xl bg-gray-300 border-4 border-white"/>
          <div class="pb-2 space-y-2">
            <div class="h-5 w-36 bg-gray-300 rounded"/>
            <div class="h-3 w-24 bg-gray-200 rounded"/>
          </div>
        </div>
      </div>
    </div>

    <!-- Error -->
    <div v-else-if="error" class="max-w-2xl mx-auto px-4 py-16 text-center">
      <p class="text-red-600">{{ error }}</p>
      <RouterLink to="/company" class="text-indigo-600 hover:underline text-sm mt-2 inline-block">← Back to Pages</RouterLink>
    </div>

    <!-- Page content -->
    <template v-else-if="page">
      <!-- Cover -->
      <div class="relative h-48 bg-gradient-to-br from-indigo-500 to-purple-600 overflow-hidden">
        <img v-if="page.coverUrl" :src="page.coverUrl" :alt="page.name" class="w-full h-full object-cover"/>
      </div>

      <div class="max-w-2xl mx-auto px-4">
        <!-- Logo + info header -->
        <div class="flex items-end justify-between -mt-12 mb-4">
          <div class="flex items-end gap-4">
            <!-- Logo -->
            <div class="w-24 h-24 rounded-2xl border-4 border-white bg-white shadow-lg overflow-hidden shrink-0">
              <img v-if="page.logoUrl" :src="page.logoUrl" :alt="page.name" class="w-full h-full object-cover"/>
              <div v-else class="w-full h-full bg-indigo-600 flex items-center justify-center text-white font-bold text-3xl">
                {{ page.name.charAt(0).toUpperCase() }}
              </div>
            </div>

            <div class="pb-1">
              <h1 class="text-xl font-bold text-gray-900">{{ page.name }}</h1>
              <p class="text-sm text-gray-500">
                {{ page.followerCount }} follower{{ page.followerCount !== 1 ? 's' : '' }}
                <span v-if="page.myRole" class="ml-2 px-2 py-0.5 rounded-full text-xs font-medium"
                  :class="{
                    'bg-yellow-100 text-yellow-800': page.myRole === 'Owner',
                    'bg-blue-100 text-blue-800':    page.myRole === 'Admin',
                    'bg-gray-100 text-gray-700':    page.myRole === 'Editor',
                  }"
                >{{ page.myRole }}</span>
              </p>
            </div>
          </div>

          <!-- Actions -->
          <div class="flex gap-2 pb-1">
            <button v-if="isAdmin" @click="openEdit"
              class="px-3 py-1.5 text-xs font-medium border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 transition"
            >Edit Page</button>
            <button
              class="px-4 py-1.5 text-sm font-medium rounded-lg transition border"
              :class="page.isFollowing
                ? 'border-gray-300 text-gray-700 hover:border-red-300 hover:text-red-600'
                : 'bg-indigo-600 border-indigo-600 text-white hover:bg-indigo-700'"
              @click="toggleFollow"
            >{{ page.isFollowing ? 'Unfollow' : 'Follow' }}</button>
          </div>
        </div>

        <!-- Tabs -->
        <div class="flex gap-1 border-b border-gray-200 mb-6">
          <button
            v-for="tab in ['posts', 'about', ...(isAdmin ? ['admins'] : [])]"
            :key="tab"
            class="px-4 py-2 text-sm font-medium capitalize border-b-2 transition-colors"
            :class="activeTab === tab
              ? 'border-indigo-600 text-indigo-700'
              : 'border-transparent text-gray-500 hover:text-gray-700'"
            @click="activeTab = tab as 'posts' | 'about' | 'admins'"
          >{{ tab }}</button>
        </div>

        <!-- Posts tab -->
        <div v-if="activeTab === 'posts'" class="space-y-4 pb-8">
          <div v-if="posts.length === 0" class="text-center py-10 text-gray-500 text-sm">
            No posts yet.
          </div>
          <PostCard v-for="post in posts" :key="post.id" :post="post"/>
        </div>

        <!-- About tab -->
        <div v-else-if="activeTab === 'about'" class="pb-8">
          <div class="bg-white border border-gray-100 rounded-xl p-5 shadow-sm">
            <h2 class="font-semibold text-gray-900 mb-3">About</h2>
            <p v-if="page.description" class="text-gray-700 whitespace-pre-wrap leading-relaxed text-sm">
              {{ page.description }}
            </p>
            <p v-else class="text-gray-500 text-sm italic">No description.</p>
            <div class="mt-4 pt-4 border-t border-gray-100 text-xs text-gray-500">
              Created by
              <RouterLink :to="`/profile/${page.createdBy.id}`" class="font-medium text-gray-700 hover:underline">
                {{ page.createdBy.displayName }}
              </RouterLink>
              · {{ new Date(page.createdAt).toLocaleDateString() }}
            </div>
          </div>
        </div>

        <!-- Admins tab (Owner/Admin only) -->
        <div v-else-if="activeTab === 'admins' && isAdmin" class="pb-8">
          <div class="bg-white border border-gray-100 rounded-xl p-5 shadow-sm">
            <div class="flex items-center justify-between mb-4">
              <h2 class="font-semibold text-gray-900">Page Admins</h2>
              <button v-if="isOwner || page.myRole === 'Admin'"
                class="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
                @click="showAddAdmin = !showAddAdmin"
              >+ Add admin</button>
            </div>

            <!-- Add admin form -->
            <div v-if="showAddAdmin" class="mb-4 p-3 bg-gray-50 rounded-lg space-y-2">
              <div class="flex gap-2">
                <input v-model="addAdminId" type="text" placeholder="User ID"
                  class="flex-1 rounded-lg border border-gray-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
                <select v-model="addAdminRole"
                  class="rounded-lg border border-gray-300 px-2 py-1.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                >
                  <option v-if="isOwner" value="Admin">Admin</option>
                  <option value="Editor">Editor</option>
                </select>
                <button :disabled="addingAdmin"
                  class="px-3 py-1.5 bg-indigo-600 text-white text-sm rounded-lg hover:bg-indigo-700 transition disabled:opacity-50"
                  @click="handleAddAdmin"
                >Add</button>
              </div>
              <p v-if="addAdminError" class="text-xs text-red-600">{{ addAdminError }}</p>
            </div>

            <!-- Admin list -->
            <div class="space-y-2">
              <div
                v-for="admin in admins?.admins ?? []"
                :key="admin.userId"
                class="flex items-center justify-between py-2 border-b border-gray-50 last:border-0"
              >
                <div class="flex items-center gap-2.5">
                  <RouterLink :to="`/profile/${admin.userId}`">
                    <img v-if="admin.avatarUrl" :src="admin.avatarUrl" :alt="admin.displayName" class="w-8 h-8 rounded-full object-cover"/>
                    <div v-else class="w-8 h-8 rounded-full bg-indigo-500 flex items-center justify-center text-white text-xs font-semibold">
                      {{ admin.displayName.charAt(0).toUpperCase() }}
                    </div>
                  </RouterLink>
                  <div>
                    <RouterLink :to="`/profile/${admin.userId}`" class="text-sm font-medium text-gray-800 hover:underline">
                      {{ admin.displayName }}
                    </RouterLink>
                    <span class="ml-2 text-xs px-1.5 py-0.5 rounded font-medium"
                      :class="{
                        'bg-yellow-100 text-yellow-800': admin.role === 'Owner',
                        'bg-blue-100 text-blue-800':    admin.role === 'Admin',
                        'bg-gray-100 text-gray-700':    admin.role === 'Editor',
                      }"
                    >{{ admin.role }}</span>
                  </div>
                </div>

                <!-- Remove button (can't remove Owner, can't remove self if last Owner) -->
                <button
                  v-if="admin.userId !== auth.user?.id && admin.role !== 'Owner'"
                  class="text-xs text-red-500 hover:text-red-700"
                  @click="removeAdmin(admin.userId)"
                >Remove</button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>
  </div>

  <!-- Edit modal -->
  <Teleport to="body">
    <div
      v-if="showEditModal"
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm"
      @click.self="showEditModal = false"
    >
      <div class="bg-white rounded-2xl shadow-2xl w-full max-w-md max-h-[90vh] flex flex-col">
        <div class="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <h2 class="text-lg font-bold text-gray-900">Edit Page</h2>
          <button class="text-gray-400 hover:text-gray-600 p-1 rounded-full hover:bg-gray-100 transition" @click="showEditModal = false">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <div class="overflow-y-auto flex-1 px-6 py-4 space-y-4">
          <!-- Cover -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Cover Image</label>
            <div v-if="editCoverPreview" class="relative h-24 rounded-xl overflow-hidden">
              <img :src="editCoverPreview" class="w-full h-full object-cover"/>
              <button type="button" class="absolute top-2 right-2 bg-black/50 text-white rounded-full p-1"
                @click="editCoverFile = null; editCoverPreview = null"
              >
                <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                </svg>
              </button>
            </div>
            <div v-else class="relative h-24 rounded-xl overflow-hidden bg-gray-100">
              <img v-if="page?.coverUrl" :src="page.coverUrl" class="w-full h-full object-cover opacity-60"/>
              <label class="absolute inset-0 flex items-center justify-center cursor-pointer hover:bg-black/10 transition">
                <span class="text-xs text-gray-600 bg-white/80 px-2 py-1 rounded-full">Change cover</span>
                <input type="file" class="hidden" accept="image/*" @change="onEditCoverChange"/>
              </label>
            </div>
          </div>

          <!-- Logo -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Logo</label>
            <div class="flex items-center gap-3">
              <div class="w-14 h-14 rounded-xl border border-gray-200 overflow-hidden">
                <img v-if="editLogoPreview" :src="editLogoPreview" class="w-full h-full object-cover"/>
                <img v-else-if="page?.logoUrl" :src="page.logoUrl" class="w-full h-full object-cover"/>
                <div v-else class="w-full h-full bg-indigo-600 flex items-center justify-center text-white text-xl font-bold">
                  {{ page?.name.charAt(0).toUpperCase() }}
                </div>
              </div>
              <label class="text-xs text-indigo-600 cursor-pointer hover:underline">
                Change logo
                <input type="file" class="hidden" accept="image/*" @change="onEditLogoChange"/>
              </label>
            </div>
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Name</label>
            <input v-model="editName" type="text" maxlength="256"
              class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Description</label>
            <textarea v-model="editDesc" rows="3"
              class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 resize-none"
            />
          </div>

          <div v-if="editError" class="text-sm text-red-600 bg-red-50 rounded-lg px-3 py-2">
            {{ editError }}
          </div>
        </div>

        <div class="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
          <button type="button" class="px-4 py-2 text-sm font-medium text-gray-700 border border-gray-300 rounded-lg hover:bg-gray-50"
            @click="showEditModal = false">Cancel</button>
          <button type="button" :disabled="editSaving"
            class="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 disabled:opacity-50"
            @click="saveEdit">
            <span v-if="editSaving">Saving…</span>
            <span v-else>Save Changes</span>
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
