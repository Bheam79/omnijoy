// ── User & Auth ──────────────────────────────────────────────────────────────

export type Gender = 'Male' | 'Female' | 'NotDisclosed'

export interface User {
  id: string
  email: string
  displayName: string
  avatarUrl?: string
  coverUrl?: string
  bio?: string
  gender: Gender
  birthDate?: string
  showBirthDate: boolean
  createdAt: string
}

export interface AuthTokens {
  accessToken: string
  refreshToken: string
  expiresIn: number
}

// ── Posts ────────────────────────────────────────────────────────────────────

export type PostType = 'Text' | 'Image' | 'Video' | 'TextOnBackground'
export type Privacy = 'Public' | 'Friends' | 'OnlyMe'

export interface PostMedia {
  id: string
  mediaType: 'Image' | 'Video'
  url: string
  thumbnailUrl?: string
  order: number
}

export interface Post {
  id: string
  author: User
  companyPageId?: string
  content: string
  backgroundImageUrl?: string
  postType: PostType
  privacy: Privacy
  media: PostMedia[]
  createdAt: string
  updatedAt: string
}

// ── Friends ──────────────────────────────────────────────────────────────────

export type FriendStatus = 'Pending' | 'Accepted' | 'Declined' | 'Blocked'
export type RelationType = 'Parent' | 'Sibling' | 'Child'

export interface Friend {
  id: string
  user: User
  status: FriendStatus
  relationType?: RelationType
  relationSubtype?: string
  createdAt: string
}

// ── Messages & Conversations ─────────────────────────────────────────────────

export type MessageType = 'Text' | 'Image' | 'Video' | 'File'
export type ConversationType = 'Direct' | 'Group'

/** Minimal user info returned in chat responses (subset of User). */
export interface ChatUser {
  id: string
  displayName: string
  avatarUrl?: string
}

export interface MessageMedia {
  id: string
  mediaType: 'Image' | 'Video' | 'File'
  url: string
  fileName?: string
  fileSizeBytes?: number
}

export interface Message {
  id: string
  conversationId: string
  sender: ChatUser
  content?: string
  messageType: MessageType
  media?: MessageMedia
  createdAt: string
  isDeleted: boolean
}

export interface Conversation {
  id: string
  type: ConversationType
  participants: ChatUser[]
  lastMessage?: Message
  unreadCount: number
  createdAt: string
}

// ── Events ───────────────────────────────────────────────────────────────────

export type RsvpStatus = 'Going' | 'Maybe' | 'NotGoing'

export interface Event {
  id: string
  creator: User
  companyPageId?: string
  title: string
  description?: string
  startAt: string
  endAt?: string
  location?: string
  coverImageUrl?: string
  privacy: Privacy
  rsvp?: RsvpStatus
  attendeeCount: number
  createdAt: string
}

// ── Company Pages ────────────────────────────────────────────────────────────

export type CompanyRole = 'Owner' | 'Admin' | 'Editor'

export interface CompanyPage {
  id: string
  name: string
  description?: string
  logoUrl?: string
  coverUrl?: string
  createdBy: User
  createdAt: string
  isFollowing?: boolean
  myRole?: CompanyRole
}

// ── Live Streams ─────────────────────────────────────────────────────────────

export type StreamStatus = 'Scheduled' | 'Live' | 'Ended'

export interface LiveStream {
  id: string
  host: User
  title: string
  status: StreamStatus
  viewerCount: number
  startedAt?: string
  endedAt?: string
}

// ── Notifications ────────────────────────────────────────────────────────────

/**
 * Strings match the backend NotificationType enum exactly
 * (see backend/Omnijoy.Core/Models/Enums/NotificationType.cs).
 */
export type NotificationType =
  | 'FriendRequest'
  | 'FriendRequestAccepted'
  | 'PostLike'
  | 'PostComment'
  | 'PostShare'
  | 'CommentLike'
  | 'CommentReply'
  | 'EventInvite'
  | 'EventReminder'
  | 'NewMessage'
  | 'NewFollower'
  | 'LiveStreamStarted'
  | 'TaggedInPost'
  | 'FamilyRelationRequest'
  | 'CompanyPageInvite'
  | 'NewPostFromFriend'
  | 'EventCreatedByFriend'
  | 'MentionInPost'
  | 'MentionInComment'
  | 'MessageReceived'

export interface NotificationActor {
  id: string
  displayName: string
  avatarUrl?: string
}

export interface Notification {
  id: string
  type: NotificationType
  referenceId?: string
  isRead: boolean
  createdAt: string
  actor?: NotificationActor
  title?: string
  body?: string
}

export interface UserPresence {
  userId: string
  isOnline: boolean
  lastSeenAt?: string
}

// ── Shared ───────────────────────────────────────────────────────────────────

export interface OgPreview {
  url: string
  title?: string
  description?: string
  imageUrl?: string
  siteName?: string
}

export interface PaginatedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  hasMore: boolean
}
