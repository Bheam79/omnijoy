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

COPY backend/ ./

# Restore separately for layer caching (only re-runs when .csproj files change)
RUN dotnet restore Omnijoy.Api/Omnijoy.Api.csproj

# Inject built frontend so it ends up in the publish output's wwwroot
COPY --from=frontend-build /dist Omnijoy.Api/wwwroot/

RUN dotnet publish Omnijoy.Api/Omnijoy.Api.csproj \
      --configuration Release \
      --output /app/publish \
      --no-self-contained \
      --no-restore


# ── Stage 3: ASP.NET Core runtime ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /var/omnijoy/media

COPY --from=backend-build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

ENTRYPOINT ["dotnet", "Omnijoy.Api.dll"]
