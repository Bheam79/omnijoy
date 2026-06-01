<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import SlugPicker from '@/components/shared/SlugPicker.vue'
import PlacePicker from '@/components/shared/PlacePicker.vue'
import { userService } from '@/services/userService'
import type { PlaceSelection } from '@/types/places'

const auth = useAuthStore()

const currentSlug = ref<string | null>(
  (auth.user as (typeof auth.user & { urlSlug?: string | null }))?.urlSlug ?? null,
)

function onSlugUpdated(newSlug: string | null) {
  currentSlug.value = newSlug
  // Persist slug back onto the cached user so TopNav / profile links update instantly.
  if (auth.user) {
    auth.setUser({ ...auth.user, urlSlug: newSlug ?? undefined } as typeof auth.user & { urlSlug?: string })
  }
}

// ── Location ──────────────────────────────────────────────────────────────────

/** Build a PlaceSelection from the auth-store user (pre-populate the picker). */
function buildInitialLocation(): PlaceSelection | null {
  const u = auth.user
  if (!u?.locationCountry && !u?.locationName) return null
  return {
    placeId:          'stored',
    displayName:      u.locationName ?? u.locationCountry ?? '',
    city:             u.locationCity ?? null,
    country:          u.locationCountry ?? null,
    countryCode:      null,
    latitude:         null,
    longitude:        null,
    formattedAddress: null,
  }
}

const locationSelection = ref<PlaceSelection | null>(buildInitialLocation())
const locationSaving = ref(false)
const locationSaveSuccess = ref(false)
const locationSaveError = ref<string | null>(null)

async function saveLocation() {
  if (!locationSelection.value) return
  locationSaving.value = true
  locationSaveSuccess.value = false
  locationSaveError.value = null

  try {
    const updated = await userService.updateProfile({
      locationPlaceId:    locationSelection.value.placeId !== 'manual' && locationSelection.value.placeId !== 'stored'
                            ? locationSelection.value.placeId
                            : null,
      locationName:       locationSelection.value.displayName,
      locationCity:       locationSelection.value.city,
      locationCountry:    locationSelection.value.country,
      locationCountryCode: locationSelection.value.countryCode,
      locationLatitude:   locationSelection.value.latitude,
      locationLongitude:  locationSelection.value.longitude,
    })
    locationSaveSuccess.value = true
    // Update the cached user so the new location is reflected immediately
    auth.setUser(updated)
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    locationSaveError.value = ax.response?.data?.error ?? 'Failed to save location.'
  } finally {
    locationSaving.value = false
  }
}

function onLocationChanged(value: PlaceSelection | null) {
  locationSelection.value = value
  // Reset feedback whenever the user changes the selection
  locationSaveSuccess.value = false
  locationSaveError.value = null
}
</script>

<template>
  <div data-testid="profile-settings-form">
    <!-- Back link -->
    <div class="flex items-center gap-2 mb-6">
      <RouterLink
        to="/settings"
        class="flex items-center gap-1.5 text-sm text-gray-500 hover:text-slate-300 transition-colors"
      >
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
        Settings
      </RouterLink>
    </div>

    <h1 class="text-2xl font-bold text-slate-100 mb-2">Profile</h1>
    <p class="text-sm text-gray-500 mb-6">Manage your public profile settings.</p>

    <!-- Location card -->
    <div
      v-if="auth.user"
      class="bg-slate-800 rounded-xl border border-slate-700 px-5 py-5 mb-4"
      data-testid="location-section"
    >
      <h2 class="text-base font-semibold text-slate-100 mb-1">Location</h2>
      <p class="text-xs text-gray-500 mb-4">
        Shown on your profile so friends can see where you're from.
      </p>

      <PlacePicker
        :model-value="locationSelection"
        mode="cities"
        label="Where are you from?"
        placeholder="Search for your city…"
        @update:model-value="onLocationChanged"
      />

      <!-- Save error -->
      <div
        v-if="locationSaveError"
        class="mt-3 rounded-lg bg-red-950 border border-red-800 px-3 py-2 text-sm text-red-400"
        data-testid="location-save-error"
      >
        {{ locationSaveError }}
      </div>

      <!-- Save success -->
      <div
        v-if="locationSaveSuccess"
        class="mt-3 rounded-lg bg-green-950 border border-green-800 px-3 py-2 text-sm text-green-400"
        data-testid="location-save-success"
      >
        Location saved!
      </div>

      <!-- Save button -->
      <div class="mt-3">
        <button
          type="button"
          :disabled="locationSaving || !locationSelection"
          class="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          data-testid="location-save-btn"
          @click="saveLocation"
        >
          <span v-if="locationSaving">Saving…</span>
          <span v-else>Save location</span>
        </button>
      </div>
    </div>

    <!-- Public URL card -->
    <SlugPicker
      v-if="auth.user"
      :initial-slug="currentSlug"
      scope="user"
      @updated="onSlugUpdated"
    />
  </div>
</template>
