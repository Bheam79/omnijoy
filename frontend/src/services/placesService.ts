import api from './api'
import type { AutocompleteResult, PlaceSelection } from '@/types/places'

// ── DTOs (mirrors backend PlaceAutocompleteResultDto / PlaceDetailsDto) ────────

interface AutocompleteResultDto {
  placeId: string
  description: string
  mainText: string
  secondaryText: string
}

interface PlaceDetailsDto {
  placeId: string
  formattedAddress: string
  city: string | null
  country: string | null
  countryCode: string | null
  latitude: number | null
  longitude: number | null
}

// ── Service ───────────────────────────────────────────────────────────────────

export const placesService = {
  /**
   * Returns autocomplete predictions for the given input text.
   *
   * @param input        Partial address / city name.
   * @param mode         '(cities)' for user location, '(address)' for event/company.
   * @param sessionToken Client-generated UUID session token (reuse across keystroke calls).
   */
  async autocomplete(
    input: string,
    mode: string,
    sessionToken: string,
  ): Promise<AutocompleteResult[]> {
    const { data } = await api.get<AutocompleteResultDto[]>('/api/places/autocomplete', {
      params: {
        input,
        types: mode,
        sessiontoken: sessionToken,
      },
    })
    return data
  },

  /**
   * Resolves a place_id to structured location data.
   * Pass the same sessionToken used during autocomplete to close the billing session.
   *
   * @param placeId      Google place_id from a prior autocomplete call.
   * @param sessionToken The session token used during autocomplete.
   */
  async getDetails(placeId: string, sessionToken: string): Promise<PlaceSelection> {
    const { data } = await api.get<PlaceDetailsDto>(`/api/places/details/${encodeURIComponent(placeId)}`, {
      params: { sessiontoken: sessionToken },
    })
    return {
      placeId:          data.placeId,
      displayName:      data.formattedAddress || data.city || data.placeId,
      city:             data.city,
      country:          data.country,
      countryCode:      data.countryCode,
      latitude:         data.latitude,
      longitude:        data.longitude,
      formattedAddress: data.formattedAddress,
    }
  },
}

export default placesService
