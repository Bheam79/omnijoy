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

# Absolute path to this repo (used by prod-install for the systemd unit)
REPO_PATH     := $(shell pwd)

.PHONY: help build build-backend build-frontend \
        start-db stop-db \
        deploy-blue deploy-green switch rollback \
        dev dev-backend dev-frontend \
        test test-backend test-frontend test-e2e test-e2e-api test-e2e-browser \
        migrate clean status logs \
        prod-up prod-start prod-stop prod-down prod-restart \
        prod-build prod-migrate prod-logs prod-status prod-shell \
        prod-install prod-uninstall _check-env

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
	@echo "    make test            Run all tests (backend + frontend)"
	@echo "    make test-backend    Run xUnit tests"
	@echo "    make test-frontend   Run Vitest tests"
	@echo "    make test-e2e        Run all E2E tests (Playwright)"
	@echo "    make test-e2e-api    Run E2E API tests only (no browser)"
	@echo "    make test-e2e-browser Run E2E browser tests only"
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
	@echo "    make prod-install    Install systemd user service (auto-start on boot)"
	@echo "    make prod-uninstall  Remove systemd user service"
	@echo ""
	@echo "  Misc:"
	@echo "    make status          Show running containers and active slot"
	@echo "    make logs            Tail logs from the active slot"
	@echo "    make clean           Remove build artifacts"
	@echo ""

# ── Development ───────────────────────────────────────────────────────────────
dev: start-db
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
	echo ">> Activating $$NEW_SLOT (port $$NEW_PORT)..."; \
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

# Build the Docker image (runs frontend + backend compilation inside Docker).
prod-build: _check-env
	@echo ">> Building production image..."
	$(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) build --pull

# Build image + start all services. This is the main 'git pull → make' target.
prod-up: _check-env
	@echo ">> Starting production stack..."
	$(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) up -d --build --remove-orphans
	@echo ""
	@echo "  Stack is up. Run 'make prod-migrate' if this is a first-time or schema-change deploy."
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

# Rebuild backend image and restart just the backend + nginx services.
prod-restart: _check-env
	@echo ">> Rebuilding backend image..."
	$(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) build --pull backend
	@echo ">> Restarting backend + nginx..."
	$(COMPOSE) -f $(PROD_COMPOSE) --env-file $(PROD_ENV) up -d --no-deps backend nginx

# Run EF Core migrations via a temporary SDK container joined to the Docker network.
# This avoids having to publish the DB port to the host.
prod-migrate: _check-env
	@echo ">> Running EF Core migrations (via temporary SDK container)..."
	@DB_PASS=$$(grep MYSQL_PASSWORD $(PROD_ENV) | cut -d= -f2); \
	DB_USER=$$(grep MYSQL_USER $(PROD_ENV) | grep -v ROOT | cut -d= -f2 | head -1); \
	DB_NAME=$$(grep MYSQL_DATABASE $(PROD_ENV) | cut -d= -f2); \
	$(DOCKER) run --rm \
	  --network 07ad0b82_omnijoy_net \
	  -v "$(REPO_PATH)/backend:/src" \
	  -w /src \
	  -e "ConnectionStrings__DefaultConnection=Server=07ad0b82_omnijoy_mysql;Port=3306;Database=$${DB_NAME:-omnijoy};User=$${DB_USER:-omnijoy};Password=$$DB_PASS;AllowPublicKeyRetrieval=true;" \
	  mcr.microsoft.com/dotnet/sdk:10.0 \
	  dotnet ef database update \
	    --project Omnijoy.Infrastructure \
	    --startup-project Omnijoy.Api
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

# Remove the systemd user service.
prod-uninstall:
	@echo ">> Removing systemd user service..."
	-systemctl --user stop    omnijoy.service 2>/dev/null
	-systemctl --user disable omnijoy.service 2>/dev/null
	@rm -f "$$HOME/.config/systemd/user/omnijoy.service"
	systemctl --user daemon-reload
	@echo ">> Done."
