# Omnijoy — CLAUDE.md

## Project overview

Omnijoy is a social media platform (like Facebook) without ads or forced content.
Full-stack: **C# .NET 10** backend + **Vue 3 / Vite / TailwindCSS** frontend + **MariaDB** (MySQL) in Docker.
Real-time via **SignalR** — no polling anywhere.

---

## Repository layout

```
/
├── backend/
│   ├── Omnijoy.Api/          # ASP.NET Core Web API + SignalR hubs
│   ├── Omnijoy.Core/         # Domain models, interfaces, DTOs
│   ├── Omnijoy.Infrastructure/ # EF Core DbContext, repositories, services
│   └── Omnijoy.Tests/        # xUnit unit tests
├── frontend/                 # Vue 3 + Vite + TailwindCSS SPA
├── docker/
│   ├── docker-compose.yml    # MariaDB container
│   ├── init.sql              # DB init script
│   └── .env.example          # Copy to .env and fill in
├── Makefile                  # All dev and deploy targets
├── Omnijoy.slnx              # .NET solution file
├── .gitignore
├── .editorconfig
└── CLAUDE.md                 # This file
```

---

## Dev environment setup

### Prerequisites
- .NET 10 SDK
- Node.js 22+
- Docker (for MariaDB)

### First-time setup

```bash
# 1. Copy env file and optionally edit passwords
cp docker/.env.example docker/.env

# 2. Start MariaDB
make start-db

# 3. Run EF Core migrations (once schema tasks are done — OMNIJOY-3)
make migrate

# 4. Start dev servers (backend on :5000, frontend on :5173)
make dev
```

The frontend Vite dev server proxies `/api` and `/hubs` to `http://localhost:5000` automatically.

---

## Port mappings

| Host port | Container | Purpose |
|-----------|-----------|---------|
| **31280** | :80 | Production HTTP (nginx → active slot) |
| 5000 | (local) | .NET backend dev / blue slot |
| 5001 | (local) | .NET backend green slot |
| 5173 | (local) | Vite frontend dev server |
| 3306 | (internal) | MariaDB (only accessible inside Docker network) |

---

## Docker container naming

All containers use the prefix **`07ad0b82_omnijoy`**:

| Container name | Purpose |
|---|---|
| `07ad0b82_omnijoy_mysql` | MariaDB database |
| `07ad0b82_omnijoy_backend` | .NET API (production stack) |
| `07ad0b82_omnijoy_nginx` | Nginx reverse proxy |
| `07ad0b82_omnijoy_mediamtx` | RTMP/HLS live streaming |
| `07ad0b82_omnijoy_blue` | Blue slot (legacy blue/green targets) |
| `07ad0b82_omnijoy_green` | Green slot (legacy blue/green targets) |

Docker network: `07ad0b82_omnijoy_net`
Volumes: `07ad0b82_omnijoy_mysql_data`, `07ad0b82_omnijoy_media_data`

---

## Common commands

```bash
make dev              # Start everything for local development
make build            # Production build (frontend → backend/wwwroot, then publish)
make test             # Run all tests
make test-backend     # xUnit tests with coverage
make test-frontend    # Vitest tests with coverage

make deploy-blue      # Build + deploy to blue slot (legacy)
make deploy-green     # Build + deploy to green slot (legacy)
make switch           # Swap nginx to inactive slot (zero-downtime, legacy)
make rollback         # Swap back to previous slot (legacy)

make start-db         # Start MariaDB container (dev)
make stop-db          # Stop MariaDB container (dev)
make migrate          # Run EF Core migrations (dev — host must reach DB)
make status           # Show containers + active slot
make logs             # Tail active slot logs
make clean            # Remove build artifacts
```

### Production stack (git pull → make)

```bash
# First-time setup on a server:
cp docker/.env.example docker/.env
$EDITOR docker/.env          # Set MYSQL_PASSWORD, JWT_SECRET_KEY, PUBLIC_PORT, etc.

make prod-up                 # Build image + start all services
make prod-migrate            # Apply DB migrations (runs via Docker, no host DB access needed)

# Updates:
git pull
make prod-up                 # Rebuilds image from source and restarts

# Day-to-day:
make prod-status             # Show running containers
make prod-logs               # Tail all logs  (SVC=backend for one service)
make prod-logs SVC=backend
make prod-shell              # Shell into running backend container
make prod-restart            # Rebuild + restart backend only (fastest deploy)
make prod-down               # Tear down the stack (volumes preserved)

# Persist across reboots via systemd --user:
make prod-install            # Install ~/.config/systemd/user/omnijoy.service
loginctl enable-linger $USER # Keep service running when not logged in
systemctl --user start omnijoy.service

make prod-uninstall          # Remove the systemd service

# Podman users — just override DOCKER:
make prod-up DOCKER=podman
```

