/**
 * Presence store — tracks which users are online.
 *
 * The notifications hub broadcasts UserOnline / UserOffline events from a
 * user's friends, which the notifications store forwards to setOnline /
 * setOffline here. For users we haven't received a live event for, call
 * ensurePresence(userIds) to batch-fetch via /api/users/presence.
 */
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { notificationService } from '@/services/notificationService'

interface PresenceEntry {
  online: boolean
  lastSeenAt?: string
}

export const usePresenceStore = defineStore('presence', () => {
  /** userId → PresenceEntry. Missing key = unknown (treat as offline). */
  const presence = ref<Record<string, PresenceEntry>>({})

  /**
   * Track which user IDs we've already requested from the server so concurrent
   * calls to ensurePresence don't fire duplicate requests.
   */
  const pending = new Set<string>()

  // ── Getters ─────────────────────────────────────────────────────────────────

  function isOnline(userId: string): boolean {
    return presence.value[userId]?.online === true
  }

  function lastSeen(userId: string): string | undefined {
    return presence.value[userId]?.lastSeenAt
  }

  const onlineCount = computed(
    () => Object.values(presence.value).filter((p) => p.online).length,
  )

  // ── Mutations (called from SignalR handlers) ───────────────────────────────

  function setOnline(userId: string) {
    presence.value[userId] = { online: true }
  }

  function setOffline(userId: string, at?: string) {
    presence.value[userId] = { online: false, lastSeenAt: at ?? new Date().toISOString() }
  }

  // ── Batch load ──────────────────────────────────────────────────────────────

  /**
   * Make sure we have presence info for the given user IDs.
   * IDs we already know are skipped; the rest are fetched in a single request.
   */
  async function ensurePresence(userIds: string[]): Promise<void> {
    const unknown = userIds.filter(
      (id) => !(id in presence.value) && !pending.has(id),
    )
    if (unknown.length === 0) return

    unknown.forEach((id) => pending.add(id))
    try {
      const result = await notificationService.getPresence(unknown)
      for (const p of result) {
        presence.value[p.userId] = {
          online: p.isOnline,
          lastSeenAt: p.lastSeenAt,
        }
      }
    } catch {
      // Treat unknown IDs as offline on error so UI doesn't get stuck.
      for (const id of unknown) {
        if (!(id in presence.value)) presence.value[id] = { online: false }
      }
    } finally {
      unknown.forEach((id) => pending.delete(id))
    }
  }

  function reset() {
    presence.value = {}
    pending.clear()
  }

  return {
    presence,
    onlineCount,
    isOnline,
    lastSeen,
    setOnline,
    setOffline,
    ensurePresence,
    reset,
  }
})
