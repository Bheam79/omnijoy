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
| `07ad0b82_omnijoy_blue` | Blue deployment slot |
| `07ad0b82_omnijoy_green` | Green deployment slot |
| `07ad0b82_omnijoy_nginx` | Nginx reverse proxy (active slot router) |

Docker network: `07ad0b82_omnijoy_net`
Volume: `07ad0b82_omnijoy_mysql_data`

---

## Common commands

```bash
make dev              # Start everything for local development
make build            # Production build (frontend → backend/wwwroot, then publish)
make test             # Run all tests
make test-backend     # xUnit tests with coverage
make test-frontend    # Vitest tests with coverage

make deploy-blue      # Build + deploy to blue slot
make deploy-green     # Build + deploy to green slot
make switch           # Swap nginx to inactive slot (zero-downtime)
make rollback         # Swap back to previous slot

make start-db         # Start MariaDB container
make stop-db          # Stop MariaDB container
make migrate          # Run EF Core migrations
make status           # Show containers + active slot
make logs             # Tail active slot logs
make clean            # Remove build artifacts
```

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
