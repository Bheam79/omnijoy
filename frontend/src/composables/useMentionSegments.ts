import type { MentionDto } from '@/types/mentions'

export type MentionTextSegment =
  | { type: 'text'; text: string }
  | { type: 'mention'; text: string; mention: MentionDto }

const wordCharacter = /[\p{L}\p{N}_]/u
const slugCharacter = /[A-Za-z0-9_-]/

/**
 * Splits content using only mention records supplied by the server. Tokens
 * without persisted metadata deliberately remain part of a plain-text segment.
 */
export function splitMentionText(
  content: string,
  mentions: readonly MentionDto[] = [],
): MentionTextSegment[] {
  if (!content) return []

  const bySlug = new Map<string, MentionDto>()
  for (const mention of mentions) {
    const slug = mention.matchedSlug.toLocaleLowerCase('en-US')
    if (slug && !bySlug.has(slug)) bySlug.set(slug, mention)
  }

  const candidates = [...bySlug.keys()].sort((a, b) => b.length - a.length)
  if (candidates.length === 0) return [{ type: 'text', text: content }]

  const segments: MentionTextSegment[] = []
  let plainStart = 0
  let index = 0

  while (index < content.length) {
    if (content[index] !== '@' || !hasLeadingBoundary(content, index)) {
      index++
      continue
    }

    const matchedSlug = candidates.find((slug) => {
      const end = index + 1 + slug.length
      if (content.slice(index + 1, end).toLocaleLowerCase('en-US') !== slug) return false
      if (end < content.length && (slugCharacter.test(content[end]) || wordCharacter.test(content[end]))) {
        return false
      }
      return true
    })

    if (!matchedSlug) {
      index++
      continue
    }

    if (plainStart < index) {
      segments.push({ type: 'text', text: content.slice(plainStart, index) })
    }

    const end = index + 1 + matchedSlug.length
    segments.push({
      type: 'mention',
      text: content.slice(index, end),
      mention: bySlug.get(matchedSlug)!,
    })
    index = end
    plainStart = end
  }

  if (plainStart < content.length) {
    segments.push({ type: 'text', text: content.slice(plainStart) })
  }

  return segments
}

function hasLeadingBoundary(content: string, atIndex: number): boolean {
  if (atIndex === 0) return true
  const previous = content[atIndex - 1]
  return previous !== '@' && !wordCharacter.test(previous)
}