**docker/.env key variables** (see `docker/.env.example` for full list):

| Variable | Required | Description |
|---|---|---|
| `MYSQL_ROOT_PASSWORD` | ✓ | MariaDB root password |
| `MYSQL_PASSWORD` | ✓ | App DB user password |
| `JWT_SECRET_KEY` | ✓ | Min 32 chars — signs auth tokens |
| `PUBLIC_PORT` | — | Host port for nginx (default: 80) |
| `RTMP_PORT` | — | RTMP ingest port (default: 1935) |
| `CORS_ORIGINS` | — | Production domain, e.g. `https://omnijoy.example.com` |

DB is **not published to the host** in production — only nginx publishes `PUBLIC_PORT`.

---

## Backend structure

- **Omnijoy.Api** — controllers, SignalR hubs, middleware, `Program.cs`
- **Omnijoy.Core** — entity models, DTOs, interfaces (no EF/infrastructure deps)
- **Omnijoy.Infrastructure** — `OmnijoyDbContext`, repository implementations, services
- **Omnijoy.Tests** — xUnit tests, uses EF InMemory + Moq + FluentAssertions

### SignalR hubs

| Hub | Path | Purpose |
|-----|------|---------|
| `NotificationHub` | `/hubs/notifications` | General user notifications |
| `ChatHub` | `/hubs/chat` | Messenger / direct messages |
| `FeedHub` | `/hubs/feed` | Real-time new-post delivery to followers |
| `LiveHub` | `/hubs/live` | Live streaming events + live chat |

All hubs require JWT authentication. The token is passed via the `access_token`
query parameter (SignalR WebSocket convention).

### Notifications + presence

- **`INotificationService`** (Infrastructure) persists rows to the
  `Notifications` table and pushes them in real time via `NotificationHub`.
  Inject it into controllers for both "create + push" (`CreateAsync`,
  `CreateForManyAsync`) and pure transient pushes (`PushTransientAsync`).
- The Infrastructure project must not reference the API project; the
  bridge is `IHubContextDispatcher` (declared in Infrastructure, implemented
  by `NotificationHubDispatcher` in `Omnijoy.Api/Hubs`).
- **`IPresenceTracker`** is registered as a singleton
  (`InMemoryPresenceTracker`). The `NotificationHub` calls
  `ConnectedAsync` / `DisconnectedAsync` on every hub-connection lifecycle
  event and broadcasts `UserOnline` / `UserOffline` to every accepted
  friend's `user:{friendId}` group.
- Frontend: `useNotificationsStore` owns the single connection to
  `/hubs/notifications`, forwards events to `useFriendsStore` /
  `usePresenceStore`. Connect on login from `TopNav.vue`, disconnect on
  logout. `<PresenceDot :user-id />` reads from the presence store and
  lazy-fetches via `GET /api/users/presence?userIds=…`.

### EF Core / database

- Provider: **Pomelo.EntityFrameworkCore.MySql** (EF Core 9.x — latest version that Pomelo supports)
- Connection string config key: `ConnectionStrings:DefaultConnection`
- Migrations project: `Omnijoy.Infrastructure`
- Startup project for migrations: `Omnijoy.Api`

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> \
  --project backend/Omnijoy.Infrastructure \
  --startup-project backend/Omnijoy.Api

# Apply migrations
dotnet ef database update \
  --project backend/Omnijoy.Infrastructure \
  --startup-project backend/Omnijoy.Api
