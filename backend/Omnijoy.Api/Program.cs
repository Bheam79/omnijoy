using Amazon.Runtime;
using Amazon.S3;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Omnijoy.Api.HealthChecks;
using Omnijoy.Api.Hubs;
using Omnijoy.Api.RateLimiting;
using Omnijoy.Core.Interfaces;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<OmnijoyDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// ── Redis ─────────────────────────────────────────────────────────────────────
var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
var redisEnabled = !string.IsNullOrWhiteSpace(redisConnectionString);

if (redisEnabled)
{
    // Distributed cache backed by Redis (token blacklist, rate-limit counters,
    // presence data for multi-node deployments).
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName   = "omnijoy:";
    });
}
else
{
    // Fallback for single-node dev / test: in-memory distributed cache.
    builder.Services.AddDistributedMemoryCache();
}

// ── Authentication (JWT) ──────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            // Support JWT in query string for:
            //   • SignalR hubs  (/hubs/*)
            //   • Browser-based live stream WebSocket ingest (/api/live/*/ingest)
            // Browsers cannot set custom headers on WebSocket connections, so the
            // JWT must be passed as ?access_token=... in the query string.
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs") ||
                     (path.StartsWithSegments("/api/live") &&
                      path.Value?.EndsWith("/ingest", StringComparison.OrdinalIgnoreCase) == true)))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },

            // Reject tokens whose JTI appears in the blacklist (post-logout)
            // or whose issued-at predates the user's token-invalidation cutoff
            // (set during a password reset to invalidate all outstanding JWTs).
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;

                // ── 1. Blacklist check (post-logout JTI) ──────────────────────
                var blacklist = context.HttpContext.RequestServices
                    .GetService<ITokenBlacklist>();
                if (blacklist is not null)
                {
                    var jti = principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                    if (!string.IsNullOrEmpty(jti) && await blacklist.IsBlacklistedAsync(jti))
                    {
                        context.Fail("Token has been revoked.");
                        return;
                    }
                }

                // ── 2. Token-invalidation cutoff check (post-password-reset) ──
                var subClaim = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                var iatClaim = principal?.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;

                if (!string.IsNullOrEmpty(subClaim) && Guid.TryParse(subClaim, out var tokenUserId)
                    && long.TryParse(iatClaim, out var issuedAtUnix))
                {
                    var db = context.HttpContext.RequestServices
                        .GetService<OmnijoyDbContext>();
                    if (db is not null
                        && await TokenCutoffChecker.IsTokenStaleAsync(db, tokenUserId, issuedAtUnix))
                    {
                        context.Fail("Token has been invalidated.");
                    }
                }
            }
        };
    });

// ── Authorization policies ────────────────────────────────────────────────────
// Named policies wrap the role checks so controllers can use either
// [Authorize(Policy = "RequireAdmin")] or [Authorize(Roles = "Admin")] — both work.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("RequireModeratorOrAdmin", policy =>
        policy.RequireRole("Admin", "Moderator"));
});

// ── SignalR ───────────────────────────────────────────────────────────────────
var signalRBuilder = builder.Services.AddSignalR();
if (redisEnabled)
{
    // Redis backplane: routes hub messages across multiple backend instances.
    signalRBuilder.AddStackExchangeRedis(redisConnectionString!, options =>
    {
        options.Configuration.ChannelPrefix =
            StackExchange.Redis.RedisChannel.Literal("omnijoy");
    });
}

// ── Presence + notifications ──────────────────────────────────────────────────
// PresenceTracker: Redis-backed when Redis is available (multi-node); falls
// back to in-memory for single-node dev. NotificationService is scoped
// (it uses DbContext); its dispatcher wraps IHubContext<NotificationHub>.
if (redisEnabled)
    builder.Services.AddSingleton<IPresenceTracker, RedisPresenceTracker>();
else
    builder.Services.AddSingleton<IPresenceTracker, InMemoryPresenceTracker>();

builder.Services.AddScoped<IHubContextDispatcher, NotificationHubDispatcher>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// ── Token blacklist (JWT revocation on logout) ────────────────────────────────
builder.Services.AddSingleton<ITokenBlacklist, RedisTokenBlacklist>();

