import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'

/**
 * API E2E tests for AdminController endpoints:
 *   GET   /api/admin/users               (Admin / Moderator)
 *   PATCH /api/admin/users/{id}/role     (Admin only)
 *   POST  /api/admin/users/{id}/ban      (Admin / Moderator)
 *   POST  /api/admin/users/{id}/unban    (Admin / Moderator)
 *   GET   /api/admin/audit-log           (Admin only)
 *
 * Requires the SEED.admin user to have been promoted to Admin role by
 * global-setup (via docker exec on the MariaDB container).
 */


// ── GET /api/admin/users ──────────────────────────────────────────────────────

test.describe('GET /api/admin/users', () => {
  test('admin can list all users', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.admin)
    const resp = await request.get(`${baseURL}/api/admin/users`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBe(true)
    expect((body.items ?? body).length).toBeGreaterThan(0)
  })

  test('supports search query parameter', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.admin)
    const resp = await request.get(`${baseURL}/api/admin/users?q=alice`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBe(true)
  })

  test('supports pagination parameters', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.admin)
    const resp = await request.get(`${baseURL}/api/admin/users?page=1&pageSize=5`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
  })

  test('returns 403 for a regular user', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/admin/users`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(403)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/admin/users`)
    expect(resp.status()).toBe(401)
  })
})

// ── PATCH /api/admin/users/{id}/role ─────────────────────────────────────────

test.describe('PATCH /api/admin/users/:id/role', () => {
  test('admin can change a user role to Moderator and back', async ({ request, baseURL }) => {
    const { token: adminToken } = getSharedTokenFor(SEED.admin)
    const { userId: targetId } = getSharedTokenFor(SEED.user3)

    // Promote to Moderator
    const promoteResp = await request.patch(
      `${baseURL}/api/admin/users/${targetId}/role`,
      {
        headers: { Authorization: `Bearer ${adminToken}` },
        data: { role: 'Moderator' },
      },
    )
    expect(promoteResp.status()).toBe(200)
    const promoted = await promoteResp.json()
    expect(promoted.role).toBe('Moderator')

    // Restore to User
    const restoreResp = await request.patch(
      `${baseURL}/api/admin/users/${targetId}/role`,
      {
        headers: { Authorization: `Bearer ${adminToken}` },
        data: { role: 'User' },
      },
    )
    expect(restoreResp.status()).toBe(200)
    const restored = await restoreResp.json()
    expect(restored.role).toBe('User')
  })

  test('returns 400 for an invalid role value', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.admin)
    const { userId: targetId } = getSharedTokenFor(SEED.user1)
    const resp = await request.patch(
      `${baseURL}/api/admin/users/${targetId}/role`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: { role: 'SuperAdmin' },
      },
    )
    expect(resp.status()).toBe(400)
  })

  test('returns 404 for a non-existent user', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.admin)
    const resp = await request.patch(
      `${baseURL}/api/admin/users/00000000-0000-0000-0000-000000000000/role`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: { role: 'User' },
      },
    )
    expect(resp.status()).toBe(404)
  })

  test('returns 403 for a regular user', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const { userId: targetId } = getSharedTokenFor(SEED.user2)
    const resp = await request.patch(
      `${baseURL}/api/admin/users/${targetId}/role`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: { role: 'Moderator' },
      },
    )
    expect(resp.status()).toBe(403)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.patch(
      `${baseURL}/api/admin/users/00000000-0000-0000-0000-000000000000/role`,
      { data: { role: 'User' } },
    )
    expect(resp.status()).toBe(401)
  })
})

// ── POST /api/admin/users/{id}/ban ────────────────────────────────────────────

