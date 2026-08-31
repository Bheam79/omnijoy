/** A mention resolved and persisted by the server for a content field. */
export interface MentionDto {
  /** Normalized slug matched in the original content. */
  matchedSlug: string
  userId: string
  displayName: string
  /** The target user's current vanity slug, which may differ from matchedSlug. */
  urlSlug?: string | null
}