// ── Rate limiting ─────────────────────────────────────────────────────────────
// GlobalLimiter: 200 req/min per IP (unauth) | 600 req/min per userId (auth)
// "strict":  10 req/min per IP  — applied to auth endpoints via [EnableRateLimiting]
// "upload":  20 req/hour per userId — applied to upload endpoints
//
// All numeric limits can be overridden via the "RateLimiting:*" config section
// (see appsettings.Development.json for the E2E-friendly overrides). Production
// uses the defaults from RateLimitConstants unless RateLimiting__Upload__PermitLimit
// (etc.) is set in the environment.
builder.Services.AddOmnijoyRateLimiting(redisConnectionString, builder.Configuration);

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

// ── Controllers & OpenAPI ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ── Global exception handling ────────────────────────────────────────────────
// Catches every uncaught exception that escapes a controller and produces a
// consistent JSON ProblemDetails response. Without this the framework returns
// a 500 with an empty body, making "post returns 500" reports hard to
// diagnose — see OMNIJOY-125 for the symptom. Also maps known infrastructure
// outages (e.g. MinIO unavailable → 502) to more accurate status codes.
builder.Services.AddExceptionHandler<Omnijoy.Api.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ── HTTP Client (for OG meta fetching & OAuth) ───────────────────────────────
builder.Services.AddHttpClient();

// Named client used by MetaPreviewController.  Timeout and User-Agent are
// configured here so the controller body stays lean and the settings are
// consistent across test and production.
builder.Services.AddHttpClient("MetaPreview", c =>
{
    c.Timeout = TimeSpan.FromSeconds(8);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("OmnijoyBot/1.0 (+https://omnijoy.local)");
});

// DNS resolver — mockable in unit tests via IHostResolver
builder.Services.AddSingleton<Omnijoy.Core.Interfaces.IHostResolver, Omnijoy.Api.Services.DnsHostResolver>();

