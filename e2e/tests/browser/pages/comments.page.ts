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

  /** Returns the comment input for a post card. */
  commentInput(postCard: Locator): Locator {
    return postCard.locator('[data-testid="comment-input"]')
  }

  /** Returns the submit button for a comment form. */
  commentSubmitButton(postCard: Locator): Locator {
    return postCard.locator('[data-testid="comment-submit"]')
  }

  /** Returns all rendered comment items inside a post card. */
  commentItems(postCard: Locator): Locator {
    return postCard.locator('[data-testid="comment-item"]')
  }

  /** First post card on the page. */
  get firstPostCard(): Locator {
    return this.page.locator('[data-testid="post-card"]').first()
  }
}
