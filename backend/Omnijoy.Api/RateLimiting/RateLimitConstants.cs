namespace Omnijoy.Api.RateLimiting;

/// <summary>
/// Centralised constants for all rate-limit policy names and numeric limits.
/// Referenced from Program.cs, controllers, and tests so changes stay in one place.
/// </summary>
public static class RateLimitConstants
{
    // ── Policy names ──────────────────────────────────────────────────────────

    /// <summary>10 req/min — login, register, OTP, OAuth endpoints.</summary>
    public const string StrictPolicy = "strict";

    /// <summary>20 req/hour — media-upload endpoints (posts, avatar, messages).</summary>
    public const string UploadPolicy = "upload";

    // ── Global limiter limits ─────────────────────────────────────────────────

    /// <summary>200 requests per minute for unauthenticated requests (per IP).</summary>
    public const int GlobalIpPermitLimit = 200;

    /// <summary>600 requests per minute for authenticated requests (per userId).</summary>
    public const int GlobalUserPermitLimit = 600;

    /// <summary>Sliding window for the global policy.</summary>
    public static readonly TimeSpan GlobalWindow = TimeSpan.FromMinutes(1);

    // ── Strict policy limits ──────────────────────────────────────────────────

    /// <summary>10 requests per minute per IP on auth endpoints.</summary>
    public const int StrictPermitLimit = 10;

    /// <summary>Window for the strict policy.</summary>
    public static readonly TimeSpan StrictWindow = TimeSpan.FromMinutes(1);

    // ── Upload policy limits ──────────────────────────────────────────────────

    /// <summary>20 requests per hour per userId on upload endpoints.</summary>
    public const int UploadPermitLimit = 20;

    /// <summary>Window for the upload policy.</summary>
    public static readonly TimeSpan UploadWindow = TimeSpan.FromHours(1);
}