// ── Google Places API proxy ───────────────────────────────────────────────────
// Named client used by PlacesProxyService. The API key is read from
// configuration (Google:PlacesApiKey / env var Google__PlacesApiKey).
// When the key is absent the service returns empty results so dev
// environments without a key do not break.
builder.Services.AddHttpClient("GooglePlaces", client =>
{
    client.BaseAddress = new Uri("https://maps.googleapis.com/");
    client.Timeout     = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<IPlacesProxyService, PlacesProxyService>();

// ── Memory Cache (for OG preview cache) ──────────────────────────────────────
builder.Services.AddMemoryCache();

// ── Auth services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountService, AccountService>();

// ── User / media services ─────────────────────────────────────────────────────
// Storage: "local" (default) saves to wwwroot/uploads/.
//          "s3" uses an S3-compatible bucket — configure Storage:S3:* keys.
var storageType = builder.Configuration["Storage:Type"] ?? "local";
if (storageType.Equals("s3", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IMediaStorageService, S3MediaStorageService>();
    // Verifies the bucket/credentials once at startup so a misconfiguration
    // (wrong access key, wrong endpoint, missing bucket policy) shows up in
    // `make prod-logs` immediately instead of on the next user upload — and
    // when it does fail, the log line carries the real MinIO error code
    // (e.g. SignatureDoesNotMatch) rather than the SDK's opaque "Access Denied".
    builder.Services.AddHostedService<S3StorageStartupProbe>();

    // Standalone IAmazonS3 used by MinioHealthCheck. S3MediaStorageService
    // builds its own client internally (it owns checksum quirks and per-request
    // config), so the health check gets a separate, isolated client wired from
    // the same Storage:S3:* configuration values. Keeping them separate means a
    // future change to the storage service's client construction can't silently
    // change probe behavior.
    builder.Services.AddSingleton<IAmazonS3>(sp =>
    {
        var cfg        = sp.GetRequiredService<IConfiguration>();
        var serviceUrl = cfg["Storage:S3:ServiceUrl"]
            ?? throw new InvalidOperationException("Storage:S3:ServiceUrl must be configured.");
        var accessKey  = cfg["Storage:S3:AccessKey"]
            ?? throw new InvalidOperationException("Storage:S3:AccessKey must be configured.");
        var secretKey  = cfg["Storage:S3:SecretKey"]
            ?? throw new InvalidOperationException("Storage:S3:SecretKey must be configured.");

        var s3Config = new AmazonS3Config
        {
            ServiceURL     = serviceUrl,
            ForcePathStyle = true,
            // Same checksum opt-out as S3MediaStorageService — MinIO rejects
            // the default CRC32 checksum headers added by AWSSDK 3.7.412+.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        return new AmazonS3Client(accessKey, secretKey, s3Config);
    });
}
else
{
    builder.Services.AddScoped<IMediaStorageService, LocalMediaStorageService>();
}

builder.Services.AddScoped<IPrivacyService, PrivacyService>();
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
builder.Services.AddScoped<IUserService, UserService>();
// ── Background task queue + thumbnail generator ───────────────────────────────
// IBackgroundTaskQueue is a singleton channel; ThumbnailGeneratorService drains it.
// IThumbnailService (scoped) enqueues jobs from PostService.
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddScoped<IThumbnailService, ThumbnailService>();
builder.Services.AddHostedService<ThumbnailGeneratorService>();

builder.Services.AddScoped<IPostService, PostService>();
// Feed cache (per-user page-1 + trending list). Uses IDistributedCache —
// Redis when configured, in-memory otherwise. Trade-offs documented on
// DistributedFeedCache + TrendingFeedRefreshService.
builder.Services.AddScoped<IFeedCache, DistributedFeedCache>();
builder.Services.AddHostedService<TrendingFeedRefreshService>();
// GDPR hard-delete: purges users whose DeletionScheduledAt grace period has
// elapsed (Account:DeletionGraceDays, default 30). Runs every 24h.
builder.Services.AddHostedService<AccountDeletionPurgeService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IReactionService, ReactionService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<IFriendInviteService, FriendInviteService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<ICompanyPageService, CompanyPageService>();
builder.Services.AddScoped<ILiveStreamService, LiveStreamService>();
builder.Services.AddScoped<IShareService, ShareService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<ISlugService, SlugService>();
builder.Services.AddScoped<IModerationLogService, ModerationLogService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// ── Health checks ─────────────────────────────────────────────────────────────
// Liveness:  GET /api/health/live  — always 200 if the process is up
//                                    (orchestrator restart trigger).
// Readiness: GET /api/health/ready — runs DB + Redis + MinIO probes. Returns
//                                    503 when any tagged "ready" probe is
//                                    degraded; the JSON body reports the
//                                    per-component status (UIResponseWriter).
//
// Only probes tagged "ready" run on the readiness endpoint; the liveness
// endpoint uses Predicate = _ => false to skip all probes.
var hcBuilder = builder.Services.AddHealthChecks()
    .AddMySql(connectionString, name: "mariadb", tags: ["ready"]);

if (redisEnabled)
    hcBuilder.AddRedis(redisConnectionString!, name: "redis", tags: ["ready"]);

if (storageType.Equals("s3", StringComparison.OrdinalIgnoreCase))
    hcBuilder.AddCheck<MinioHealthCheck>("minio", tags: ["ready"]);

var app = builder.Build();

// ── Auto-apply pending EF Core migrations on startup ─────────────────────────
// This ensures the schema is always in sync after a deploy without requiring
// a separate 'make prod-migrate' step.  If migrations fail the application
// will refuse to start — a broken deploy is better than a running app with a
// mismatched schema.
{
    using var scope = app.Services.CreateScope();
    var migrationLogger = scope.ServiceProvider
        .GetRequiredService<ILogger<OmnijoyDbContext>>();
    var db = scope.ServiceProvider.GetRequiredService<OmnijoyDbContext>();
    try
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            migrationLogger.LogInformation(
                "Applying {Count} pending migration(s): {Names}",
                pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync();
            migrationLogger.LogInformation("Database migrations applied successfully.");
        }
    }
    catch (Exception ex)
    {
        migrationLogger.LogCritical(ex,
            "Failed to apply database migrations. Startup aborted.");
        throw; // Abort startup — a mismatched schema will cause cascading failures.
    }
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Catch-all exception handler — registered first so it wraps every downstream
// middleware (auth, rate limiter, controllers, hubs).  Logs + returns
// ProblemDetails. See Middleware/GlobalExceptionHandler.cs.
app.UseExceptionHandler();

app.UseCors();

// WebSockets — must be registered before UseAuthentication so that WebSocket
// upgrade requests are handled, and before UseStaticFiles so that the
// /api/live/{id}/ingest endpoint is reachable as a WebSocket.
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
});

// ── Static files (Vue SPA served from wwwroot) ────────────────────────────────
// Registered BEFORE UseAuthentication / UseAuthorization / UseRateLimiter so
// that requests for index.html, JS bundles, CSS, and other assets short-circuit
// the pipeline before being counted by the 200 req/min-per-IP global limiter.
// A single SPA page load can pull in 10+ assets — keeping those out of the
// limiter bucket leaves headroom for the actual API traffic the bucket is
// meant to police.
//
// Cache-Control strategy:
//   index.html     → no-cache, no-store (contains the Vite chunk manifest;
//                    stale copies cause "Failed to fetch dynamically imported
//                    module" errors after a redeploy — see OMNIJOY-174/175).
//   /assets/* etc. → immutable, 1 year (content-hashed filenames; safe forever).
var spaStaticFileOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers["Pragma"]        = "no-cache";
            ctx.Context.Response.Headers["Expires"]       = "0";
        }
        else
        {
            // Content-hashed filenames — safe to cache indefinitely.
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
        }
    },
};

