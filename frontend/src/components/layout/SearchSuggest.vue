<script setup lang="ts">
/**
 * Top-nav search input with debounced auto-suggest dropdown.
 *
 * The composable {@link useSearchSuggest} owns the debouncing + keyboard
 * navigation state; this component is purely the visual + DOM-event layer.
 */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useSearchSuggest, type SuggestItem } from '@/composables/useSearchSuggest'

const router = useRouter()

const {
  query,
  loading,
  open,
  highlighted,
  items,
  hasResults,
  groups,
  moveDown,
  moveUp,
  selectHighlighted,
  reset,
  close,
} = useSearchSuggest()

const inputRef = ref<HTMLInputElement | null>(null)

const trimmedQuery = computed(() => query.value.trim())

function onFocus() {
  if (trimmedQuery.value.length > 0) open.value = true
}

function onBlur() {
  // Delay so click handlers on items can fire first
  setTimeout(() => { close() }, 150)
}

function onKeydown(e: KeyboardEvent) {
  switch (e.key) {
    case 'ArrowDown':
      e.preventDefault()
      open.value = true
      moveDown()
      break
    case 'ArrowUp':
      e.preventDefault()
      open.value = true
      moveUp()
      break
    case 'Enter': {
      e.preventDefault()
      const item = selectHighlighted()
      if (item) {
        goToItem(item)
      } else if (trimmedQuery.value.length > 0) {
        goToResultsPage()
      }
      break
    }
    case 'Escape':
      e.preventDefault()
      close()
      inputRef.value?.blur()
      break
  }
}

function goToItem(item: SuggestItem) {
  const target = item.routeTo
  reset()
  router.push(target)
}

function goToResultsPage() {
  const q = trimmedQuery.value
  if (!q) return
  reset()
  router.push({ name: 'search', query: { q } })
}

function categoryLabel(category: SuggestItem['category']) {
  switch (category) {
    case 'user':    return 'People'
    case 'post':    return 'Posts'
    case 'event':   return 'Events'
    case 'company': return 'Companies'
  }
}

function indexOf(item: SuggestItem): number {
  return items.value.findIndex(i => i.category === item.category && i.id === item.id)
}
</script>

