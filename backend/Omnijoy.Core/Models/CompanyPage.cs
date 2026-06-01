namespace Omnijoy.Core.Models;

public class CompanyPage
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? CoverUrl { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Vanity URL slug — lowercase a–z / 0–9 / hyphen / underscore, 3–30 chars.
    /// Null when unset. Globally unique across users + company pages.
    /// </summary>
    public string? UrlSlug { get; set; }

    // ── Physical address ───────────────────────────────────────────────────────

    /// <summary>Google Place ID for the business address.</summary>
    public string? AddressPlaceId { get; set; }

    /// <summary>Full formatted address, e.g. "123 Main St, Oslo, Norway".</summary>
    public string? AddressText { get; set; }

    /// <summary>City name.</summary>
    public string? AddressCity { get; set; }

    /// <summary>Country name.</summary>
    public string? AddressCountry { get; set; }

    /// <summary>Latitude for distance comparisons (decimal degrees, WGS-84).</summary>
    public decimal? AddressLatitude { get; set; }

    /// <summary>Longitude for distance comparisons (decimal degrees, WGS-84).</summary>
    public decimal? AddressLongitude { get; set; }

    // Navigation properties
    public User CreatedByUser { get; set; } = null!;
    public ICollection<CompanyPageAdmin> Admins { get; set; } = [];
    public ICollection<CompanyPageFollow> Followers { get; set; } = [];
    public ICollection<Post> Posts { get; set; } = [];
    public ICollection<Event> Events { get; set; } = [];
}