app.UseDefaultFiles();
app.UseStaticFiles(spaStaticFileOptions);

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// ── API Controllers ───────────────────────────────────────────────────────────
app.MapControllers();

// ── Health endpoints ──────────────────────────────────────────────────────────
// /api/health/live  → 200 as long as the process is up. Container orchestrators
//                     (k8s livenessProbe, compose healthcheck) use this to
//                     decide whether to restart the container.
// /api/health/ready → runs DB + Redis + MinIO probes (the ones tagged "ready").
//                     Returns 503 + per-component JSON body when degraded so
//                     load balancers can drain traffic from this instance
//                     while leaving the process alive.
// Both endpoints disable rate limiting — orchestrators must not be throttled.
app.MapHealthChecks("/api/health/live", new HealthCheckOptions
{
    Predicate         = _ => false,
    ResultStatusCodes = { [HealthStatus.Healthy] = StatusCodes.Status200OK },
}).DisableRateLimiting();

app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
{
    Predicate      = hc => hc.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
}).DisableRateLimiting();

// ── SignalR Hubs ──────────────────────────────────────────────────────────────
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<FeedHub>("/hubs/feed");
app.MapHub<LiveHub>("/hubs/live");

// ── SPA fallback (must be last) ───────────────────────────────────────────────
// Vue Router routes like /wall, /profile/:id, etc. don't exist as files on
// disk, so UseStaticFiles can't serve them — they fall through to this
// endpoint, which returns index.html. DisableRateLimiting keeps SPA route
// navigations out of the global limiter bucket for the same reason static
// assets are: serving the HTML shell is not API traffic.
//
// MapFallbackToFile uses a different internal code path than UseStaticFiles
// so OnPrepareResponse does not fire for it. We use MapFallback with an
// inline handler that stamps the same no-cache headers before serving the file.
app.MapFallback(async ctx =>
{
    ctx.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    ctx.Response.Headers["Pragma"]        = "no-cache";
    ctx.Response.Headers["Expires"]       = "0";
    ctx.Response.ContentType              = "text/html; charset=utf-8";

    var env  = ctx.RequestServices.GetRequiredService<IWebHostEnvironment>();
    var file = env.WebRootFileProvider.GetFileInfo("index.html");
    if (file.Exists)
        await ctx.Response.SendFileAsync(file);
}).DisableRateLimiting();

app.Run();
