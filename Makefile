# ─────────────────────────────────────────────────────────────────────────────
# Omnijoy — Makefile
# Blue/green deployment model:
#   - Blue slot runs on port 5000 (internal)
#   - Green slot runs on port 5001 (internal)
#   - Nginx (port 80 → host 31280) proxies to the active slot
#   - 'make switch' swaps the active slot with zero downtime
# ─────────────────────────────────────────────────────────────────────────────

SHELL       := /bin/bash
PROJECT     := omnijoy
PREFIX      := 07ad0b82_omnijoy
BLUE_PORT   := 5000
GREEN_PORT  := 5001
PUBLIC_PORT := 80

BLUE_APP    := $(PREFIX)_blue
GREEN_APP   := $(PREFIX)_green
NGINX_CONT  := $(PREFIX)_nginx
ACTIVE_FILE := /tmp/$(PROJECT)_active_slot

PUBLISH_DIR := /tmp/$(PROJECT)_publish
BLUE_DIR    := $(PUBLISH_DIR)/blue
GREEN_DIR   := $(PUBLISH_DIR)/green

# Production compose file + env file
PROD_COMPOSE  := docker/docker-compose.prod.yml
PROD_ENV      := docker/.env
PROD_ENV_EX   := docker/.env.example

# Docker / Podman — override with DOCKER=podman if needed
DOCKER        ?= docker
COMPOSE       ?= $(DOCKER) compose

# Absolute path to this repo — resolves to the HOST-visible path so that
# volume mounts in sibling Docker containers work correctly when running
# inside a dev-container (Docker-from-Docker / DinD setup).
#
# Inside a dev-container the workspace is a bind-mount from the host, e.g.:
#   /home/user/projects/omnijoy  →  /workspace  (inside the container)
# A sibling container started via the host Docker socket needs the HOST path,
# not the in-container path.  We detect this by inspecting our own container.
#
# Two-step: capture hostname first (avoids $$(hostname) quoting issues in
# $(shell ...)), then use it to look up the mount source via docker inspect.
_SELF_HOSTNAME    := $(shell hostname)
_HOST_WORKSPACE   := $(shell docker inspect $(_SELF_HOSTNAME) 2>/dev/null \
  | python3 -c "import sys,json;d=json.load(sys.stdin);[print(m['Source']) for m in d[0].get('Mounts',[]) if m.get('Destination')=='/workspace']" \
  2>/dev/null)
ifneq ($(_HOST_WORKSPACE),)
# Running inside a dev-container: translate /workspace → host source path.
REPO_PATH     := $(_HOST_WORKSPACE)/repo
else
# Running directly on the host or in CI: use the current directory.
REPO_PATH     := $(shell pwd)
endif

.PHONY: help build build-backend build-frontend \
        start-db stop-db start-redis stop-redis \
        deploy-blue deploy-green switch rollback \
        dev dev-backend dev-frontend \
        test test-backend test-frontend test-e2e test-e2e-api test-e2e-browser \
        test-e2e-prod \
        migrate clean status logs \
        prod-up prod-start prod-stop prod-down prod-restart \
        prod-build prod-migrate prod-logs prod-status prod-shell \
        prod-nginx-reload prod-rotate-minio prod-install prod-uninstall \
        prod-metrics _check-env

