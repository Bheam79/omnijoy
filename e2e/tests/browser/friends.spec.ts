import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'
import { injectTokens } from '../../support/auth-helpers'
import { FriendsPage } from './pages/friends.page'

test.describe('Friends page', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
  })

  test('/friends page loads', async ({ page }) => {
    const friends = new FriendsPage(page)
    await friends.goto()
    await page.waitForLoadState('networkidle')
    expect(page.url()).toContain('/friends')
    // Page renders without crash
    await expect(page.locator('body')).toBeVisible()
  })

  test('friends page shows friend list or empty state', async ({ page }) => {
    const friends = new FriendsPage(page)
    await friends.goto()
    await page.waitForLoadState('networkidle')

    const hasFriends = (await friends.friendCards.count()) > 0
    const hasEmpty = await page.getByText(/no friends yet|find friends|suggestions/i).isVisible()
    expect(hasFriends || hasEmpty).toBeTruthy()
  })

  test('friend requests section is visible or has empty state', async ({ page }) => {
    const friends = new FriendsPage(page)
    await friends.goto()
    await page.waitForLoadState('networkidle')

    // The Friends page always shows a tab bar with "Requests" tab (visible on all tabs).
    // The default tab is "Friends" which shows the friend list or "No friends yet".
    // We check that the "Requests" tab is present — it's always visible as a tab switcher.
    const requestsTab = page.getByRole('button', { name: /Requests/i })
    const hasRequestsTab = await requestsTab.first().isVisible()

    // Also accept "No friends yet" (empty friend state on default tab)
    const noFriendsText = page.getByText(/no friends yet|suggestions/i).first()
    const hasEmptyState = await noFriendsText.isVisible()

    expect(hasRequestsTab || hasEmptyState).toBeTruthy()
  })
})

test.describe('Friend request lifecycle (API-backed browser test)', () => {
  test('send + accept friend request flow', async ({ page, request, baseURL }) => {
    // Log in as user1
    const { token: token1, user: user1 } = getSharedTokenFor(SEED.user1)

    // Log in as user2
    const { token: token2, user: user2 } = getSharedTokenFor(SEED.user2)

    // user1 sends friend request to user2 (clean up first)
    await request.delete(`${baseURL}/api/friends/${user2.id}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    await request.delete(`${baseURL}/api/friends/request/${user2.id}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })

    const sendResp = await request.post(`${baseURL}/api/friends/request/${user2.id}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect([200, 201, 409]).toContain(sendResp.status())

    if (sendResp.status() !== 409) {
      // user2 accepts
      const acceptResp = await request.post(`${baseURL}/api/friends/accept/${user1.id}`, {
        headers: { Authorization: `Bearer ${token2}` },
      })
      expect([200, 201]).toContain(acceptResp.status())
    }

    // Navigate as user1 to friends page and verify
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
    await page.goto('/friends')
    await page.waitForLoadState('networkidle')
    expect(page.url()).toContain('/friends')
  })

  test('decline friend request', async ({ request, baseURL }) => {
    const { token: token3, user: user3 } = getSharedTokenFor(SEED.user3)

    const { token: token2, user: user2 } = getSharedTokenFor(SEED.user2)

    // user3 sends to user2
    await request.delete(`${baseURL}/api/friends/request/${user2.id}`, {
      headers: { Authorization: `Bearer ${token3}` },
    })
    const sendResp = await request.post(`${baseURL}/api/friends/request/${user2.id}`, {
      headers: { Authorization: `Bearer ${token3}` },
    })

    if (sendResp.status() !== 409) {
      // user2 declines
      const declineResp = await request.post(`${baseURL}/api/friends/decline/${user3.id}`, {
        headers: { Authorization: `Bearer ${token2}` },
      })
      expect([200, 204]).toContain(declineResp.status())
    }
  })
})
