<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink } from 'vue-router'

// ── Last-updated date — bump this whenever the terms text changes. ──────────
const LAST_UPDATED = 'June 1, 2026'

const META = {
  title:       'Terms of Service — Omnijoy',
  description: 'The rules that govern your use of Omnijoy: what you can post, how accounts work, and what we both agree to.',
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
          Terms of Service
        </h1>
        <p class="text-sm text-slate-500">Last updated: {{ LAST_UPDATED }}</p>
      </header>

      <section class="prose prose-invert max-w-none space-y-6 leading-relaxed">
        <p>
          Welcome to Omnijoy. These Terms of Service ("Terms") form a binding agreement
          between you and Omnijoy. By creating an account or using the service you agree
          to them. If you do not agree, please do not use Omnijoy.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">1. Who can use Omnijoy</h2>
        <p>
          You must be at least 13 years old (or the minimum digital-consent age in your
          country, whichever is higher) to create an Omnijoy account. You agree to provide
          accurate registration information and to keep your password confidential. You are
          responsible for everything that happens under your account.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">2. Your account</h2>
        <ul class="list-disc list-inside space-y-2">
          <li>One personal account per person. Company / brand presence belongs on a
            Company Page, not on a personal account.</li>
          <li>You may not impersonate another person or organization, or misrepresent
            your affiliation with anyone.</li>
          <li>You may close your account at any time from
            <RouterLink to="/settings" class="text-indigo-300 hover:text-indigo-400">Settings → Account</RouterLink>.
            We may suspend or terminate accounts that violate these Terms.</li>
        </ul>

        <h2 class="text-xl font-bold text-slate-100 mt-8">3. Your content</h2>
        <p>
          You keep ownership of everything you post on Omnijoy — your text, photos, videos,
          and live streams. By posting it, you grant Omnijoy a worldwide, non-exclusive,
          royalty-free license to host, store, reproduce, display, and distribute that
          content <strong>only</strong> so we can operate the service — for example, to
          show your post to the people you shared it with, to generate thumbnails, and to
          back it up. This license ends when you delete the content, except for residual
          copies in routine backups that are rotated out over time.
        </p>
        <p>
          You are responsible for the content you post. You confirm that you have the
          rights you need to share it, and that doing so does not violate the law or the
          rights of any third party.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">4. Acceptable use</h2>
        <p>Do not use Omnijoy to:</p>
        <ul class="list-disc list-inside space-y-2">
          <li>Post illegal content, including child sexual abuse material, threats of
            violence, or content that incites violence against a person or group.</li>
          <li>Harass, bully, dox, or target individuals with abuse.</li>
          <li>Post spam, scams, deceptive promotions, or coordinated inauthentic
            behavior.</li>
          <li>Distribute malware, exploit kits, or links to phishing pages.</li>
          <li>Infringe other people's intellectual-property rights. Repeat infringers will
            be removed.</li>
          <li>Scrape the service at scale, evade rate limits, or otherwise place an
            unreasonable load on our infrastructure.</li>
          <li>Reverse-engineer or interfere with the operation of Omnijoy, except where
            applicable law gives you a non-waivable right to do so.</li>
        </ul>
        <p>
          You can report content that breaks these rules from the "…" menu on any post,
          comment, profile, or message. Reports go to our moderation team.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">5. Moderation and enforcement</h2>
        <p>
          We may, at our reasonable discretion, remove content, restrict features, or
          suspend or terminate accounts that violate these Terms or applicable law. Where
          appropriate, we will give you notice and an opportunity to appeal — but in urgent
          cases (such as imminent harm or legal obligation) we may act first and explain
          afterward.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">6. Company Pages and events</h2>
        <p>
          Company Pages let businesses, organizations, and creators maintain a presence on
          Omnijoy. Page owners are responsible for the content their page publishes and
          for accurate representation of their organization. Events created on Omnijoy may
          not be used to organize unlawful activity.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">7. Live streaming</h2>
        <p>
          When you go live, the same acceptable-use rules apply in real time. We may
          interrupt a stream that violates these Terms. By streaming you also confirm
          you have the rights to broadcast the music, video, and other material in your
          stream.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">8. Privacy</h2>
        <p>
          Our handling of personal data is governed by the
          <RouterLink to="/privacy" class="text-indigo-300 hover:text-indigo-400">Privacy Policy</RouterLink>,
          which forms part of these Terms.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">9. Third-party content and links</h2>
        <p>
          Omnijoy may show content posted by other users or contain links to third-party
          websites. We do not endorse third-party content and we are not responsible for
          it. Visit external links at your own discretion.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">10. Service changes and availability</h2>
        <p>
          Omnijoy is provided on an ongoing basis but may evolve, be temporarily
          unavailable for maintenance, or have features added or removed. We will make
          reasonable efforts to keep the service running and to give notice of significant
          changes.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">11. Disclaimers</h2>
        <p>
          Omnijoy is provided <strong>"as is" and "as available"</strong>, without
          warranties of any kind, whether express or implied, including warranties of
          merchantability, fitness for a particular purpose, and non-infringement, to the
          maximum extent permitted by law. We do not warrant that the service will be
          uninterrupted, error-free, or free of harmful components.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">12. Limitation of liability</h2>
        <p>
          To the maximum extent permitted by law, Omnijoy will not be liable for any
          indirect, incidental, special, consequential, or punitive damages, or for any
          loss of profits, revenue, data, or goodwill, arising out of or in connection
          with the service. Our total liability for any claim arising out of or relating
          to these Terms or the service is limited to the greater of (a) the amount you
          paid us in the twelve months before the event giving rise to the claim, or
          (b) one hundred US dollars (USD 100). Some jurisdictions do not allow these
          limitations — in that case the limitations apply only to the extent permitted
          by law.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">13. Indemnity</h2>
        <p>
          You agree to indemnify and hold harmless Omnijoy, its affiliates, and their
          personnel from any claim, demand, loss, or expense (including reasonable legal
          fees) arising from your content, your use of the service, or your violation of
          these Terms.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">14. Termination</h2>
        <p>
          You may stop using Omnijoy and delete your account at any time. We may
          terminate or suspend your access for breach of these Terms, prolonged inactivity,
          or where required by law. Sections that by their nature should survive
          termination — including content licenses you have already granted, disclaimers,
          limitations of liability, and dispute-resolution terms — will continue to apply
          after your account ends.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">15. Governing law and disputes</h2>
        <p>
          These Terms are governed by the laws of the jurisdiction in which Omnijoy is
          established, without regard to its conflict-of-laws rules. To the extent
          permitted by law, any dispute will be resolved exclusively in the courts of
          that jurisdiction. Nothing in these Terms affects your rights as a consumer
          under the mandatory law of your country of residence.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">16. Changes to these Terms</h2>
        <p>
          We may update these Terms from time to time. When we make material changes we
          will update the "Last updated" date above and, for significant changes, give
          you advance notice. Continued use of Omnijoy after the effective date
          constitutes acceptance of the updated Terms.
        </p>

        <h2 class="text-xl font-bold text-slate-100 mt-8">17. Contact</h2>
        <p>
          Questions about these Terms:
          <a class="text-indigo-300 hover:text-indigo-400" href="mailto:legal@omnijoy.app">legal&#64;omnijoy.app</a>.
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