# ── Default target ────────────────────────────────────────────────────────────
help:
	@echo ""
	@echo "  Omnijoy — available targets"
	@echo ""
	@echo "  Development:"
	@echo "    make dev             Start DB + backend + frontend dev servers"
	@echo "    make dev-backend     Run .NET backend in watch mode (port 5000)"
	@echo "    make dev-frontend    Run Vite dev server (port 5173)"
	@echo "    make start-db        Start MariaDB Docker container"
	@echo "    make stop-db         Stop MariaDB Docker container"
	@echo "    make start-redis     Start Redis Docker container"
	@echo "    make stop-redis      Stop Redis Docker container"
	@echo ""
	@echo "  Build:"
	@echo "    make build           Build backend + frontend (production)"
	@echo "    make build-backend   Build .NET backend only"
	@echo "    make build-frontend  Build Vue frontend only"
	@echo ""
	@echo "  Database:"
	@echo "    make migrate         Run EF Core migrations"
	@echo ""
	@echo "  Deploy (blue/green):"
	@echo "    make deploy-blue     Build and deploy to blue slot (port $(BLUE_PORT))"
	@echo "    make deploy-green    Build and deploy to green slot (port $(GREEN_PORT))"
	@echo "    make switch          Switch nginx to the inactive slot"
	@echo "    make rollback        Switch nginx back to the previous slot"
	@echo ""
	@echo "  Testing:"
	@echo "    make test               Run all tests (backend + frontend)"
	@echo "    make test-backend       Run xUnit tests"
	@echo "    make test-frontend      Run Vitest tests"
	@echo "    make test-e2e           Run all E2E tests against the dev stack"
	@echo "    make test-e2e-api       Run E2E API tests only (no browser)"
	@echo "    make test-e2e-browser   Run E2E browser tests only"
	@echo "    make test-e2e-prod      Run all E2E tests against the prod stack"
	@echo "                            (MinIO + Redis — run 'make prod-up' first)"
	@echo ""
	@echo "  Production (full Docker stack — DB + backend + nginx):"
	@echo "    make prod-up         Build image + start all services (main entry point)"
	@echo "    make prod-start      Start without rebuilding (after prod-down)"
	@echo "    make prod-stop       Stop containers (keep them for prod-start)"
	@echo "    make prod-down       Stop and remove containers"
	@echo "    make prod-restart    Rebuild image and restart backend only"
	@echo "    make prod-migrate    Run EF Core migrations via temporary container"
	@echo "    make prod-logs       Tail logs (all services; pass SVC=backend to filter)"
	@echo "    make prod-status     Show production container status"
	@echo "    make prod-shell      Open a shell inside the backend container"
	@echo "    make prod-nginx-reload  Reload nginx after editing nginx.prod.conf.template (recreate container first for template changes)"
	@echo "    make prod-rotate-minio  Force-recreate MinIO + minio-init after .env credential change"
	@echo "    make prod-install    Install systemd user service (auto-start on boot)"
	@echo "    make prod-uninstall  Remove systemd user service"
	@echo ""
	@echo "  Misc:"
	@echo "    make status          Show running containers and active slot"
	@echo "    make logs            Tail logs from the active slot"
	@echo "    make clean           Remove build artifacts"
	@echo ""

# ── Development ───────────────────────────────────────────────────────────────
dev: start-db start-redis
	@echo ">> Starting dev servers..."
	@$(MAKE) -j2 dev-backend dev-frontend

dev-backend:
	@echo ">> Backend: http://localhost:5000"
	cd backend/Omnijoy.Api && dotnet watch run \
	  --urls http://localhost:5000

dev-frontend:
	@echo ">> Frontend: http://localhost:5173"
	cd frontend && npm run dev

# ── Database ──────────────────────────────────────────────────────────────────
start-db:
	@echo ">> Starting MariaDB..."
	$(COMPOSE) -f docker/docker-compose.yml --env-file docker/.env up -d mysql
	@echo ">> Waiting for DB to be healthy..."
	@until $(DOCKER) inspect --format='{{.State.Health.Status}}' $(PREFIX)_mysql 2>/dev/null | grep -q healthy; do \
	  sleep 2; \
	done
	@echo ">> DB is ready."

stop-db:
	$(COMPOSE) -f docker/docker-compose.yml stop mysql

start-redis:
	@echo ">> Starting Redis..."
	$(COMPOSE) -f docker/docker-compose.yml --env-file docker/.env up -d redis
	@echo ">> Waiting for Redis to be healthy..."
	@until $(DOCKER) inspect --format='{{.State.Health.Status}}' $(PREFIX)_redis 2>/dev/null | grep -q healthy; do \
	  sleep 2; \
	done
	@echo ">> Redis is ready."

stop-redis:
	$(COMPOSE) -f docker/docker-compose.yml stop redis

migrate:
	@echo ">> Running EF Core migrations..."
	cd backend && dotnet ef database update \
	  --project Omnijoy.Infrastructure \
	  --startup-project Omnijoy.Api

# ── Build ─────────────────────────────────────────────────────────────────────
build: build-frontend build-backend

build-frontend:
	@echo ">> Building Vue frontend..."
	cd frontend && npm ci && npm run build
	@echo ">> Frontend built → backend/Omnijoy.Api/wwwroot"

build-backend:
	@echo ">> Building .NET backend..."
	dotnet publish backend/Omnijoy.Api/Omnijoy.Api.csproj \
	  --configuration Release \
	  --output $(PUBLISH_DIR)/app \
	  --no-self-contained
	@echo ">> Backend published → $(PUBLISH_DIR)/app"