```

### Feed cache

- `IFeedCache` (Core) + `DistributedFeedCache` (Infrastructure) wrap
  `IDistributedCache` (Redis in prod, in-memory in dev) with JSON serialization.
- Keys: `feed:{userId:N}:p1` (per-user page-1 only, 60s TTL),
  `trending:posts` (global trending list, refreshed every 5 min by
  `TrendingFeedRefreshService` — `BackgroundService`).
- `PostService.GetFeedAsync` only caches `page==1 && pageSize==20`. Custom
  page sizes and pages 2+ always hit the DB.
- Invalidation is push-style: `PostsController.CreatePost` calls
  `IFeedCache.InvalidateUserFeedsAsync(author + friends)`. Stale data
  bound = 60s TTL for users we miss (e.g. company-page followers).
- Cache failures (Redis down, JSON corruption) are logged and degrade to
  cache-miss; feed reads never fail because of caching.
- `GET /api/feed/trending` serves the cached list; falls back to a live
  `PostService.GetTrendingPostsAsync` query on cache miss.

### Rate limiting

Implemented via `Microsoft.AspNetCore.RateLimiting` (built-in .NET 8+) with Redis-backed
counters when Redis is available, falling back gracefully to per-instance in-memory limiters.
Registration: `services.AddOmnijoyRateLimiting(redisConnectionString)` in `Program.cs`.
Middleware: `app.UseRateLimiter()` (after `UseAuthorization()`).

| Policy | Key | Limit | Applied to |
|--------|-----|-------|-----------|
| **GlobalLimiter** (implicit) | IP (unauthenticated) | 200 req/min | Every request |
| **GlobalLimiter** (implicit) | userId (authenticated) | 600 req/min | Every request |
| `strict` | IP | 10 req/min | `AuthController` (class-level) |
| `upload` | userId or IP | 20 req/hour | POST media/avatar/cover/messages/events/company-pages |

Apply named policies with `[EnableRateLimiting(RateLimitConstants.StrictPolicy)]` or
`[EnableRateLimiting(RateLimitConstants.UploadPolicy)]` on controllers/actions.
All rejections return **429 Too Many Requests** with a `Retry-After` header (seconds).

Package: `RedisRateLimiting` 1.2.1 (cristipufu) — use `RedisRateLimitPartition.GetFixedWindowRateLimiter`
with `options.AddPolicy<string>(name, ctx => ...)` for per-user/IP partitioned Redis policies.
The `.AddRedisFixedWindowLimiter` extension only supports global (non-partitioned) counters.

---

## Frontend structure

- Framework: **Vue 3** (Composition API + `<script setup>`)
- Build tool: **Vite 8** with `@tailwindcss/vite` plugin (Tailwind v4)
- State: **Pinia**
- Routing: **Vue Router 4**
- HTTP: **axios**
- Real-time: **@microsoft/signalr**
- Tests: **Vitest** + `@vue/test-utils`

```
src/
├── components/      # Reusable UI components
│   ├── layout/      # TopNav, Sidebar, AppShell
│   ├── feed/        # FeedItem, FeedList
│   ├── post/        # PostComposer, PostCard
│   ├── chat/        # ChatWindow, ConversationList
│   ├── profile/     # ProfileCard
│   ├── events/      # EventCard
│   ├── company/     # CompanyCard
│   ├── live/        # LivePlayer
│   └── shared/      # Modal, Button, Avatar, etc.
├── composables/     # useAuth, useSignalR, useFeed, etc.
├── stores/          # Pinia stores (auth, feed, chat, notifications)
├── router/          # Vue Router config + guards
├── services/        # API service modules (axios wrappers)
├── types/           # TypeScript interfaces
└── views/           # Page components (route targets)
```

Build output goes to `backend/Omnijoy.Api/wwwroot/` — the .NET backend serves
the SPA as static files in production.

---

## Key technical notes

- **No polling** — all real-time features use SignalR exclusively.
- **Pomelo 9.0 + EF Core 9.x** — Pomelo does not yet support EF Core 10. The backend targets .NET 10 but uses EF Core 9 for MySQL compatibility.
- **TailwindCSS v4** — uses `@import "tailwindcss"` in CSS (no `tailwind.config.js`).
- **TypeScript 6 + `ignoreDeprecations: "6.0"`** — `baseUrl` in tsconfig is required for `@/` alias resolution.
- **Vue build → wwwroot** — `vite build` writes directly to `backend/Omnijoy.Api/wwwroot`. The .NET `MapFallbackToFile("index.html")` handles SPA routing.

---

## Testing targets

- Backend: 95% line coverage (enforced via `coverlet /p:Threshold=95`)
- Frontend: 95% line/branch/function/statement coverage (enforced in `vite.config.ts`)
- E2E: Playwright (project `/e2e`, see OMNIJOY-17)
