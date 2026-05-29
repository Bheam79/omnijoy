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
| 9000 | :9000 | MinIO S3 API (dev only — internal-only in prod, proxied via `/media/`) |
| 9001 | :9001 | MinIO web console (dev only) |

---

## Docker container naming

All containers use the prefix **`07ad0b82_omnijoy`**:

| Container name | Purpose |
|---|---|
| `07ad0b82_omnijoy_mysql` | MariaDB database |
| `07ad0b82_omnijoy_backend` | .NET API (production stack) |
| `07ad0b82_omnijoy_nginx` | Nginx reverse proxy |
| `07ad0b82_omnijoy_mediamtx` | RTMP/HLS live streaming |
| `07ad0b82_omnijoy_minio` | MinIO S3-compatible object store (user-uploaded media) |
| `07ad0b82_omnijoy_minio_init` | One-shot bucket creator (idempotent) — exits after bootstrap |
| `07ad0b82_omnijoy_blue` | Blue slot (legacy blue/green targets) |
| `07ad0b82_omnijoy_green` | Green slot (legacy blue/green targets) |

Docker network: `07ad0b82_omnijoy_net`
Volumes: `07ad0b82_omnijoy_mysql_data`, `07ad0b82_omnijoy_media_data`, `07ad0b82_omnijoy_minio_data`

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

make prod-up                 # Build image + start + recreate backend/nginx
# No separate prod-migrate needed — migrations auto-apply on backend startup.

# Updates (normal flow — source-code change):
git pull
make prod-up                 # That's it. Backend is force-recreated with the new image.

# What 'make prod-up' actually does (3 steps):
#   1. Build the backend image (Vite + dotnet publish inside Docker multi-stage).
#   2. Start any services that aren't running yet (data services untouched if healthy).
#   3. Force-recreate backend + nginx with the fresh image.
#      --force-recreate is required because podman-compose does NOT auto-recreate
#      containers when their image is rebuilt (unlike docker compose).
#
# Which services get restarted:
#   - backend  → ALWAYS recreated (step 3 is unconditional)
#   - nginx    → ALWAYS recreated (step 3)
#   - mysql / redis / minio / mediamtx → NOT restarted (step 2 is no-op for healthy containers).
#     Use `$(DOCKER) compose -f docker/docker-compose.prod.yml pull` to update them.
#
# DB migrations run automatically when the new backend container starts up.
# Run 'make prod-migrate' only if you need to apply migrations outside normal startup
# (e.g. manual schema inspection, rollback testing).

# Day-to-day:
make prod-status             # Show running containers
make prod-logs               # Tail all logs  (SVC=backend for one service)
make prod-logs SVC=backend
make prod-shell              # Shell into running backend container
make prod-restart            # Rebuild + restart backend only (fastest deploy)
make prod-nginx-reload       # Reload nginx after editing nginx.prod.conf
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

### Media storage (MinIO / S3)

- **MinIO compatibility (AWSSDK.S3 ≥ 3.7.412):** the SDK now auto-attaches
  `x-amz-sdk-checksum-algorithm` + `x-amz-checksum-crc32` headers to
  every PutObject, which MinIO rejects with **`Access Denied`**.
  `S3MediaStorageService` opts out via
  `RequestChecksumCalculation = WHEN_REQUIRED` and
  `ResponseChecksumValidation = WHEN_REQUIRED` on `AmazonS3Config`,
  plus `DisableDefaultChecksumValidation = true` on the upload request.
  Don't remove either guard when bumping the SDK.
- MinIO also rejects per-object ACLs — never set `CannedACL` on
  `TransferUtilityUploadRequest`. Public read is bucket-policy only
  (`mc anonymous set download …`).
- `Storage:Type` switches `IMediaStorageService` between `local`
  (`LocalMediaStorageService` → `wwwroot/uploads/`) and `s3`
  (`S3MediaStorageService` → MinIO / AWS S3 / R2). Dev defaults to
  `local`; the prod compose sets `Storage__Type=s3`.
- Prod stack ships a `07ad0b82_omnijoy_minio` container plus a one-shot
  `07ad0b82_omnijoy_minio_init` (`minio/mc`) that creates the bucket and
  sets the `download` anonymous policy. The init container is idempotent
  and exits with status 0 — its `Exited (0)` state in `docker ps -a` is
  expected.
- Backend reaches MinIO internally at `http://07ad0b82_omnijoy_minio:9000`.
  Public URLs are returned as **relative** paths under `/media/…` —
  `nginx.prod.conf` has a `/media/` `location` that `proxy_pass`es to
  the `omnijoy` bucket and stamps a 1-year immutable `Cache-Control`
  header on every response. Objects are content-addressed (UUID keys),
  so caching forever is safe.
- The bucket name in `nginx.prod.conf` (`http://minio/omnijoy/`) must
  match `MINIO_BUCKET` in `docker/.env`. Change them together.
- Required env vars: `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`,
  `MINIO_BUCKET` (see `docker/.env.example`). The root user/password
  double as the S3 access key/secret for the backend.
- Dev compose publishes ports 9000 (API) and 9001 (web console) so a
  host-side backend can hit the store when `Storage__Type=s3` is set.

### Vanity URL slugs

- `User.UrlSlug` + `CompanyPage.UrlSlug` (nullable, unique-per-table).
  Cross-table uniqueness enforced in `SlugService` (no FK / cross-table index).
