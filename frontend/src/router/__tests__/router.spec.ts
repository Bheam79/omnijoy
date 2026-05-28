/**
 * Tests that the router's catch-all slug route never shadows real top-level
 * routes and that the registration order is correct.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

// ── Mock the auth store so the router guard doesn't crash in tests ─────────────
vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({
    isAuthenticated: true,
  }),
}))

// ── Import the actual app router (after mock so guard runs fine) ───────────────
// We use a memory history so we can push routes synchronously.
async function importRouter() {
  // Each call gets a fresh module instance via resetModules in beforeEach
  const mod = await import('@/router/index')
  return mod.default
}

describe('router route registration order', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    // Bust the module cache so we get a fresh router instance per test
    vi.resetModules()
  })

  it('resolves /login to the login route, NOT the slug resolver', async () => {
    const router = await importRouter()
    const resolved = router.resolve('/login')
    expect(resolved.name).toBe('login')
    expect(resolved.name).not.toBe('slug-resolver')
  })

  it('resolves /wall to the wall route, NOT the slug resolver', async () => {
    const router = await importRouter()
    const resolved = router.resolve('/wall')
    expect(resolved.name).toBe('wall')
  })

  it('resolves /settings to the settings route, NOT the slug resolver', async () => {
    const router = await importRouter()
    const resolved = router.resolve('/settings')
    expect(resolved.name).toBe('settings')
  })

  it('resolves /profile/:userId to the profile route, NOT the slug resolver', async () => {
    const router = await importRouter()
    const resolved = router.resolve('/profile/some-user-id')
    expect(resolved.name).toBe('profile')
  })

  it('resolves /friends to the friends route, NOT the slug resolver', async () => {
    const router = await importRouter()
    const resolved = router.resolve('/friends')
    expect(resolved.name).toBe('friends')
  })

  it('resolves /notifications to the notifications route, NOT the slug resolver', async () => {
    const router = await importRouter()
    const resolved = router.resolve('/notifications')
    expect(resolved.name).toBe('notifications')
  })

  it('resolves /search to the search route, NOT the slug resolver', async () => {
    const router = await importRouter()
    const resolved = router.resolve('/search')
    expect(resolved.name).toBe('search')
  })

  it('resolves a valid vanity slug (e.g. /johndoe) to the slug-resolver route', async () => {
    const router = await importRouter()
    const resolved = router.resolve('/johndoe')
    expect(resolved.name).toBe('slug-resolver')
    expect(resolved.params.slug).toBe('johndoe')
  })

  it('resolves a vanity slug with underscores and digits to slug-resolver', async () => {
    const router = await importRouter()
    const resolved = router.resolve('/alice_smith2')
    expect(resolved.name).toBe('slug-resolver')
  })

  it('does NOT match a multi-segment path against slug-resolver (falls through to 404)', async () => {
    const router = await importRouter()
    const resolved = router.resolve('/some/deep/path')
    expect(resolved.name).toBe('not-found')
  })
})
