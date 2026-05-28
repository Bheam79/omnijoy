using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.Interfaces;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<OmnijoyDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

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

        // Support JWT in SignalR query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Presence + notifications ──────────────────────────────────────────────────
// PresenceTracker is a singleton so connection state is shared across requests.
// NotificationService is scoped (it uses DbContext); its dispatcher wraps
// IHubContext<NotificationHub> to push real-time events.
builder.Services.AddSingleton<IPresenceTracker, InMemoryPresenceTracker>();
builder.Services.AddScoped<IHubContextDispatcher, NotificationHubDispatcher>();
builder.Services.AddScoped<INotificationService, NotificationService>();

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
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPostService, PostService>();
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

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

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
