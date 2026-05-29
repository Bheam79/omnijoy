using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
            // Support JWT in SignalR query string
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },

            // Reject tokens whose JTI appears in the blacklist (post-logout)
            OnTokenValidated = async context =>
            {
                var blacklist = context.HttpContext.RequestServices
                    .GetService<ITokenBlacklist>();
                if (blacklist is null) return;

                var jti = context.Principal?
                    .FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrEmpty(jti) && await blacklist.IsBlacklistedAsync(jti))
                    context.Fail("Token has been revoked.");
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

// ── HTTP Client (for OG meta fetching & OAuth) ───────────────────────────────
builder.Services.AddHttpClient();

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
    builder.Services.AddScoped<IMediaStorageService, S3MediaStorageService>();
else
    builder.Services.AddScoped<IMediaStorageService, LocalMediaStorageService>();

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
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IReactionService, ReactionService>();
builder.Services.AddScoped<IFriendService, FriendService>();
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

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// ── Static files (Vue SPA served from wwwroot) ────────────────────────────────
app.UseDefaultFiles();
app.UseStaticFiles();

// ── API Controllers ───────────────────────────────────────────────────────────
app.MapControllers();

// ── SignalR Hubs ──────────────────────────────────────────────────────────────
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<FeedHub>("/hubs/feed");
app.MapHub<LiveHub>("/hubs/live");

// ── SPA fallback (must be last) ───────────────────────────────────────────────
app.MapFallbackToFile("index.html");

app.Run();
