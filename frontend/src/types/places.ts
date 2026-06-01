/**
 * Types for the PlacePicker component and Places API service.
 */

/**
 * A resolved place selection from the Google Places API.
 * Emitted by <PlacePicker> via `update:modelValue`.
 */
export interface PlaceSelection {
  /** Google place_id, or 'manual' when the user typed a value manually. */
  placeId: string
  /** Human-readable name shown in the chip, e.g. 'Oslo, Norway'. */
  displayName: string
  city: string | null
  country: string | null
  countryCode: string | null
  latitude: number | null
  longitude: number | null
  /** Full formatted address — populated in 'address' mode, null in 'cities' mode. */
  formattedAddress: string | null
}

/**
 * A single autocomplete suggestion returned by GET /api/places/autocomplete.
 */
export interface AutocompleteResult {
  placeId: string
  description: string
  mainText: string
  secondaryText: string
}
