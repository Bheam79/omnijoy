using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Omnijoy.Core.DTOs.Places;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Proxies requests to the Google Places Autocomplete and Details APIs using a
/// named <c>HttpClient</c> ("GooglePlaces"). The API key is read from
/// <c>Google:PlacesApiKey</c> (or environment variable
/// <c>Google__PlacesApiKey</c>). When the key is not configured the methods
/// return empty / null rather than throwing, so dev environments without a
/// key do not break.
/// </summary>
public sealed class PlacesProxyService : IPlacesProxyService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string?            _apiKey;
    private readonly ILogger<PlacesProxyService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PlacesProxyService(
        IHttpClientFactory         httpFactory,
        IConfiguration             configuration,
        ILogger<PlacesProxyService> logger)
    {
        _httpFactory = httpFactory;
        _apiKey      = configuration["Google:PlacesApiKey"];
        _logger      = logger;
    }

    // ── GET autocomplete ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PlaceAutocompleteResultDto>> GetAutocompleteAsync(
        string  input,
        string? types,
        string? sessionToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogDebug("Google:PlacesApiKey is not configured — returning empty autocomplete results.");
            return [];
        }

        try
        {
            var qs = BuildQueryString(new Dictionary<string, string?>
            {
                ["input"]        = input,
                ["types"]        = types,
                ["sessiontoken"] = sessionToken,
                ["key"]          = _apiKey
            });

            var client = _httpFactory.CreateClient("GooglePlaces");
            using var response = await client.GetAsync($"maps/api/place/autocomplete/json?{qs}");
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            var doc = await JsonSerializer.DeserializeAsync<AutocompleteResponse>(stream, JsonOptions);

            if (doc is null || doc.Status is not ("OK" or "ZERO_RESULTS"))
            {
                _logger.LogWarning(
                    "Google Places Autocomplete returned status={Status}", doc?.Status ?? "(null)");
                return [];
            }

            return doc.Predictions
                .Select(p => new PlaceAutocompleteResultDto(
                    PlaceId:       p.PlaceId ?? string.Empty,
                    Description:   p.Description ?? string.Empty,
                    MainText:      p.StructuredFormatting?.MainText ?? string.Empty,
                    SecondaryText: p.StructuredFormatting?.SecondaryText ?? string.Empty))
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Places Autocomplete request failed for input={Input}", input);
            return [];
        }
    }

    // ── GET details ──────────────────────────────────────────────────────────

    public async Task<PlaceDetailsDto?> GetDetailsAsync(string placeId, string? sessionToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogDebug("Google:PlacesApiKey is not configured — returning null for place details.");
            return null;
        }

        try
        {
            var fields = "place_id,formatted_address,address_components,geometry";
            var qs = BuildQueryString(new Dictionary<string, string?>
            {
                ["place_id"]     = placeId,
                ["fields"]       = fields,
                ["sessiontoken"] = sessionToken,
                ["key"]          = _apiKey
            });

            var client = _httpFactory.CreateClient("GooglePlaces");
            using var response = await client.GetAsync($"maps/api/place/details/json?{qs}");
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            var doc = await JsonSerializer.DeserializeAsync<DetailsResponse>(stream, JsonOptions);

            if (doc is null || doc.Status != "OK" || doc.Result is null)
            {
                _logger.LogWarning(
                    "Google Places Details returned status={Status} for placeId={PlaceId}",
                    doc?.Status ?? "(null)", placeId);
                return null;
            }

            var r = doc.Result;

            // Extract city, country and countryCode from address_components
            string? city        = GetComponent(r.AddressComponents, "locality")
                               ?? GetComponent(r.AddressComponents, "postal_town");
            string? country     = GetComponent(r.AddressComponents, "country", longName: true);
            string? countryCode = GetComponent(r.AddressComponents, "country", longName: false);

            return new PlaceDetailsDto(
                PlaceId:          r.PlaceId ?? placeId,
                FormattedAddress: r.FormattedAddress ?? string.Empty,
                City:             city,
                Country:          country,
                CountryCode:      countryCode,
                Latitude:         r.Geometry?.Location?.Lat,
                Longitude:        r.Geometry?.Location?.Lng);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Places Details request failed for placeId={PlaceId}", placeId);
            return null;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static string BuildQueryString(Dictionary<string, string?> parameters)
        => string.Join("&", parameters
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

    private static string? GetComponent(
        IReadOnlyList<AddressComponent>? components,
        string type,
        bool   longName = true)
        => components?
            .FirstOrDefault(c => c.Types?.Contains(type) == true)
            is { } comp
                ? (longName ? comp.LongName : comp.ShortName)
                : null;

    // ── Internal deserialization models ──────────────────────────────────────
    // These are private to the service — callers only see the public DTOs.

    private sealed class AutocompleteResponse
    {
        public string Status { get; set; } = string.Empty;
        public List<Prediction> Predictions { get; set; } = [];
    }

    private sealed class Prediction
    {
        [System.Text.Json.Serialization.JsonPropertyName("place_id")]
        public string? PlaceId { get; set; }

        public string? Description { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("structured_formatting")]
        public StructuredFormatting? StructuredFormatting { get; set; }
    }

    private sealed class StructuredFormatting
    {
        [System.Text.Json.Serialization.JsonPropertyName("main_text")]
        public string? MainText { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("secondary_text")]
        public string? SecondaryText { get; set; }
    }

    private sealed class DetailsResponse
    {
        public string Status { get; set; } = string.Empty;
        public PlaceResult? Result { get; set; }
    }

    private sealed class PlaceResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("place_id")]
        public string? PlaceId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("formatted_address")]
        public string? FormattedAddress { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("address_components")]
        public List<AddressComponent>? AddressComponents { get; set; }

        public PlaceGeometry? Geometry { get; set; }
    }

    private sealed class AddressComponent
    {
        [System.Text.Json.Serialization.JsonPropertyName("long_name")]
        public string? LongName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("short_name")]
        public string? ShortName { get; set; }

        public List<string>? Types { get; set; }
    }

    private sealed class PlaceGeometry
    {
        public LatLng? Location { get; set; }
    }

    private sealed class LatLng
    {
        public double? Lat { get; set; }
        public double? Lng { get; set; }
    }
}
