import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the comment thread on a post.
 * Works against the wall page (/wall) where posts and their comments are rendered.
 */
export class CommentsPage {
  readonly page: Page

  constructor(page: Page) {
    this.page = page
  }

  async goto() {
    await this.page.goto('/wall')
    await this.page.waitForLoadState('networkidle')
  }

  /** Returns the comment input for the first post card. */
  commentInput(postCard: Locator): Locator {
    return postCard.locator(
      'textarea[placeholder*="comment" i], input[placeholder*="comment" i], [data-testid="comment-input"]',
    ).first()
  }

  /** Returns the submit button for a comment form. */
  commentSubmitButton(postCard: Locator): Locator {
    return postCard.getByRole('button', { name: /post|comment|submit|send/i }).first()
  }

  /** Returns all rendered comment items inside a post card. */
  commentItems(postCard: Locator): Locator {
    return postCard.locator(
      '[data-testid="comment-item"], .comment-item, li.comment, [class*="comment"]',
    )
  }

  /** First post card on the page. */
  get firstPostCard(): Locator {
    return this.page.locator(
      '[data-testid="post-card"], article, [class*="post-card"]',
    ).first()
  }
}