<template>
  <div class="relative w-full">
    <!-- Search input -->
    <div class="relative">
      <svg class="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
      </svg>
      <input
        ref="inputRef"
        v-model="query"
        type="search"
        role="combobox"
        aria-autocomplete="list"
        aria-controls="search-suggest-listbox"
        :aria-expanded="open && hasResults"
        placeholder="Search Omnijoy…"
        class="w-full bg-gray-100 rounded-full pl-9 pr-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white transition-colors"
        @focus="onFocus"
        @blur="onBlur"
        @keydown="onKeydown"
      />
    </div>

    <!-- Dropdown -->
    <div
      v-if="open && trimmedQuery.length > 0"
      id="search-suggest-listbox"
      role="listbox"
      class="absolute z-50 left-0 right-0 mt-1 bg-white rounded-xl shadow-lg border border-gray-200 max-h-[28rem] overflow-y-auto"
    >
      <!-- Loading -->
      <div v-if="loading" class="px-4 py-6 text-center text-sm text-gray-500">
        Searching…
      </div>

      <!-- Empty -->
      <div v-else-if="!hasResults" class="px-4 py-6 text-center text-sm text-gray-500">
        No results for "<span class="font-medium">{{ trimmedQuery }}</span>"
      </div>

      <!-- Results, grouped by category -->
      <template v-else>
        <!-- People -->
        <section v-if="groups.users.length > 0">
          <header class="px-4 pt-3 pb-1 flex items-center gap-1.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">
            <svg class="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"/>
            </svg>
            {{ categoryLabel('user') }}
          </header>
          <button
            v-for="(u, i) in groups.users"
            :key="`user-${u.id}`"
            type="button"
            role="option"
            class="w-full text-left px-4 py-2 flex items-center gap-3 hover:bg-gray-50 transition-colors"
            :class="indexOf({ category: 'user', id: u.id, label: u.displayName, routeTo: '' }) === highlighted ? 'bg-indigo-50' : ''"
            :data-test="`suggest-user-${i}`"
            @mousedown.prevent="goToItem({
              category: 'user',
              id: u.id,
              label: u.displayName,
              routeTo: `/profile/${u.id}`,
            })"
          >
            <img
              v-if="u.avatarUrl"
              :src="u.avatarUrl"
              :alt="u.displayName"
              class="h-8 w-8 rounded-full object-cover"
            />
            <div
              v-else
              class="h-8 w-8 rounded-full bg-indigo-100 text-indigo-600 flex items-center justify-center text-xs font-bold"
            >
              {{ u.displayName.charAt(0).toUpperCase() }}
            </div>
            <div class="min-w-0 flex-1">
              <p class="text-sm font-medium text-gray-900 truncate">{{ u.displayName }}</p>
              <p v-if="u.bio" class="text-xs text-gray-500 truncate">{{ u.bio }}</p>
            </div>
          </button>
        </section>

        <!-- Posts -->
        <section v-if="groups.posts.length > 0">
          <header class="px-4 pt-3 pb-1 flex items-center gap-1.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">
            <svg class="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
            </svg>
            {{ categoryLabel('post') }}
          </header>
          <button
            v-for="(p, i) in groups.posts"
            :key="`post-${p.id}`"
            type="button"
            role="option"
            class="w-full text-left px-4 py-2 flex items-start gap-3 hover:bg-gray-50 transition-colors"
            :class="indexOf({ category: 'post', id: p.id, label: p.content, routeTo: '' }) === highlighted ? 'bg-indigo-50' : ''"
            :data-test="`suggest-post-${i}`"
            @mousedown.prevent="goToItem({
              category: 'post',
              id: p.id,
              label: p.content,
              routeTo: `/share/posts/${p.id}`,
            })"
          >
            <img
              v-if="p.authorAvatarUrl"
              :src="p.authorAvatarUrl"
              :alt="p.authorDisplayName"
              class="h-8 w-8 rounded-full object-cover shrink-0 mt-0.5"
            />
            <div
              v-else
              class="h-8 w-8 rounded-full bg-blue-100 text-blue-600 flex items-center justify-center text-xs font-bold shrink-0 mt-0.5"
            >
              {{ p.authorDisplayName.charAt(0).toUpperCase() }}
            </div>
            <div class="min-w-0 flex-1">
              <p class="text-sm text-gray-900 line-clamp-2">{{ p.content }}</p>
              <p class="text-xs text-gray-500 truncate">{{ p.authorDisplayName }}</p>
            </div>
          </button>
        </section>

        <!-- Events -->
        <section v-if="groups.events.length > 0">
          <header class="px-4 pt-3 pb-1 flex items-center gap-1.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">
            <svg class="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
            </svg>
            {{ categoryLabel('event') }}
          </header>
          <button
            v-for="(e, i) in groups.events"
            :key="`event-${e.id}`"
            type="button"
            role="option"
            class="w-full text-left px-4 py-2 flex items-center gap-3 hover:bg-gray-50 transition-colors"
            :class="indexOf({ category: 'event', id: e.id, label: e.title, routeTo: '' }) === highlighted ? 'bg-indigo-50' : ''"
            :data-test="`suggest-event-${i}`"
            @mousedown.prevent="goToItem({
              category: 'event',
              id: e.id,
              label: e.title,
              routeTo: `/events/${e.id}`,
            })"
          >
            <img
              v-if="e.coverImageUrl"
              :src="e.coverImageUrl"
              :alt="e.title"
              class="h-8 w-8 rounded-lg object-cover shrink-0"
            />
            <div
              v-else
              class="h-8 w-8 rounded-lg bg-purple-100 text-purple-600 flex items-center justify-center text-xs font-bold shrink-0"
            >
              📅
            </div>
            <div class="min-w-0 flex-1">
              <p class="text-sm font-medium text-gray-900 truncate">{{ e.title }}</p>
              <p class="text-xs text-gray-500 truncate">
                {{ new Date(e.startAt).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' }) }}
                <template v-if="e.location"> · {{ e.location }}</template>
              </p>
            </div>
          </button>
        </section>

        <!-- Companies -->
        <section v-if="groups.companies.length > 0">
          <header class="px-4 pt-3 pb-1 flex items-center gap-1.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">
            <svg class="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0H5m14 0h2a2 2 0 002-2v-1a1 1 0 00-1-1h-4M5 21H3a2 2 0 01-2-2v-1a1 1 0 011-1h4m6 6h0M9 7h6M9 11h6M9 15h6"/>
            </svg>
            {{ categoryLabel('company') }}
          </header>
          <button
            v-for="(c, i) in groups.companies"
            :key="`company-${c.id}`"
            type="button"
            role="option"
            class="w-full text-left px-4 py-2 flex items-center gap-3 hover:bg-gray-50 transition-colors"
            :class="indexOf({ category: 'company', id: c.id, label: c.name, routeTo: '' }) === highlighted ? 'bg-indigo-50' : ''"
            :data-test="`suggest-company-${i}`"
            @mousedown.prevent="goToItem({
              category: 'company',
              id: c.id,
              label: c.name,
              routeTo: `/company/${c.id}`,
            })"
          >
            <img
              v-if="c.logoUrl"
              :src="c.logoUrl"
              :alt="c.name"
              class="h-8 w-8 rounded-lg object-cover shrink-0"
            />
            <div
              v-else
              class="h-8 w-8 rounded-lg bg-gray-100 text-gray-700 flex items-center justify-center text-xs font-bold shrink-0"
            >
              {{ c.name.charAt(0).toUpperCase() }}
            </div>
            <div class="min-w-0 flex-1">
              <p class="text-sm font-medium text-gray-900 truncate">{{ c.name }}</p>
              <p class="text-xs text-gray-500 truncate">
                {{ c.followerCount }} follower{{ c.followerCount === 1 ? '' : 's' }}
              </p>
            </div>
          </button>
        </section>

        <!-- See all results -->
        <div class="border-t border-gray-100 mt-1">
          <button
            type="button"
            class="w-full text-center px-4 py-2.5 text-sm font-medium text-indigo-600 hover:bg-indigo-50 transition-colors"
            data-test="suggest-see-all"
            @mousedown.prevent="goToResultsPage"
          >
            See all results for "{{ trimmedQuery }}"
          </button>
        </div>
      </template>
    </div>
  </div>
</template>
