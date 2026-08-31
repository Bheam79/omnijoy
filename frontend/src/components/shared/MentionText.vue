<script setup lang="ts">
import { computed } from 'vue'
import { useProfileUrl } from '@/composables/useProfileUrl'
import { splitMentionText } from '@/composables/useMentionSegments'
import type { MentionDto } from '@/types/mentions'

const props = withDefaults(defineProps<{
  content: string
  mentions?: MentionDto[] | null
}>(), {
  mentions: () => [],
})

const segments = computed(() => splitMentionText(props.content, props.mentions ?? []))
</script>

<template>
  <span data-testid="mention-text"><template
    v-for="(segment, index) in segments"
    :key="index"
  ><RouterLink
    v-if="segment.type === 'mention'"
    :to="useProfileUrl({ id: segment.mention.userId, urlSlug: segment.mention.urlSlug })"
    class="font-semibold text-blue-400 hover:underline"
    :title="segment.mention.displayName"
    data-testid="resolved-mention"
  >{{ segment.text }}</RouterLink><template v-else>{{ segment.text }}</template></template></span>
</template>