# ── Blue/Green Deployment ─────────────────────────────────────────────────────
deploy-blue: build
	@echo ">> Deploying to BLUE slot (port $(BLUE_PORT))..."
	@mkdir -p $(BLUE_DIR)
	@cp -r $(PUBLISH_DIR)/app/* $(BLUE_DIR)/
	@$(DOCKER) rm -f $(BLUE_APP) 2>/dev/null || true
	$(DOCKER) run -d \
	  --name $(BLUE_APP) \
	  --network 07ad0b82_omnijoy_net \
	  -p $(BLUE_PORT):80 \
	  -e ASPNETCORE_URLS=http://+:80 \
	  -e ASPNETCORE_ENVIRONMENT=Production \
	  -v $(BLUE_DIR):/app \
	  -w /app \
	  mcr.microsoft.com/dotnet/aspnet:10.0 \
	  dotnet Omnijoy.Api.dll
	@echo ">> Blue slot running on port $(BLUE_PORT)"
	@echo ">> Run 'make switch' to activate."

deploy-green: build
	@echo ">> Deploying to GREEN slot (port $(GREEN_PORT))..."
	@mkdir -p $(GREEN_DIR)
	@cp -r $(PUBLISH_DIR)/app/* $(GREEN_DIR)/
	@$(DOCKER) rm -f $(GREEN_APP) 2>/dev/null || true
	$(DOCKER) run -d \
	  --name $(GREEN_APP) \
	  --network 07ad0b82_omnijoy_net \
	  -p $(GREEN_PORT):80 \
	  -e ASPNETCORE_URLS=http://+:80 \
	  -e ASPNETCORE_ENVIRONMENT=Production \
	  -v $(GREEN_DIR):/app \
	  -w /app \
	  mcr.microsoft.com/dotnet/aspnet:10.0 \
	  dotnet Omnijoy.Api.dll
	@echo ">> Green slot running on port $(GREEN_PORT)"
	@echo ">> Run 'make switch' to activate."

switch:
	@echo ">> Switching active slot..."
	@if [ -f $(ACTIVE_FILE) ] && [ "$$(cat $(ACTIVE_FILE))" = "blue" ]; then \
	  NEW_SLOT=green; NEW_PORT=$(GREEN_PORT); OLD_SLOT=blue; \
	else \
	  NEW_SLOT=blue; NEW_PORT=$(BLUE_PORT); OLD_SLOT=green; \
	fi; \
	echo ">> Probing $$NEW_SLOT readiness at http://localhost:$$NEW_PORT/api/health/ready ..."; \
	HC_TRIES=20; HC_DELAY=2; HC_OK=0; \
	for i in $$(seq 1 $$HC_TRIES); do \
	  CODE=$$(curl -fsS -o /dev/null -w "%{http_code}" \
	    --max-time 5 "http://localhost:$$NEW_PORT/api/health/ready" 2>/dev/null || echo 000); \
	  if [ "$$CODE" = "200" ]; then HC_OK=1; break; fi; \
	  echo "   attempt $$i/$$HC_TRIES → $$CODE, retrying in $${HC_DELAY}s ..."; \
	  sleep $$HC_DELAY; \
	done; \
	if [ $$HC_OK -ne 1 ]; then \
	  echo ""; \
	  echo "  ERROR: $$NEW_SLOT failed readiness check on port $$NEW_PORT."; \
	  echo "  Refusing to switch nginx — $$OLD_SLOT remains active."; \
	  echo "  Tail the slot logs:  $(DOCKER) logs $(PREFIX)_$$NEW_SLOT"; \
	  exit 1; \
	fi; \
	echo ">> $$NEW_SLOT readiness OK. Activating..."; \
	$(MAKE) _nginx-point PORT=$$NEW_PORT; \
	echo $$NEW_SLOT > $(ACTIVE_FILE); \
	echo ">> Active slot: $$NEW_SLOT. Previous: $$OLD_SLOT is still running for rollback."

rollback:
	@echo ">> Rolling back..."
	@if [ -f $(ACTIVE_FILE) ] && [ "$$(cat $(ACTIVE_FILE))" = "green" ]; then \
	  PREV_SLOT=blue; PREV_PORT=$(BLUE_PORT); \
	else \
	  PREV_SLOT=green; PREV_PORT=$(GREEN_PORT); \
	fi; \
	echo ">> Rolling back to $$PREV_SLOT (port $$PREV_PORT)..."; \
	$(MAKE) _nginx-point PORT=$$PREV_PORT; \
	echo $$PREV_SLOT > $(ACTIVE_FILE); \
	echo ">> Rolled back to $$PREV_SLOT."

# Internal: update nginx upstream to target port
_nginx-point:
	@$(DOCKER) rm -f $(NGINX_CONT) 2>/dev/null || true
	@cat > /tmp/omnijoy_nginx.conf << 'NGINX' \
events {} \
http { \
  upstream app { server host-gateway:$(PORT); } \
  server { \
    listen $(PUBLIC_PORT); \
    location / { proxy_pass http://app; proxy_http_version 1.1; proxy_set_header Upgrade $$http_upgrade; proxy_set_header Connection "upgrade"; proxy_set_header Host $$host; } \
  } \
} \
NGINX
	$(DOCKER) run -d \
	  --name $(NGINX_CONT) \
	  --add-host host-gateway:host-gateway \
	  -p $(PUBLIC_PORT):$(PUBLIC_PORT) \
	  -v /tmp/omnijoy_nginx.conf:/etc/nginx/nginx.conf:ro \
	  nginx:alpine

# ── Testing ───────────────────────────────────────────────────────────────────
test: test-backend test-frontend

test-backend:
	@echo ">> Running backend tests..."
	dotnet test backend/Omnijoy.Tests/Omnijoy.Tests.csproj \
	  --configuration Release \
	  /p:CollectCoverage=true \
	  /p:CoverletOutputFormat=lcov \
	  /p:CoverletOutput=./coverage/lcov.info \
	  /p:Threshold=95 \
	  /p:ThresholdType=line

test-frontend:
	@echo ">> Running frontend tests..."
	cd frontend && npm run test -- --coverage

test-e2e:
	@echo ">> Running E2E tests (Playwright)..."
	cd e2e && npm ci && npx playwright test

test-e2e-api:
	@echo ">> Running E2E API tests only..."
	cd e2e && npm ci && npx playwright test tests/api

test-e2e-browser:
	@echo ">> Running E2E browser tests only..."
	cd e2e && npm ci && npx playwright test tests/browser

# Run the full Playwright suite against the production Docker stack
# (MinIO + Redis code paths are exercised instead of the local/in-memory fallbacks).
#
# Prerequisites:
#   1. docker/.env must exist and be populated (cp docker/.env.example docker/.env).
#   2. The production stack must already be running:  make prod-up
#
# BASE_URL is derived from PUBLIC_PORT in docker/.env (defaults to 80).
# The global-setup seeds test users idempotently — safe to re-run.
test-e2e-prod: _check-env
	@echo ">> Running E2E tests against the production stack (MinIO + Redis)..."
	@PUBLIC_PORT=$$(grep '^PUBLIC_PORT=' $(PROD_ENV) | cut -d= -f2 | head -1); \
	PUBLIC_PORT=$${PUBLIC_PORT:-80}; \
	echo ">>   BASE_URL=http://localhost:$$PUBLIC_PORT"; \
	cd e2e && npm ci && BASE_URL=http://localhost:$$PUBLIC_PORT npx playwright test

# ── Misc ──────────────────────────────────────────────────────────────────────
status:
	@echo "── Containers ──────────────────────────────────────────"
	@$(DOCKER) ps --filter name=$(PREFIX) --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
	@echo "── Active slot ─────────────────────────────────────────"
	@if [ -f $(ACTIVE_FILE) ]; then cat $(ACTIVE_FILE); else echo "(none yet)"; fi

logs:
	@SLOT=$$(cat $(ACTIVE_FILE) 2>/dev/null || echo blue); \
	echo ">> Tailing logs for $$SLOT..."; \
	$(DOCKER) logs -f $(PREFIX)_$$SLOT

clean:
	@echo ">> Cleaning build artifacts..."
	rm -rf $(PUBLISH_DIR)
	rm -rf backend/Omnijoy.Api/wwwroot
	find . -name "bin" -o -name "obj" | grep -v node_modules | xargs rm -rf
	@echo ">> Done."

# ── Production stack ──────────────────────────────────────────────────────────
# Ensure docker/.env exists; warn and exit if missing.
_check-env:
	@if [ ! -f "$(PROD_ENV)" ]; then \
	  echo ""; \
	  echo "  ERROR: $(PROD_ENV) not found."; \
	  echo "  Copy the example and fill in your secrets:"; \
	  echo "    cp $(PROD_ENV_EX) $(PROD_ENV)"; \
	  echo "    \$$EDITOR $(PROD_ENV)"; \
	  echo ""; \
	  exit 1; \
	fi

# Capture the short git hash once so all prod targets use the same value.
# Falls back to "dev" if git is unavailable (CI without the repo).
GIT_HASH ?= $(shell git rev-parse --short HEAD 2>/dev/null || echo dev)

# Build the Docker image (runs frontend + backend compilation inside Docker).
prod-build: _check-env
	@echo ">> Building production image (GIT_HASH=$(GIT_HASH))..."
	GIT_HASH=$(GIT_HASH) $(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) build --pull

# Build image + start all services. This is the main 'git pull → make' target.
#
# Three-step deploy (works with both docker compose and podman-compose):
#   1. Build the backend image (multi-stage: Vite + dotnet publish inside Docker).
#   2. Ensure all services are running — data services (mysql/redis/minio/mediamtx)
#      are left untouched if already healthy; new services are created.
#   3. Force-recreate backend + nginx so the freshly built image is actually
#      running.  This explicit step is necessary because podman-compose does NOT
#      automatically recreate containers when their image is rebuilt (unlike
#      plain docker compose up --build).
#
# Migrations are applied automatically at backend startup (Program.cs).
# 'make prod-migrate' is only needed for the very first deploy or to run
# migrations manually outside of normal startup.
prod-up: _check-env
	@echo ">> Building backend image (GIT_HASH=$(GIT_HASH))..."
	GIT_HASH=$(GIT_HASH) $(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) build backend
	@echo ">> Ensuring all services are running..."
	GIT_HASH=$(GIT_HASH) $(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) up -d --remove-orphans
	@echo ">> Applying new image to backend + nginx..."
	GIT_HASH=$(GIT_HASH) $(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) up -d --no-deps --force-recreate backend nginx
	@echo ""
	@echo "  Deploy complete. DB migrations applied automatically on startup."
	@echo "  Public port: $$(grep PUBLIC_PORT $(PROD_ENV) | cut -d= -f2 || echo 80)"

# Start without rebuilding (e.g. after prod-down).
prod-start: _check-env
	$(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) up -d --remove-orphans

# Stop containers but keep them (faster restart with prod-start).
prod-stop: _check-env
	$(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) stop

# Stop and remove containers (volumes are preserved).
prod-down: _check-env
	$(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) down

# Rebuild backend image and force-recreate just the backend + nginx services.
# Fastest deploy path — skips data services entirely.
prod-restart: _check-env
	@echo ">> Rebuilding backend image (GIT_HASH=$(GIT_HASH))..."
	GIT_HASH=$(GIT_HASH) $(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) build --pull backend
	@echo ">> Force-recreating backend + nginx with new image..."
	GIT_HASH=$(GIT_HASH) $(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) up -d --no-deps --force-recreate backend nginx

# Run EF Core migrations via a temporary SDK container joined to the Docker network.
# This avoids having to publish the DB port to the host.
#
# Notes:
#  - Uses 'dotnet-ef' v9.x (latest Pomelo-compatible version); installed at runtime
#    because the mcr.microsoft.com/dotnet/sdk image does not ship it.
#  - Volume mount uses ':z' (lowercase) so SELinux / Podman rootless environments
#    can relabel the bind-mount without needing exclusive ownership.
#  - OMNIJOY_CONN is the env var read by DesignTimeDbContextFactory.
#  - grep patterns are anchored (^VAR=) to avoid matching ROOT_PASSWORD with MYSQL_PASSWORD.
prod-migrate: _check-env
	@echo ">> Running EF Core migrations (via temporary SDK container)..."
	@DB_PASS=$$(grep "^MYSQL_PASSWORD=" $(PROD_ENV) | cut -d= -f2 | head -1); \
	DB_USER=$$(grep "^MYSQL_USER=" $(PROD_ENV) | cut -d= -f2 | head -1); \
	DB_NAME=$$(grep "^MYSQL_DATABASE=" $(PROD_ENV) | cut -d= -f2 | head -1); \
	$(DOCKER) run --rm \
	  --network 07ad0b82_omnijoy_net \
	  -v "$(REPO_PATH)/backend:/src:z" \
	  -w /src \
	  -e "OMNIJOY_CONN=Server=07ad0b82_omnijoy_mysql;Port=3306;Database=$${DB_NAME:-omnijoy};User=$${DB_USER:-omnijoy};Password=$$DB_PASS;AllowPublicKeyRetrieval=true;" \
	  mcr.microsoft.com/dotnet/sdk:10.0 \
	  sh -c "dotnet tool install --global dotnet-ef --version '9.*' 2>&1 | tail -1 \
	    && export PATH=\$$PATH:/root/.dotnet/tools \
	    && dotnet ef database update \
	         --project Omnijoy.Infrastructure \
	         --startup-project Omnijoy.Api"
	@echo ">> Migrations complete."

# Tail logs. Use SVC=backend (or mysql/nginx) to filter; defaults to all.
prod-logs: _check-env
	$(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) logs -f $(SVC)

# Show production container status.
prod-status:
	@echo "── Production containers ────────────────────────────────"
	@$(DOCKER) ps --filter name=$(PREFIX) \
	  --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# Open an interactive shell inside the running backend container.
prod-shell:
	$(DOCKER) exec -it 07ad0b82_omnijoy_backend /bin/bash

# nginx.prod.conf.template is processed by envsubst at container start and
# written to /etc/nginx/nginx.conf inside the container. This target signals
# nginx to re-read the generated /etc/nginx/nginx.conf without dropping
# active connections (graceful reload). To pick up template changes, recreate
# the container first: make prod-up  (which force-recreates nginx).
prod-nginx-reload:
	@echo ">> Reloading nginx config..."
	$(DOCKER) exec $(PREFIX)_nginx nginx -t
	$(DOCKER) exec $(PREFIX)_nginx nginx -s reload
	@echo ">> nginx reloaded."

# Force-recreate MinIO + the one-shot minio-init container so that a changed
# MINIO_ROOT_USER / MINIO_ROOT_PASSWORD in docker/.env is picked up.
#
# 'make prod-up' does NOT recreate data services (mysql / minio / redis /
# mediamtx) to protect persistent state.  This means that after a credential
# rotation the backend gets the new values but MinIO still runs with the old
# ones, causing every PutObject to fail with an opaque AccessDenied error.
#
# Workflow:
#   1. Edit docker/.env (change MINIO_ROOT_USER / MINIO_ROOT_PASSWORD).
#   2. make prod-rotate-minio   ← restarts MinIO with new creds + re-runs init.
#   3. make prod-up             ← restarts backend so it uses the new creds.
prod-rotate-minio: _check-env
	@echo ">> Force-recreating MinIO with credentials from $(PROD_ENV)..."
	$(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) \
	  up -d --no-deps --force-recreate minio minio-init
	@echo ""
	@echo "  MinIO restarted.  Now run 'make prod-up' to recreate the backend"
	@echo "  with the new credentials."

# Install a systemd user service so the stack starts automatically on boot/login.
# After running this, also run:  loginctl enable-linger $$USER
prod-install:
	@echo ">> Installing systemd user service..."
	@mkdir -p "$$HOME/.config/systemd/user"
	@sed "s|REPO_PATH_PLACEHOLDER|$(REPO_PATH)|g" \
	  docker/omnijoy.service \
	  > "$$HOME/.config/systemd/user/omnijoy.service"
	systemctl --user daemon-reload
	systemctl --user enable omnijoy.service
	@echo ""
	@echo "  Service installed. To start it now:"
	@echo "    systemctl --user start omnijoy.service"
	@echo ""
	@echo "  To keep it running after logout (recommended on servers):"
	@echo "    loginctl enable-linger $$USER"

# Start a temporary Prometheus container scraping the Omnijoy backend.
# Requires the prod stack to be running (make prod-up).
# Prometheus UI: http://localhost:9090
# The container is auto-removed when stopped (--rm).
prod-metrics:
	@echo '>> Starting Prometheus scraping Omnijoy backend...'
	$(DOCKER) run -d --rm \
	  --name 07ad0b82_omnijoy_prometheus \
	  --network 07ad0b82_omnijoy_net \
	  -p 9090:9090 \
	  -v $(REPO_PATH)/docker/prometheus.example.yml:/etc/prometheus/prometheus.yml:ro \
	  prom/prometheus
	@echo '>> Prometheus running on http://localhost:9090'

# Remove the systemd user service.
prod-uninstall:
	@echo ">> Removing systemd user service..."
	-systemctl --user stop    omnijoy.service 2>/dev/null
	-systemctl --user disable omnijoy.service 2>/dev/null
	@rm -f "$$HOME/.config/systemd/user/omnijoy.service"
	systemctl --user daemon-reload
	@echo ">> Done."
