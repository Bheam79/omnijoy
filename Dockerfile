# ─────────────────────────────────────────────────────────────────────────────
# Omnijoy — multi-stage production image
#
# Build context: repo root
#   docker build -t omnijoy .
#
# Normally started via:  make prod-up
# which calls docker compose -f docker/docker-compose.prod.yml
# ─────────────────────────────────────────────────────────────────────────────

# ── Stage 1: Vue / Vite frontend ─────────────────────────────────────────────
FROM node:22-alpine AS frontend-build
WORKDIR /src

# Install dependencies first for better layer caching
COPY frontend/package*.json ./
RUN npm ci --prefer-offline

COPY frontend/ ./

# Build into /dist (overrides vite.config.ts outDir which is a local-dev path)
RUN npx vite build --outDir /dist --emptyOutDir


# ── Stage 2: .NET backend publish ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src

# Optional: git commit hash injected at build time so GET /api/version returns
# a meaningful identifier that clients can use to detect deploys.
# Usage:  docker build --build-arg GIT_HASH=$(git rev-parse --short HEAD) .
# When omitted the SDK fills AssemblyInformationalVersion with the package
# version from the .csproj (e.g. "1.0.0"), which is still a stable value
# the client can use to detect a version change after a redeploy.
ARG GIT_HASH=""

COPY backend/ ./

# Restore separately for layer caching (only re-runs when .csproj files change)
RUN dotnet restore Omnijoy.Api/Omnijoy.Api.csproj

# Inject built frontend so it ends up in the publish output's wwwroot
COPY --from=frontend-build /dist Omnijoy.Api/wwwroot/

RUN if [ -n "$GIT_HASH" ]; then \
      dotnet publish Omnijoy.Api/Omnijoy.Api.csproj \
        --configuration Release \
        --output /app/publish \
        --no-self-contained \
        --no-restore \
        /p:InformationalVersion="$GIT_HASH"; \
    else \
      dotnet publish Omnijoy.Api/Omnijoy.Api.csproj \
        --configuration Release \
        --output /app/publish \
        --no-self-contained \
        --no-restore; \
    fi


# ── Stage 3: ASP.NET Core runtime ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# FFmpeg is required for async video thumbnail extraction (ThumbnailGeneratorService).
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /var/omnijoy/media

COPY --from=backend-build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

ENTRYPOINT ["dotnet", "Omnijoy.Api.dll"]
