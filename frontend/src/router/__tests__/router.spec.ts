/**
 * Tests that the router's catch-all slug route never shadows real top-level
 * routes and that the registration order is correct.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

// ── Mock the auth store — configurable per test ────────────────────────────────
//
// The default mock represents an authenticated regular user. Individual tests
// that need different role / auth states call `setMockAuth({...})` before
// `importRouter()` to override the next module load.
const mockAuthState = vi.hoisted(() => ({
  isAuthenticated: true,
  user: { role: 'User' as 'User' | 'Moderator' | 'Admin' },
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuthState,
}))

function setMockAuth(state: {
  isAuthenticated: boolean
  user: { role: 'User' | 'Moderator' | 'Admin' } | null
}) {
  mockAuthState.isAuthenticated = state.isAuthenticated
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  ;(mockAuthState as any).user = state.user
}

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
    // Reset auth mock to default authenticated regular user
    setMockAuth({ isAuthenticated: true, user: { role: 'User' } })
  })

  it('resolves /login to the login route, NOT the slug resolver', async () => {
    const router = await importRouter()
    const resolved = router.resolve('/login')
    expect(resolved.name).toBe('login')
    expect(resolved.name).not.toBe('slug-resolver')
  })

  it('resolves /forgot-password to the forgot-password route, NOT the slug resolver', async () => {
    const router = await importRouter()
    const resolved = router.resolve('/forgot-password')
    expect(resolved.name).toBe('forgot-password')
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

describe('admin route role guard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.resetModules()
  })

  it('redirects regular User away from /admin/reports back to /wall', async () => {
    setMockAuth({ isAuthenticated: true, user: { role: 'User' } })
    const router = await importRouter()

    await router.push('/admin/reports')
    await router.isReady()

    expect(router.currentRoute.value.name).toBe('wall')
  })

  it('allows Moderator into /admin/reports', async () => {
    setMockAuth({ isAuthenticated: true, user: { role: 'Moderator' } })
    const router = await importRouter()

    await router.push('/admin/reports')
    await router.isReady()

    expect(router.currentRoute.value.name).toBe('admin-reports')
  })

  it('allows Admin into /admin/reports', async () => {
    setMockAuth({ isAuthenticated: true, user: { role: 'Admin' } })
    const router = await importRouter()

    await router.push('/admin/reports')
    await router.isReady()

    expect(router.currentRoute.value.name).toBe('admin-reports')
  })

  it('redirects Moderator away from /admin/audit-log (Admin-only)', async () => {
    setMockAuth({ isAuthenticated: true, user: { role: 'Moderator' } })
    const router = await importRouter()

    await router.push('/admin/audit-log')
    await router.isReady()

    expect(router.currentRoute.value.name).toBe('wall')
  })

  it('allows Admin into /admin/audit-log', async () => {
    setMockAuth({ isAuthenticated: true, user: { role: 'Admin' } })
    const router = await importRouter()

    await router.push('/admin/audit-log')
    await router.isReady()

    expect(router.currentRoute.value.name).toBe('admin-audit-log')
  })

  it('redirects unauthenticated user away from /admin to /login', async () => {
    setMockAuth({ isAuthenticated: false, user: null })
    const router = await importRouter()

    await router.push('/admin/reports')
    await router.isReady()

    expect(router.currentRoute.value.name).toBe('login')
  })

  it('resolves bare /admin as the AdminShell that redirects to /admin/reports', async () => {
    setMockAuth({ isAuthenticated: true, user: { role: 'Admin' } })
    const router = await importRouter()

    await router.push('/admin')
    await router.isReady()

    // /admin redirects to /admin/reports
    expect(router.currentRoute.value.name).toBe('admin-reports')
  })
})
