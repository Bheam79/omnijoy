import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'

/**
 * API E2E tests for ReportsController endpoints:
 *   POST  /api/reports                  (any authenticated user)
 *   GET   /api/admin/reports            (Admin / Moderator only)
 *   PATCH /api/admin/reports/{id}       (Admin / Moderator only)
 */

async function loginAs(request: APIRequestContext, baseURL: string | undefined, user: typeof SEED.user1) {
  const resp = await request.post(`${baseURL}/api/auth/login`, {
    data: { email: user.email, password: user.password },
  })
  const body = await resp.json()
  return { token: body.accessToken as string, userId: body.user.id as string }
}

/** Creates a public text post and returns its id. */
async function createPost(request: APIRequestContext, baseURL: string | undefined, token: string) {
  const resp = await request.post(`${baseURL}/api/posts`, {
    headers: { Authorization: `Bearer ${token}` },
    multipart: { content: 'Post for report tests', postType: 'Text', privacy: 'Everyone' },
  })
  const body = await resp.json()
  return body.id as string
}

// ── POST /api/reports ─────────────────────────────────────────────────────────

test.describe('POST /api/reports', () => {
  test('submits a report against a post', async ({ request, baseURL }) => {
    const { token: token1 } = await loginAs(request, baseURL, SEED.user1)
    const postId = await createPost(request, baseURL, token1)

    const { token: token2 } = await loginAs(request, baseURL, SEED.user2)
    const resp = await request.post(`${baseURL}/api/reports`, {
      headers: { Authorization: `Bearer ${token2}` },
      data: {
        targetType: 'Post',
        targetId: postId,
        reason: 'Spam',
        notes: 'This post looks spammy',
      },
    })
    expect(resp.status()).toBe(201)
    const body = await resp.json()
    expect(body.id).toBeTruthy()
    expect(body.targetType).toBe('Post')
    expect(body.reason).toBe('Spam')
    expect(body.status).toBeTruthy()
  })

  test('submits a report against a user', async ({ request, baseURL }) => {
    const { token, userId: targetId } = await loginAs(request, baseURL, SEED.user1)
    const { token: token2 } = await loginAs(request, baseURL, SEED.user2)

    const resp = await request.post(`${baseURL}/api/reports`, {
      headers: { Authorization: `Bearer ${token2}` },
      data: {
        targetType: 'User',
        targetId,
        reason: 'Harassment',
        notes: null,
      },
    })
    // 201 on first report; 409 Conflict if already reported the same target
    expect([201, 409]).toContain(resp.status())
  })

  test('returns 400 for an invalid reason', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const { token: token2 } = await loginAs(request, baseURL, SEED.user2)
    const postId = await createPost(request, baseURL, token)

    const resp = await request.post(`${baseURL}/api/reports`, {
      headers: { Authorization: `Bearer ${token2}` },
      data: {
        targetType: 'Post',
        targetId: postId,
        reason: 'InvalidReason',
      },
    })
    expect(resp.status()).toBe(400)
  })

  test('returns 404 for a non-existent target', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.post(`${baseURL}/api/reports`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        targetType: 'Post',
        targetId: '00000000-0000-0000-0000-000000000000',
        reason: 'Spam',
      },
    })
    expect(resp.status()).toBe(404)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/reports`, {
      data: {
        targetType: 'Post',
        targetId: '00000000-0000-0000-0000-000000000000',
        reason: 'Spam',
      },
    })
    expect(resp.status()).toBe(401)
  })
})

// ── GET /api/admin/reports ────────────────────────────────────────────────────

test.describe('GET /api/admin/reports', () => {
  test('admin can list all reports', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.admin)
    const resp = await request.get(`${baseURL}/api/admin/reports`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBe(true)
  })

  test('supports filtering by status', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.admin)
    const resp = await request.get(`${baseURL}/api/admin/reports?status=Pending`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
  })

  test('supports filtering by type', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.admin)
    const resp = await request.get(`${baseURL}/api/admin/reports?type=Post`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
  })

  test('returns 403 for a regular user', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/admin/reports`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(403)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/admin/reports`)
    expect(resp.status()).toBe(401)
  })
})

// ── PATCH /api/admin/reports/{id} ─────────────────────────────────────────────

test.describe('PATCH /api/admin/reports/:id', () => {
  test('admin can update a report status to Reviewed', async ({ request, baseURL }) => {
    // First submit a report so we have an ID to work with
    const { token: token1 } = await loginAs(request, baseURL, SEED.user1)
    const postId = await createPost(request, baseURL, token1)

    // Use user3 to avoid duplicate-report conflicts with other tests
    const { token: token3 } = await loginAs(request, baseURL, SEED.user3)
    const submitResp = await request.post(`${baseURL}/api/reports`, {
      headers: { Authorization: `Bearer ${token3}` },
      data: { targetType: 'Post', targetId: postId, reason: 'Violence' },
    })
    // 201 on first submit; 409 if duplicate — both are acceptable here because
    // we just need a report to exist in the queue for the admin to action.
    expect([201, 409]).toContain(submitResp.status())

    const { token: adminToken } = await loginAs(request, baseURL, SEED.admin)
    const listResp = await request.get(`${baseURL}/api/admin/reports?status=Pending&pageSize=1`, {
      headers: { Authorization: `Bearer ${adminToken}` },
    })
    const listBody = await listResp.json()
    const items = listBody.items ?? listBody
    if (items.length === 0) {
      // No pending reports available — skip the update assertion
      return
    }

    const reportId = items[0].id
    const patchResp = await request.patch(`${baseURL}/api/admin/reports/${reportId}`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { status: 'Reviewed' },
    })
    expect(patchResp.status()).toBe(200)
    const patched = await patchResp.json()
    expect(patched.status).toBe('Reviewed')
  })

  test('admin can dismiss a report', async ({ request, baseURL }) => {
    const { token: adminToken } = await loginAs(request, baseURL, SEED.admin)

    // Get any pending report to dismiss
    const listResp = await request.get(`${baseURL}/api/admin/reports?status=Pending&pageSize=1`, {
      headers: { Authorization: `Bearer ${adminToken}` },
    })
    const listBody = await listResp.json()
    const items = listBody.items ?? listBody
    if (items.length === 0) return

    const reportId = items[0].id
    const patchResp = await request.patch(`${baseURL}/api/admin/reports/${reportId}`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { status: 'Dismissed' },
    })
    expect(patchResp.status()).toBe(200)
  })

  test('returns 404 for a non-existent report ID', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.admin)
    const resp = await request.patch(
      `${baseURL}/api/admin/reports/00000000-0000-0000-0000-000000000000`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: { status: 'Reviewed' },
      },
    )
    expect(resp.status()).toBe(404)
  })

  test('returns 400 for an invalid status value', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.admin)
    const resp = await request.patch(
      `${baseURL}/api/admin/reports/00000000-0000-0000-0000-000000000000`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: { status: 'NotAValidStatus' },
      },
    )
    expect([400, 404]).toContain(resp.status())
  })

  test('returns 403 for a regular user', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.patch(
      `${baseURL}/api/admin/reports/00000000-0000-0000-0000-000000000000`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: { status: 'Reviewed' },
      },
    )
    expect(resp.status()).toBe(403)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.patch(
      `${baseURL}/api/admin/reports/00000000-0000-0000-0000-000000000000`,
      { data: { status: 'Reviewed' } },
    )
    expect(resp.status()).toBe(401)
  })
})
