/**
 * Public-facing location handling for the unauthenticated front page.
 *
 * - Persists the visitor's chosen location (city / town string) to localStorage
 *   so the public events feed can stay narrowed-down across reloads.
 * - Wraps the browser Geolocation API + a free reverse-geocoding lookup
 *   (OpenStreetMap Nominatim) so visitors can opt in to auto-detection rather
 *   than typing a city by hand.
 *
 * No polling, no SignalR — this is the public landing experience only.
 */
import { ref } from 'vue'

const STORAGE_KEY = 'omnijoy_public_location'

/** Module-level reactive ref shared by every consumer of this composable. */
const publicLocation = ref<string | null>(loadFromStorage())

function loadFromStorage(): string | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw && raw.trim().length > 0 ? raw : null
  } catch {
    return null
  }
}

function persist(value: string | null) {
  try {
    if (value && value.trim().length > 0) {
      localStorage.setItem(STORAGE_KEY, value.trim())
    } else {
      localStorage.removeItem(STORAGE_KEY)
    }
  } catch {
    /* ignore — storage may be disabled */
  }
}

/**
 * Reverse-geocode coordinates to a short city label using OpenStreetMap's
 * Nominatim service. We pick the most specific available address part
 * (city / town / village / suburb) so the resulting string is short enough
 * to match against typical event Location text.
 */
async function reverseGeocode(latitude: number, longitude: number): Promise<string | null> {
  const params = new URLSearchParams({
    format:           'jsonv2',
    lat:              String(latitude),
    lon:              String(longitude),
    'accept-language': navigator.language || 'en',
    zoom:             '10',
  })
  const url = `https://nominatim.openstreetmap.org/reverse?${params.toString()}`

  const response = await fetch(url, {
    headers: { Accept: 'application/json' },
  })
  if (!response.ok) throw new Error(`Reverse geocode failed (${response.status})`)

  const data = (await response.json()) as {
    address?: {
      city?: string
      town?: string
      village?: string
      hamlet?: string
      suburb?: string
      municipality?: string
      county?: string
      state?: string
      country?: string
    }
    display_name?: string
  }

  const a = data.address ?? {}
  return (
    a.city
    ?? a.town
    ?? a.village
    ?? a.hamlet
    ?? a.suburb
    ?? a.municipality
    ?? a.county
    ?? a.state
    ?? a.country
    ?? null
  )
}

export function usePublicLocation() {
  function setLocation(value: string | null) {
    const trimmed = value?.trim() || null
    publicLocation.value = trimmed
    persist(trimmed)
  }

  /**
   * Asks the browser for the visitor's coordinates, then resolves them to a
   * city name and stores it. Rejects with an Error containing a human-readable
   * message on failure (permission denied, no geolocation support, network
   * error, etc.) so the caller can render an inline error.
   */
  async function detectFromBrowser(): Promise<string> {
    if (typeof navigator === 'undefined' || !navigator.geolocation) {
      throw new Error('Geolocation is not supported by this browser.')
    }

    const position = await new Promise<GeolocationPosition>((resolve, reject) => {
      navigator.geolocation.getCurrentPosition(resolve, (err) => {
        if (err.code === err.PERMISSION_DENIED) {
          reject(new Error('Location permission was denied.'))
        } else if (err.code === err.POSITION_UNAVAILABLE) {
          reject(new Error('Your location is currently unavailable.'))
        } else if (err.code === err.TIMEOUT) {
          reject(new Error('Timed out while finding your location.'))
        } else {
          reject(new Error(err.message || 'Unable to determine your location.'))
        }
      }, { enableHighAccuracy: false, timeout: 10_000, maximumAge: 300_000 })
    })

    const city = await reverseGeocode(position.coords.latitude, position.coords.longitude)
    if (!city) throw new Error('Could not determine your city from your location.')

    setLocation(city)
    return city
  }

  return {
    publicLocation,
    setLocation,
    detectFromBrowser,
  }
}
