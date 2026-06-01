namespace Omnijoy.Core.DTOs.Places;

/// <summary>
/// A single autocomplete prediction returned by the Google Places Autocomplete proxy.
/// </summary>
/// <param name="PlaceId">Google Places place_id — opaque identifier for the place.</param>
/// <param name="Description">Full human-readable description (e.g. "Paris, France").</param>
/// <param name="MainText">Primary part of the description (e.g. "Paris").</param>
/// <param name="SecondaryText">Secondary part of the description (e.g. "France").</param>
public record PlaceAutocompleteResultDto(
    string PlaceId,
    string Description,
    string MainText,
    string SecondaryText);

/// <summary>
/// Structured location data resolved from a Google Places place_id.
/// </summary>
/// <param name="PlaceId">The Google Places place_id that was resolved.</param>
/// <param name="FormattedAddress">Full formatted address string.</param>
/// <param name="City">Locality / city name, if available.</param>
/// <param name="Country">Country long name (e.g. "France"), if available.</param>
/// <param name="CountryCode">ISO 3166-1 alpha-2 country code (e.g. "FR"), if available.</param>
/// <param name="Latitude">WGS-84 latitude, if available.</param>
/// <param name="Longitude">WGS-84 longitude, if available.</param>
public record PlaceDetailsDto(
    string  PlaceId,
    string  FormattedAddress,
    string? City,
    string? Country,
    string? CountryCode,
    double? Latitude,
    double? Longitude);
