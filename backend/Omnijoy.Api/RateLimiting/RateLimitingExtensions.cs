using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using RedisRateLimiting;
using StackExchange.Redis;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Omnijoy.Api.RateLimiting;

/// <summary>
/// Extension methods to register all Omnijoy rate-limiting policies in
/// <c>Program.cs</c> via <see cref="AddOmnijoyRateLimiting"/>.
///
/// Policies:
/// <list type="table">
///   <listheader><term>Policy / limiter</term><description>Details</description></listheader>
///   <item>
///     <term>GlobalLimiter</term>
///     <description>
///       Applies to every request.
///       200 req/min per IP (unauthenticated) or 600 req/min per userId (authenticated).
///       Redis-backed when Redis is configured; in-memory FixedWindow fallback otherwise.
///     </description>
///   </item>
///   <item>
///     <term><see cref="RateLimitConstants.StrictPolicy"/> ("strict")</term>
///     <description>10 req/min per IP. Applied to auth endpoints.</description>
///   </item>
///   <item>
///     <term><see cref="RateLimitConstants.UploadPolicy"/> ("upload")</term>
///     <description>20 req/hour per userId. Applied to media-upload endpoints.</description>
///   </item>
/// </list>
///
/// All rejected requests receive <b>429 Too Many Requests</b> with a
/// <b>Retry-After</b> header (value in seconds).
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Registers the rate-limiting middleware and all four Omnijoy policies.
    /// A single <see cref="IConnectionMultiplexer"/> is created (and kept alive)
    /// when <paramref name="redisConnectionString"/> is non-empty; Redis-backed
    /// partitions store counters there so limits are shared across all instances.
    /// When Redis is unavailable the policies fall back gracefully to per-instance
    /// in-memory <see cref="FixedWindowRateLimiter"/>s.
    /// </summary>
    public static IServiceCollection AddOmnijoyRateLimiting(
        this IServiceCollection services,
        string? redisConnectionString,
        IConfiguration? configuration = null)
    {
        // Resolve the numeric limits — production defaults come from
        // RateLimitConstants, but every value can be overridden via
        // configuration ("RateLimiting:Upload:PermitLimit",
        // "RateLimiting:Upload:WindowSeconds", etc.) so non-prod
        // environments (Development / E2E suites) can raise the bucket
        // without recompiling the app.
        var limits = ResolveLimits(configuration);

        // A single long-lived multiplexer shared by all rate-limit partitions.
        IConnectionMultiplexer? muxer = null;
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            try
            {
                muxer = ConnectionMultiplexer.Connect(redisConnectionString);
            }
            catch (Exception ex)
            {
                // Log but don't crash — degrade gracefully to in-memory limiters.
                Console.Error.WriteLine(
                    $"[RateLimit] Redis connection failed ({ex.Message}); " +
                    "falling back to per-instance in-memory rate limiting.");
            }
        }

        services.AddRateLimiter(options =>
        {
            // ── Global limiter: covers every request ──────────────────────────
            // Authenticated users get a higher limit keyed by userId; anonymous
            // requests are capped by client IP.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                if (ctx.User.Identity?.IsAuthenticated == true)
                {
                    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon";
                    return CreatePartition(
                        $"global:user:{userId}",
                        limits.GlobalUserPermitLimit,
                        limits.GlobalWindow,
                        muxer);
                }

                var ip = GetClientIp(ctx);
                return CreatePartition(
                    $"global:ip:{ip}",
                    limits.GlobalIpPermitLimit,
                    limits.GlobalWindow,
                    muxer);
            });

            // ── Strict: 10 req/min per IP ─────────────────────────────────────
            // Apply via [EnableRateLimiting(RateLimitConstants.StrictPolicy)]
            // on AuthController (login, register, OTP, OAuth).
            options.AddPolicy<string>(
                RateLimitConstants.StrictPolicy,
                ctx => CreatePartition(
                    $"strict:{GetClientIp(ctx)}",
                    limits.StrictPermitLimit,
                    limits.StrictWindow,
                    muxer));

            // ── Upload: 20 req/hour per userId ────────────────────────────────
            // Apply via [EnableRateLimiting(RateLimitConstants.UploadPolicy)]
            // on POST media/avatar/cover/messages actions.
            options.AddPolicy<string>(
                RateLimitConstants.UploadPolicy,
                ctx =>
                {
                    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var key = string.IsNullOrEmpty(userId)
                        ? $"upload:ip:{GetClientIp(ctx)}"
                        : $"upload:user:{userId}";
                    return CreatePartition(
                        key,
                        limits.UploadPermitLimit,
                        limits.UploadWindow,
                        muxer);
                });

            // ── 429 rejection handler ─────────────────────────────────────────
            options.OnRejected = OnRejectedAsync;
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }

    /// <summary>
    /// Resolved (after-override) rate-limit values used by
    /// <see cref="AddOmnijoyRateLimiting"/>.
    /// </summary>
    internal readonly record struct ResolvedRateLimits(
        int GlobalIpPermitLimit,
        int GlobalUserPermitLimit,
        TimeSpan GlobalWindow,
        int StrictPermitLimit,
        TimeSpan StrictWindow,
        int UploadPermitLimit,
        TimeSpan UploadWindow);

    /// <summary>
    /// Reads <c>RateLimiting:*</c> overrides from <paramref name="configuration"/>
    /// (if supplied) and falls back to <see cref="RateLimitConstants"/> for
    /// anything not set. Window values can be specified as either
    /// <c>WindowSeconds</c> (preferred) or <c>WindowMinutes</c>.
    /// </summary>
    internal static ResolvedRateLimits ResolveLimits(IConfiguration? configuration)
    {
        var section = configuration?.GetSection("RateLimiting");

        return new ResolvedRateLimits(
            GlobalIpPermitLimit:   ReadInt(section,   "Global:IpPermitLimit",   RateLimitConstants.GlobalIpPermitLimit),
            GlobalUserPermitLimit: ReadInt(section,   "Global:UserPermitLimit", RateLimitConstants.GlobalUserPermitLimit),
            GlobalWindow:          ReadWindow(section, "Global",                RateLimitConstants.GlobalWindow),
            StrictPermitLimit:     ReadInt(section,   "Strict:PermitLimit",     RateLimitConstants.StrictPermitLimit),
            StrictWindow:          ReadWindow(section, "Strict",                RateLimitConstants.StrictWindow),
            UploadPermitLimit:     ReadInt(section,   "Upload:PermitLimit",     RateLimitConstants.UploadPermitLimit),
            UploadWindow:          ReadWindow(section, "Upload",                RateLimitConstants.UploadWindow));
    }

    private static int ReadInt(IConfigurationSection? section, string key, int fallback)
    {
        var raw = section?[key];
        return int.TryParse(raw, out var v) && v > 0 ? v : fallback;
    }

    private static TimeSpan ReadWindow(IConfigurationSection? section, string prefix, TimeSpan fallback)
    {
        // Prefer WindowSeconds (finer-grained); fall back to WindowMinutes
        // so existing configs that pre-date the seconds variant keep working.
        var seconds = section?[$"{prefix}:WindowSeconds"];
        if (int.TryParse(seconds, out var s) && s > 0)
            return TimeSpan.FromSeconds(s);

        var minutes = section?[$"{prefix}:WindowMinutes"];
        if (int.TryParse(minutes, out var m) && m > 0)
            return TimeSpan.FromMinutes(m);

        return fallback;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a Redis-backed or in-memory <see cref="RateLimitPartition{TKey}"/>
    /// depending on whether a <paramref name="muxer"/> is available.
    /// </summary>
    private static RateLimitPartition<string> CreatePartition(
        string key, int limit, TimeSpan window, IConnectionMultiplexer? muxer)
    {
        if (muxer is not null)
        {
            return RedisRateLimitPartition.GetFixedWindowRateLimiter(
                key,
                _ => new RedisFixedWindowRateLimiterOptions
                {
                    ConnectionMultiplexerFactory = () => muxer,
                    PermitLimit = limit,
                    Window      = window,
                });
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit      = limit,
                Window           = window,
                QueueLimit       = 0,
                AutoReplenishment = true,
            });
    }

    /// <summary>
    /// Returns the real client IP, honouring <c>X-Forwarded-For</c> when the
    /// request passes through a reverse proxy (nginx / Docker network).
    /// </summary>
    internal static string GetClientIp(HttpContext ctx)
    {
        var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            // X-Forwarded-For may be a comma-separated list; take the leftmost entry.
            var first = forwarded.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first))
                return first;
        }

        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Called when any rate limiter rejects a request. Returns 429 with a
    /// <c>Retry-After</c> header indicating when the client may retry.
    /// </summary>
    private static async ValueTask OnRejectedAsync(
        OnRejectedContext context, CancellationToken cancellationToken)
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }
        else
        {
            // Default hint when the lease doesn't carry retry timing.
            context.HttpContext.Response.Headers.RetryAfter = "60";
        }

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            """{"error":"Too many requests. Please slow down and try again later."}""",
            cancellationToken);
    }
}
