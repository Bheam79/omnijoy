<script setup lang="ts">
/**
 * Tiny coloured dot that overlays an avatar to show online status.
 *
 * Usage:
 *   <PresenceDot :user-id="user.id" />
 *   <PresenceDot :user-id="user.id" size="md" :show-offline="true" />
 *
 * Reads from the presence store. If the user is not yet tracked, the dot is
 * hidden (or rendered grey when `showOffline` is true). Call
 * `presenceStore.ensurePresence([...ids])` from a parent component to populate.
 */
import { computed, onMounted } from 'vue'
import { usePresenceStore } from '@/stores/presence'

const props = withDefaults(
  defineProps<{
    userId: string
    size?: 'xs' | 'sm' | 'md' | 'lg'
    showOffline?: boolean
  }>(),
  {
    size: 'sm',
    showOffline: false,
  },
)

const presence = usePresenceStore()

const online = computed(() => presence.isOnline(props.userId))
const tracked = computed(() => props.userId in presence.presence)

const sizeClass = computed(
  () =>
    ({
      xs: 'h-2 w-2 ring-[1.5px]',
      sm: 'h-2.5 w-2.5 ring-2',
      md: 'h-3 w-3 ring-2',
      lg: 'h-3.5 w-3.5 ring-2',
    })[props.size],
)

// Lazy ensure: when a PresenceDot mounts without parent-batched fetch,
// fall back to a single-user query so the dot eventually resolves.
onMounted(() => {
  if (!tracked.value) {
    presence.ensurePresence([props.userId]).catch(() => {})
  }
})
</script>

<template>
  <span
    v-if="online || showOffline"
    :class="[
      'inline-block rounded-full ring-slate-900',
      sizeClass,
      online ? 'bg-emerald-500' : 'bg-slate-600',
    ]"
    :title="online ? 'Online' : 'Offline'"
    :aria-label="online ? 'Online' : 'Offline'"
  />
</template>
