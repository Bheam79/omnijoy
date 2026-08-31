import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'
import { injectTokens } from '../../support/auth-helpers'

test.describe('Mention rendering', () => {
  test('resolved post mentions are profile links while unknown handles remain text', async ({
    page,
    request,
    baseURL,
  }) => {
    const alice = getSharedTokenFor(SEED.user1)
    const actor = getSharedTokenFor(SEED.user2)
    const authHeader = { Authorization: `Bearer ${actor.token}` }

    const slugResponse = await request.put(`${baseURL}/api/users/me/slug`, {
      headers: { Authorization: `Bearer ${alice.token}` },
      data: { slug: 'alice' },
    })
    expect(slugResponse.status()).toBe(200)

    const marker = `Browser mention ${Date.now()}`
    const unknownHandle = `unknown-${Date.now().toString(36)}`
    const postResponse = await request.post(`${baseURL}/api/posts`, {
      headers: authHeader,
      multipart: {
        content: `${marker}: @alice and @${unknownHandle}`,
        postType: 'Text',
        privacy: 'Everyone',
      },
    })
    expect(postResponse.status()).toBe(201)

    await injectTokens(page, baseURL!, SEED.user2.email, SEED.user2.password)
    await page.goto('/wall')

    const postCard = page.getByTestId('post-card').filter({ hasText: marker })
    await expect(postCard).toBeVisible({ timeout: 10_000 })
    const resolvedMention = postCard.getByTestId('resolved-mention').filter({ hasText: '@alice' })
    await expect(resolvedMention).toHaveAttribute('href', '/alice')
    await expect(postCard.getByTestId('mention-text')).toContainText(`@${unknownHandle}`)
    await expect(postCard.locator('a').filter({ hasText: `@${unknownHandle}` })).toHaveCount(0)
  })
})
