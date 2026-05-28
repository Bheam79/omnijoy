# ADR-001 — Media Storage Strategy

**Date:** 2026-05-28  
**Status:** Accepted  
**Deciders:** Omnijoy engineering  

---

## Context

Omnijoy needs to store user-generated binary files: profile avatars, cover images, post
photos/videos, chat attachments, and live-stream HLS segments. The platform runs in Docker
and must be deployable to a VPS without cloud credentials, but should be promotable to
AWS S3 / Cloudflare R2 / Backblaze B2 with a config change and no code changes.

### Current state (as of 2026-05-28)

| Component | File | Notes |
|---|---|---|
| `IMediaStorageService` | `Omnijoy.Core/Interfaces/IMediaStorageService.cs` | Abstraction: `StoreAsync` + `DeleteAsync` |
| `LocalMediaStorageService` | `Omnijoy.Infrastructure/Services/LocalMediaStorageService.cs` | Writes to `wwwroot/uploads/` |
| `S3MediaStorageService` | `Omnijoy.Infrastructure/Services/S3MediaStorageService.cs` | Uses `AWSSDK.S3` (MinIO-compatible) |
| Switch | `Program.cs` | `Storage:Type = "local"` (default) or `"s3"` |
| AWS SDK | `Omnijoy.Infrastructure.csproj` | `AWSSDK.S3` already a dependency |

What is **missing**: MinIO in Docker, image optimization, video thumbnail generation.

---

## Decisions

### 1. Object store: MinIO in Docker (dev + self-hosted prod)

**Chosen:** MinIO, not local filesystem.

**Rationale:**
- MinIO exposes the S3 API. Switching to AWS S3 / Cloudflare R2 is a three-line change to
  `docker/.env` (endpoint URL + credentials). Zero code changes.
- `S3MediaStorageService` and `AWSSDK.S3` are already wired in; only the Docker service
  and env vars are missing.
- Local filesystem has no path to horizontal scaling and couples media to a single
  container's disk.
- MinIO runs on ~256 MB RAM — negligible overhead on a dev laptop or VPS.

**Rejected — local filesystem only:**  
Acceptable for a single-node VPS, but the migration cost later is unnecessary given
MinIO works identically on a laptop and a cloud server.

**MinIO bucket and folder layout:**

```
omnijoy          ← single bucket (configurable via MINIO_BUCKET)
├── avatars/           profile photos                  (256×256 WebP)
├── covers/            profile / event / company banners  (1200×630 WebP)
├── posts/
│   ├── images/        post photo attachments          (≤1920 px wide, WebP)
│   └── videos/        post video clips                (original upload)
├── thumbnails/        video poster frames             (480×270 WebP)
├── chat/              message file attachments        (image / video / document)
└── hls/               live-stream HLS segments        (managed by mediamtx, read-only)
```

All objects are named `{folder}/{uuid}{ext}` (UUID + original extension), so they are
globally unique and safe to cache indefinitely.

---

### 2. Upload flow: direct multipart POST to the API

**Chosen:** Client POSTs `multipart/form-data` to the API; the API streams the bytes
straight to MinIO via `S3MediaStorageService.StoreAsync`. No presigned URLs for now.

**Rationale:**
- Already implemented (`/api/posts`, `/api/users/avatar`, etc.).
- The API can enforce auth, content-type validation, and file-size limits before touching
  the object store.
- Presigned URLs skip the API auth layer and require client-side S3 SDK wiring — worthwhile
  only when uploads are very large (> 1 GB) or extremely high-frequency. Neither applies
  here (max video 200 MB, max image 5 MB).

**Future option (not in scope now):** Add `POST /api/media/presign` returning a
short-lived presigned URL for direct browser-to-MinIO uploads, useful if post videos
grow beyond 500 MB and the API proxy becomes a bottleneck.

---

### 3. Image optimization: resize + WebP on upload

**Chosen:** Synchronous resize + WebP conversion **at upload time** using
**SixLabors.ImageSharp** (pure .NET, no native binary dependencies).

Resize targets:

| Folder | Max dimensions | Format |
|---|---|---|
| `avatars/` | 256 × 256 px (crop to fill) | WebP |
| `covers/` | 1200 × 630 px (fit, pad if needed) | WebP |
| `posts/images/` | 1920 px wide max, aspect preserved | WebP |
| `thumbnails/` | 480 × 270 px (fit) | WebP |

**Rationale:**
- SixLabors.ImageSharp is the de-facto .NET image library; pure managed code, no
  native libjpeg/libpng requirements — no Dockerfile changes needed.
- Resizing on upload is simpler than on-demand (no cache warm-up, no edge CDN needed).
  Resize for a 12 MP phone JPEG takes < 200 ms on modern hardware.
- WebP achieves 25–40 % smaller files than JPEG at equivalent visual quality.

**Rejected — on-demand resize (Imgproxy / Thumbor):**  
Extra container adds operational complexity without clear benefit at this scale.

---

### 4. Video scope: short clips (≤ 2 min); async thumbnail via FFmpeg

**Chosen scope:**
- **Short clips only** (≤ 120 s, ≤ 200 MB — limits already enforced in
  `LocalMediaStorageService`).
- **Poster/thumbnail** generated asynchronously after upload by invoking the `ffmpeg` CLI
  via `System.Diagnostics.Process`. Frame at t=1 s is extracted, resized to 480×270 WebP,
  and stored in `thumbnails/`.  
- **No HLS transcoding** for user-uploaded videos. Videos are served as-is (`.mp4` /
  `.webm`). The browser's `<video>` element handles progressive playback.

**Rationale:**
- HLS transcoding takes seconds to minutes per video and requires significant disk I/O.
  For clips ≤ 2 min, progressive MP4 download is fine on 5+ Mbps connections.
- FFmpeg thumbnail extraction is cheap (< 1 s for any clip length) and unlocks the
  post-card cover image — high UX value, low cost.
- Background jobs are implemented with an in-process `Channel<T>` queue
  (`IBackgroundTaskQueue` + `BackgroundService`). No Redis, RabbitMQ, or Hangfire needed
  at this stage.

**Deferred:** Multi-bitrate HLS transcoding (360p / 720p / 1080p adaptive streams). File
as a separate task when demand for content > 2 min emerges.

---

### 5. CDN / caching headers

**Chosen:**
- Objects are content-addressed (UUID in the key) — once written, they never change.
- Every `StoreAsync` call sets `Cache-Control: public, max-age=31536000, immutable` as
  S3 object metadata, so browsers and CDN nodes cache aggressively.
- nginx: `location /media/` (MinIO proxy pass) injects the same `Cache-Control` header.
- For the local fallback: add `location /uploads/` in nginx conf with the same header.

**Future option:** Put Cloudflare or AWS CloudFront in front of the MinIO/S3 origin to
offload egress bandwidth from the VPS.

---

## Consequences

| Area | Impact |
|---|---|
| Dev setup | `make start-db` / compose also starts `07ad0b82_omnijoy_minio`. One extra container. |
| Env vars | `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, `MINIO_BUCKET` added to `.env.example` |
| Backend config | `Storage:Type=s3`, `Storage:S3:ServiceUrl=http://minio:9000`, etc. in compose |
| Dependencies | `SixLabors.ImageSharp` NuGet package in `Omnijoy.Infrastructure` |
| Dockerfile | `ffmpeg` CLI must be installed in the production image (`apt-get install ffmpeg`) |
| Background jobs | `IBackgroundTaskQueue` + `ThumbnailGeneratorService` hosted service added |

---

## Follow-up implementation tasks

| Task | Type | Description |
|---|---|---|
| OMNIJOY-32 | Code | Add MinIO service to docker-compose (dev + prod), wire `S3MediaStorageService` config, update `.env.example` and nginx for `/media/` proxy + cache headers |
| OMNIJOY-33 | Code | Image optimization pipeline: add `SixLabors.ImageSharp`, create `IImageProcessingService` that resizes + converts to WebP before `StoreAsync`, apply to avatar / cover / post-image upload paths |
| OMNIJOY-34 | Code | Video thumbnail background job: `IBackgroundTaskQueue` + `ThumbnailGeneratorService` (FFmpeg frame extract → WebP → thumbnails/ folder), triggered after video upload, thumbnail URL written back to `PostMedia.ThumbnailUrl` |