- `SlugValidator` (Omnijoy.Core/Services) is pure: rules = 3–30 chars,
  lowercase a–z/0–9/`-`/`_`, must start with a letter, no consecutive
  separators, no trailing separator. Always lowercased on store.
- `ReservedSlugs` is a hard-coded `FrozenSet` in `SlugValidator`. **It must
  stay in sync with every top-level Vue Router path in
  `frontend/src/router/index.ts`.** When adding a new top-level frontend
  route, add the path segment to `ReservedSlugs` in the same commit.
  The `router/index.ts` file has a comment pointing at the constant location.
- Frontend vanity-URL helpers: `useProfileUrl({ id, urlSlug? })` →
  `/{slug}` or `/profile/{id}`;  `useCompanyUrl({ id, urlSlug? })` →
  `/{slug}` or `/company/{id}`.  Always use these instead of building paths by hand.
- The catch-all `/:slug` route in the frontend router resolves via
  `GET /api/slugs/resolve/{slug}` and redirects to the correct profile/company
  view.  It is registered just before the `/:pathMatch(.*)* not-found` route.
- API: `GET /api/slugs/check?slug=…`, `GET /api/slugs/resolve/{slug}`,
  `PUT /api/users/me/slug`, `PUT /api/company-pages/{id}/slug` (Owner/Admin
  only, Editor rejected).
- Race safety: pre-flight `IsSlugTakenAsync` + `SaveChanges` translates a
  unique-index violation back into a "taken" `InvalidOperationException`.

### Rate limiting

Implemented via `Microsoft.AspNetCore.RateLimiting` (built-in .NET 8+) with Redis-backed
counters when Redis is available, falling back gracefully to per-instance in-memory limiters.
Registration: `services.AddOmnijoyRateLimiting(redisConnectionString, builder.Configuration)`
in `Program.cs`. Middleware: `app.UseRateLimiter()` (after `UseAuthorization()`).

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

**Per-environment overrides.** Every numeric limit can be overridden from
configuration via the `RateLimiting:*` section — production keeps the
constants above; `appsettings.Development.json` ships an E2E-friendly
profile (1000 uploads/hr, 200 strict/min) so the Playwright suite isn't
cascade-failed by the 20/hour upload bucket. Keys: `RateLimiting:Upload:PermitLimit`,
`RateLimiting:Upload:WindowSeconds` (`WindowMinutes` also accepted),
plus `Strict:` and `Global:Ip|UserPermitLimit` siblings. For
`make test-e2e-prod`, set `RATELIMIT_UPLOAD_PERMITS=1000` (etc.) in
`docker/.env` — `docker-compose.prod.yml` already plumbs them into
the backend container as `RateLimiting__Upload__PermitLimit`.

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

| E2E target | Stack | Storage | Redis | When to run |
|---|---|---|---|---|
| `make test-e2e` | Dev (dotnet watch + Vite) | Local (`wwwroot/uploads/`) | Optional | Fast inner loop |
| `make test-e2e-prod` | Prod Docker stack | **MinIO (S3)** | **Yes** | Before tagging a release |

`make test-e2e-prod` requires `docker/.env` to exist and `make prod-up` to have been run.
It reads `PUBLIC_PORT` from `.env` and sets `BASE_URL` accordingly (default: `http://localhost:80`).

`e2e/tests/api/health.api.spec.ts` is the first spec to run and hits `GET /api/health`.
If the stack is not up at all this single test fails loudly rather than producing hundreds
of cascading assertion errors.

### E2E — 5xx guard

All E2E spec files import `test` and `expect` from `e2e/support/fixtures.ts`
(not directly from `@playwright/test`).  The fixture automatically:

- **API tests** — wraps `request` to throw immediately on any HTTP 5xx response.
- **Browser tests** — attaches `page.on('response')` + `page.on('requestfailed')`
  and fails the test after the test body if any 5xx or network failure was seen.

To intentionally test a 5xx path, opt out for that describe block or test:

```ts
test.use({ allow5xx: true })
test('returns 500 when …', async ({ request }) => {
  const resp = await request.get('/api/broken')
  expect(resp.status()).toBe(500)
})
```

### E2E — SignalR coverage

Specs under `e2e/tests/api/signalr/` exercise each hub end-to-end:

- `notifications.signalr.spec.ts` — FriendRequest push + presence broadcast.
- `chat.signalr.spec.ts` — ReceiveMessage + UserTyping.
- `feed.signalr.spec.ts` — NewPost, NewSharedPost, ReactionCountsUpdated.
- `live.signalr.spec.ts` — ViewerJoined / ViewerLeft / LiveChatMessage.

Helpers:

- `e2e/support/signalr-client.ts` — `connectToHub`, `waitForEvent`,
  `waitForEvent2`, `disposeAll`. Connects via the `access_token` query-string
  pattern that `JwtBearerEvents.OnMessageReceived` looks for. `disposeAll`
  pauses 500 ms after stopping so the server has time to run
  `OnDisconnectedAsync` before the next test connects.
- `e2e/support/shared-auth.ts` — global setup pre-fetches JWTs for every
  seed user once and persists them to `e2e/.auth/shared-auth.json`. The
  SignalR specs read from this cache instead of logging in per test,
  keeping the suite well under the `strict` (10 req/min/IP) auth rate
  limit. The `.auth/` directory is `.gitignore`d.

When adding a new hub event, mirror the test pattern: register the
`waitForEvent` listener *before* triggering the REST call so the test
can't race the push.