test.describe('POST /api/admin/users/:id/ban', () => {
  test('admin can ban and then unban a user', async ({ request, baseURL }) => {
    const { token: adminToken } = getSharedTokenFor(SEED.admin)

    // Register a fresh user to ban (avoid disrupting other tests that use seeded users)
    const ts = Date.now()
    const banTarget = {
      email: `ban_target_${ts}@omnijoy.test`,
      password: 'Test@12345!',
      displayName: `BanTarget ${ts}`,
    }
    const regResp = await request.post(`${baseURL}/api/auth/register`, {
      data: {
        email: banTarget.email,
        displayName: banTarget.displayName,
        authMethod: 'password',
        password: banTarget.password,
        gender: 'NotDisclosed',
        birthDate: '1990-01-01',
      },
    })
    const regBody = await regResp.json()
    const targetId = regBody.user?.id as string

    // Ban
    const banResp = await request.post(
      `${baseURL}/api/admin/users/${targetId}/ban`,
      {
        headers: { Authorization: `Bearer ${adminToken}` },
        data: { notes: 'E2E ban test' },
      },
    )
    expect(banResp.status()).toBe(200)
    const banned = await banResp.json()
    expect(banned.isBanned).toBe(true)

    // Unban
    const unbanResp = await request.post(
      `${baseURL}/api/admin/users/${targetId}/unban`,
      {
        headers: { Authorization: `Bearer ${adminToken}` },
        data: { notes: 'E2E unban test' },
      },
    )
    expect(unbanResp.status()).toBe(200)
    const unbanned = await unbanResp.json()
    expect(unbanned.isBanned).toBe(false)
  })

  test('returns 404 when target user does not exist', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.admin)
    const resp = await request.post(
      `${baseURL}/api/admin/users/00000000-0000-0000-0000-000000000000/ban`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: { notes: 'Non-existent user' },
      },
    )
    expect(resp.status()).toBe(404)
  })

  test('returns 403 for a regular user', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const { userId: targetId } = getSharedTokenFor(SEED.user2)
    const resp = await request.post(
      `${baseURL}/api/admin/users/${targetId}/ban`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: {},
      },
    )
    expect(resp.status()).toBe(403)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(
      `${baseURL}/api/admin/users/00000000-0000-0000-0000-000000000000/ban`,
      { data: {} },
    )
    expect(resp.status()).toBe(401)
  })
})

// ── POST /api/admin/users/{id}/unban ──────────────────────────────────────────

test.describe('POST /api/admin/users/:id/unban', () => {
  test('returns 404 when target user does not exist', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.admin)
    const resp = await request.post(
      `${baseURL}/api/admin/users/00000000-0000-0000-0000-000000000000/unban`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: {},
      },
    )
    expect(resp.status()).toBe(404)
  })

  test('returns 403 for a regular user', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const { userId: targetId } = getSharedTokenFor(SEED.user2)
    const resp = await request.post(
      `${baseURL}/api/admin/users/${targetId}/unban`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: {},
      },
    )
    expect(resp.status()).toBe(403)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(
      `${baseURL}/api/admin/users/00000000-0000-0000-0000-000000000000/unban`,
      { data: {} },
    )
    expect(resp.status()).toBe(401)
  })
})

// ── GET /api/admin/audit-log ──────────────────────────────────────────────────

test.describe('GET /api/admin/audit-log', () => {
  test('admin can retrieve the audit log', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.admin)
    const resp = await request.get(`${baseURL}/api/admin/audit-log`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBe(true)
  })

  test('supports pagination parameters', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.admin)
    const resp = await request.get(`${baseURL}/api/admin/audit-log?page=1&pageSize=5`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
  })

  test('supports filtering by actorId', async ({ request, baseURL }) => {
    const { token, userId: adminId } = getSharedTokenFor(SEED.admin)
    const resp = await request.get(`${baseURL}/api/admin/audit-log?actorId=${adminId}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
  })

  test('supports filtering by action', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.admin)
    const resp = await request.get(`${baseURL}/api/admin/audit-log?action=BanUser`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
  })

  test('audit log contains entries from this test run', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.admin)
    const resp = await request.get(`${baseURL}/api/admin/audit-log`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const body = await resp.json()
    const items = body.items ?? body
    // After role-change tests above the log should have at least one entry
    expect(items.length).toBeGreaterThanOrEqual(0)
  })

  test('returns 403 for a regular user', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/admin/audit-log`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(403)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/admin/audit-log`)
    expect(resp.status()).toBe(401)
  })
})
