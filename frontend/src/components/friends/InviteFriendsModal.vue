<script setup lang="ts">
import { ref } from 'vue'
import { friendService } from '@/services/friendService'

const emit = defineEmits<{
  close: []
}>()

// ── Invite link ────────────────────────────────────────────────────────────────

const inviteUrl = ref<string | null>(null)
const loadingLink = ref(false)
const linkCopied = ref(false)

async function generateLink() {
  if (inviteUrl.value) {
    copyLink()
    return
  }
  loadingLink.value = true
  try {
    const dto = await friendService.createInviteLink()
    inviteUrl.value = dto.inviteUrl
    copyLink()
  } catch {
    // silently ignore
  } finally {
    loadingLink.value = false
  }
}

function copyLink() {
  if (!inviteUrl.value) return
  navigator.clipboard.writeText(inviteUrl.value).then(() => {
    linkCopied.value = true
    setTimeout(() => (linkCopied.value = false), 2000)
  })
}

// ── Invite by email ────────────────────────────────────────────────────────────

const email = ref('')
const sendingEmail = ref(false)
const emailResult = ref<{ outcome: string; displayName?: string } | null>(null)
const emailError = ref<string | null>(null)

async function sendEmailInvite() {
  const trimmed = email.value.trim()
  if (!trimmed) return

  sendingEmail.value = true
  emailResult.value = null
  emailError.value = null

  try {
    const result = await friendService.inviteByEmail(trimmed)
    emailResult.value = result
    email.value = ''
  } catch (e: any) {
    emailError.value = e?.response?.data?.error ?? 'Something went wrong. Please try again.'
  } finally {
    sendingEmail.value = false
  }
}
</script>

<template>
  <div
    class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
    @click.self="emit('close')"
  >
    <div class="bg-slate-900 border border-slate-700 rounded-2xl shadow-2xl w-full max-w-md overflow-hidden">

      <!-- Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-slate-700">
        <h2 class="text-lg font-bold text-slate-100">Invite Friends</h2>
        <button
          class="text-gray-500 hover:text-gray-300 transition text-xl leading-none"
          aria-label="Close"
          @click="emit('close')"
        >
          ×
        </button>
      </div>

      <div class="p-6 space-y-6">

        <!-- ── Invite by link ──────────────────────────────────────────────── -->
        <section>
          <h3 class="text-sm font-semibold text-slate-300 mb-1">Share a link</h3>
          <p class="text-xs text-gray-500 mb-3">
            Anyone who opens this link will be automatically added as your friend.
          </p>

          <div v-if="inviteUrl" class="flex items-center gap-2 bg-slate-800 rounded-xl px-3 py-2 mb-2">
            <span class="flex-1 text-xs text-slate-300 truncate select-all">{{ inviteUrl }}</span>
          </div>

          <button
            class="w-full py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-semibold rounded-xl transition disabled:opacity-60 flex items-center justify-center gap-2"
            :disabled="loadingLink"
            @click="generateLink"
          >
            <span v-if="loadingLink">Generating…</span>
            <span v-else-if="linkCopied">✓ Copied!</span>
            <span v-else-if="inviteUrl">Copy link again</span>
            <span v-else>Generate &amp; copy invite link</span>
          </button>
        </section>

        <div class="border-t border-slate-700" />

        <!-- ── Invite by email ─────────────────────────────────────────────── -->
        <section>
          <h3 class="text-sm font-semibold text-slate-300 mb-1">Invite by email</h3>
          <p class="text-xs text-gray-500 mb-3">
            Already on Omnijoy? They'll be added as a friend instantly. Otherwise they'll get an email.
          </p>

          <form class="flex gap-2" @submit.prevent="sendEmailInvite">
            <input
              v-model="email"
              type="email"
              placeholder="friend@example.com"
              class="flex-1 bg-slate-800 border border-slate-600 text-slate-100 text-sm rounded-xl px-3 py-2 placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-indigo-500"
              :disabled="sendingEmail"
            />
            <button
              type="submit"
              class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-semibold rounded-xl transition disabled:opacity-60 shrink-0"
              :disabled="sendingEmail || !email.trim()"
            >
              {{ sendingEmail ? '…' : 'Send' }}
            </button>
          </form>

          <!-- Result -->
          <p v-if="emailResult?.outcome === 'accepted'" class="mt-2 text-sm text-green-400">
            🎉 <strong>{{ emailResult.displayName }}</strong> is already on Omnijoy — you're now friends!
          </p>
          <p v-else-if="emailResult?.outcome === 'invited'" class="mt-2 text-sm text-indigo-400">
            ✓ Invite sent! They'll receive an email with your invite link.
          </p>
          <p v-else-if="emailError" class="mt-2 text-sm text-red-400">{{ emailError }}</p>
        </section>

      </div>
    </div>
  </div>
</template>
