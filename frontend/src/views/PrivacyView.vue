<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink } from 'vue-router'

// ── Last-updated date — bump this whenever the policy text changes. ─────────
const LAST_UPDATED = 'June 1, 2026'

const META = {
  title:       'Privacy Policy — Omnijoy',
  description: 'How Omnijoy collects, uses, and protects your personal data. No ads. No data sales. Ever.',
}

onMounted(() => {
  document.title = META.title

  const setMeta = (key: string, content: string, attr = 'name') => {
    let el = document.querySelector(`meta[${attr}="${key}"]`) as HTMLMetaElement | null
    if (!el) {
      el = document.createElement('meta')
      el.setAttribute(attr, key)
      document.head.appendChild(el)
    }
    el.setAttribute('content', content)
  }
  setMeta('description', META.description)
})
</script>

<template>
  <div class="min-h-screen bg-slate-950 flex flex-col">
    <!-- ── Header ───────────────────────────────────────────────────────── -->
    <nav class="sticky top-0 z-30 bg-slate-800/90 backdrop-blur border-b border-slate-700">
      <div class="max-w-6xl mx-auto px-4 h-16 flex items-center justify-between">
        <RouterLink to="/" class="flex items-center gap-2">
          <img src="/logo.png" alt="Omnijoy" class="h-8 w-8 rounded-lg" />
          <span class="text-lg font-bold text-slate-100 tracking-tight">Omnijoy</span>
        </RouterLink>
        <div class="flex items-center gap-3">
          <RouterLink
            to="/login"
            class="text-sm font-medium text-slate-300 hover:text-indigo-400 transition-colors px-3 py-2"
          >
            Log in
          </RouterLink>
          <RouterLink
            to="/register"
            class="text-sm font-medium bg-indigo-600 hover:bg-indigo-700 text-white px-4 py-2 rounded-lg transition-colors"
          >
            Sign up
          </RouterLink>
        </div>
      </div>
    </nav>

    <!-- ── Article ──────────────────────────────────────────────────────── -->
    <article class="flex-1 max-w-3xl w-full mx-auto px-4 py-12 text-slate-300">
      <header class="mb-10">
        <h1 class="text-3xl sm:text-4xl font-extrabold text-slate-100 tracking-tight mb-2">
          Privacy Policy
        </h1>
        <p class="text-sm text-slate-500">Last updated: {{ LAST_UPDATED }}</p>
      </header>

      <section class="prose prose-invert max-w-none space-y-6 leading-relaxed">
        <p>
          Omnijoy is a social platform built around a simple promise: <strong>no ads,
          no forced feed, and we never sell your data</strong>. This policy explains what
          personal information we collect, why we collect it, how we use it, and the
          choices you have. If anything below is unclear, contact us at
          <a class="text-indigo-300 hover:text-indigo-400" href="mailto:privacy@omnijoy.app">privacy&#64;omnijoy.app</a>.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">1. Information we collect</h2>
        <p>We only collect what we need to operate the service:</p>
        <ul class="list-disc list-inside space-y-2">
          <li>
            <strong>Account information</strong> — your display name, email address, password
            (stored as a one-way hash), and optional profile fields you choose to add (avatar,
            bio, location).
          </li>
          <li>
            <strong>Content you create</strong> — posts, comments, reactions, messages,
            uploaded photos and videos, events, and live streams you host.
          </li>
          <li>
            <strong>Social graph</strong> — your friend connections, follow relationships,
            and the company pages you manage or follow.
          </li>
          <li>
            <strong>Technical data</strong> — IP address, browser user-agent, and a session
            identifier used to keep you signed in. We log a minimal request trail so we can
            debug errors and detect abuse.
          </li>
          <li>
            <strong>Optional location</strong> — if you set a public location on the home
            page, it is stored in your browser only (localStorage) until you clear it. We do
            not use device GPS or precise location.
          </li>
        </ul>
        <p>
          We do <em>not</em> use third-party advertising trackers, fingerprinting scripts,
          or social-media "share" pixels.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">2. How we use your information</h2>
        <ul class="list-disc list-inside space-y-2">
          <li>To operate the service — show you your feed, deliver messages and notifications,
            and let your friends find you.</li>
          <li>To keep accounts secure — detect suspicious sign-ins, rate-limit abusive
            traffic, and recover compromised accounts.</li>
          <li>To maintain and improve the platform — debug errors, monitor performance, and
            plan capacity.</li>
          <li>To enforce our <RouterLink to="/terms" class="text-indigo-300 hover:text-indigo-400">Terms of Service</RouterLink>
            and handle reports about content that violates them.</li>
          <li>To comply with the law when required by a valid legal request.</li>
        </ul>

        <h2 class="text-xl font-bold text-slate-100 mt-8">3. What we never do</h2>
        <ul class="list-disc list-inside space-y-2">
          <li>We do <strong>not</strong> sell, rent, or trade your personal data to anyone.</li>
          <li>We do <strong>not</strong> run third-party advertising on Omnijoy.</li>
          <li>We do <strong>not</strong> profile you to manipulate the order of your feed —
            content from your friends appears in the order they post it.</li>
          <li>We do <strong>not</strong> read your private messages, except where required to
            investigate a specific abuse report you or another user files.</li>
        </ul>

        <h2 class="text-xl font-bold text-slate-100 mt-8">4. Cookies and local storage</h2>
        <p>
          Omnijoy uses a single authentication cookie / token to keep you signed in, and
          browser localStorage to remember small preferences (theme, language, the public
          location you opted into). We do not set advertising or analytics cookies, and we
          do not load third-party scripts that would.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">5. Sharing with third parties</h2>
        <p>We share data only in three narrow cases:</p>
        <ul class="list-disc list-inside space-y-2">
          <li><strong>Service providers</strong> we rely on to operate the platform — for
            example, our hosting provider, email-delivery service, and managed database. These
            providers are contractually bound to only process data on our instructions.</li>
          <li><strong>Legal requests</strong> — when we are compelled by a valid court order,
            subpoena, or applicable law, and we cannot lawfully push back.</li>
          <li><strong>Business transfers</strong> — if Omnijoy is ever acquired, merged, or
            reorganized, your data may transfer to the new entity. Any new owner is bound by
            the commitments in this policy or must give you notice and a chance to delete
            your account before changing them.</li>
        </ul>

        <h2 class="text-xl font-bold text-slate-100 mt-8">6. Data retention</h2>
        <p>
          We keep your account data for as long as your account is active. When you delete
          your account, your profile, posts, messages, and uploaded media are removed from
          our active systems within 30 days. Backups containing residual copies are rotated
          out within 90 days. We may retain a minimal log of the deletion event itself for
          fraud prevention and to comply with the law.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">7. Your rights</h2>
        <p>
          Depending on where you live, you may have the right to access, correct, export,
          or delete the personal data we hold about you, and to object to or restrict
          certain processing. You can exercise most of these rights directly inside
          <RouterLink to="/settings" class="text-indigo-300 hover:text-indigo-400">Settings</RouterLink>:
        </p>
        <ul class="list-disc list-inside space-y-2">
          <li><strong>Access &amp; export</strong> — download a copy of your data from
            Settings → Account.</li>
          <li><strong>Correction</strong> — edit your profile and preferences at any time.</li>
          <li><strong>Deletion</strong> — close your account from Settings → Account.
            Confirmation will appear in your email inbox.</li>
        </ul>
        <p>
          For anything you cannot do yourself, write to
          <a class="text-indigo-300 hover:text-indigo-400" href="mailto:privacy@omnijoy.app">privacy&#64;omnijoy.app</a>
          and we will respond within 30 days.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">8. International data transfers</h2>
        <p>
          Omnijoy is operated from data centers that may be located outside your country of
          residence. When we transfer personal data across borders we rely on appropriate
          safeguards — standard contractual clauses, adequacy decisions, or your explicit
          consent where required by law.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">9. Children</h2>
        <p>
          Omnijoy is not directed to children under 13 (or under the equivalent minimum age
          in your jurisdiction). If you believe a child has created an account, contact us
          and we will remove it.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">10. Security</h2>
        <p>
          Passwords are stored as one-way hashes. All traffic between you and Omnijoy is
          encrypted in transit (TLS). We restrict internal access to personal data on a
          need-to-know basis. No system is perfect — if you believe you have found a
          security issue, please disclose it responsibly to
          <a class="text-indigo-300 hover:text-indigo-400" href="mailto:security@omnijoy.app">security&#64;omnijoy.app</a>.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">11. Changes to this policy</h2>
        <p>
          When we make material changes we will update the "Last updated" date above and,
          for significant changes, notify you in-app or by email before they take effect.
          Continued use of Omnijoy after the effective date constitutes acceptance of the
          updated policy.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">12. Contact</h2>
        <p>
          Questions, requests, or complaints:
          <a class="text-indigo-300 hover:text-indigo-400" href="mailto:privacy@omnijoy.app">privacy&#64;omnijoy.app</a>.
        </p>
      </section>
    </article>

    <!-- ── Footer ───────────────────────────────────────────────────────── -->
    <footer class="bg-gray-900 text-slate-500 py-8 mt-12">
      <div class="max-w-6xl mx-auto px-4 flex flex-col sm:flex-row items-center justify-between gap-3">
        <RouterLink to="/" class="flex items-center gap-2">
          <img src="/logo.png" alt="Omnijoy" class="h-6 w-6 rounded-md" />
          <span class="text-sm font-medium text-slate-400">Omnijoy</span>
        </RouterLink>
        <p class="text-xs">© 2026 Omnijoy · No ads. No data sales. Ever.</p>
        <div class="flex gap-4 text-xs">
          <RouterLink to="/privacy" class="hover:text-gray-200 transition-colors">Privacy</RouterLink>
          <RouterLink to="/terms"   class="hover:text-gray-200 transition-colors">Terms</RouterLink>
          <RouterLink to="/about"   class="hover:text-gray-200 transition-colors">About</RouterLink>
        </div>
      </div>
    </footer>
  </div>
</template>
