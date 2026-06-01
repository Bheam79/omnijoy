<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink } from 'vue-router'

const META = {
  title:       'About Omnijoy — A social platform without ads or forced feeds',
  description: 'Why Omnijoy exists, what we build, and the principles we will not bend on.',
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

    <!-- ── Hero ─────────────────────────────────────────────────────────── -->
    <section class="bg-gradient-to-br from-slate-900 via-indigo-950 to-slate-900 border-b border-slate-700">
      <div class="max-w-3xl mx-auto px-4 py-12 sm:py-16 text-center">
        <img src="/logo.png" alt="Omnijoy" class="h-20 w-20 mx-auto mb-5 rounded-2xl" />
        <h1 class="text-3xl sm:text-4xl font-extrabold text-slate-100 tracking-tight mb-3">
          A social platform that gets out of your way.
        </h1>
        <p class="text-base text-slate-400 max-w-2xl mx-auto">
          Omnijoy is a place to talk to your friends, share your moments, and discover
          what people near you are doing — without ads, without algorithmic manipulation,
          and without your data being sold.
        </p>
      </div>
    </section>

    <!-- ── Article ──────────────────────────────────────────────────────── -->
    <article class="flex-1 max-w-3xl w-full mx-auto px-4 py-12 text-slate-300">
      <section class="space-y-6 leading-relaxed">
        <h2 class="text-2xl font-bold text-slate-100">Why we built Omnijoy</h2>
        <p>
          The big social networks made a trade: they gave you a feed for free, and in
          return they decided what you saw, how often, and from whom. The result is a
          product that has slowly stopped being about your friends and started being
          about whatever keeps you scrolling longest. We wanted something different —
          a quieter place that respects your time and your attention.
        </p>

        <h2 class="text-2xl font-bold text-slate-100 mt-10">Our principles</h2>
        <div class="grid sm:grid-cols-2 gap-5 mt-2">
          <div class="bg-slate-800 rounded-xl border border-slate-700 p-5">
            <h3 class="text-slate-100 font-semibold mb-2">No ads. Ever.</h3>
            <p class="text-sm text-slate-400">
              You are not the product. Your feed is not a billboard. We will not change
              that.
            </p>
          </div>
          <div class="bg-slate-800 rounded-xl border border-slate-700 p-5">
            <h3 class="text-slate-100 font-semibold mb-2">No forced feed.</h3>
            <p class="text-sm text-slate-400">
              Posts from your friends appear in the order they posted them. No engagement
              ranking, no "suggested for you" injected between real updates.
            </p>
          </div>
          <div class="bg-slate-800 rounded-xl border border-slate-700 p-5">
            <h3 class="text-slate-100 font-semibold mb-2">Real-time, not pull-to-refresh.</h3>
            <p class="text-sm text-slate-400">
              New posts, messages, reactions, and live events arrive the moment they
              happen — pushed directly, not polled.
            </p>
          </div>
          <div class="bg-slate-800 rounded-xl border border-slate-700 p-5">
            <h3 class="text-slate-100 font-semibold mb-2">Your data stays yours.</h3>
            <p class="text-sm text-slate-400">
              We do not sell, rent, or trade your personal data. You can export everything
              you have created and delete your account at any time.
            </p>
          </div>
        </div>

        <h2 class="text-2xl font-bold text-slate-100 mt-10">What you can do here</h2>
        <ul class="list-disc list-inside space-y-2">
          <li><strong>Share</strong> posts, photos, and videos with the people who follow you.</li>
          <li><strong>Message</strong> friends one-on-one or in groups, in real time.</li>
          <li><strong>Discover events</strong> happening near you, and host your own.</li>
          <li><strong>Go live</strong> to your followers with low-latency video and chat.</li>
          <li><strong>Run a Company Page</strong> for your business, brand, or community.</li>
        </ul>

        <h2 class="text-2xl font-bold text-slate-100 mt-10">How we keep the lights on</h2>
        <p>
          Omnijoy is funded by its founders and (eventually) optional paid features for
          people who want more — extra storage, larger live audiences, advanced analytics
          for Company Pages. The free experience will always include the social features
          most people come here for: posting, friends, messaging, events, live, and a
          feed that is not an ad surface.
        </p>

        <h2 class="text-2xl font-bold text-slate-100 mt-10">Safety and moderation</h2>
        <p>
          A small, non-distracting social network only works if people behave decently to
          each other. We publish clear
          <RouterLink to="/terms" class="text-indigo-300 hover:text-indigo-400">community rules</RouterLink>,
          we act on reports, and we publish how we use the personal data we collect in our
          <RouterLink to="/privacy" class="text-indigo-300 hover:text-indigo-400">Privacy Policy</RouterLink>.
          We will not be the largest social network in the world, and that is fine.
        </p>

        <h2 class="text-2xl font-bold text-slate-100 mt-10">Get in touch</h2>
        <ul class="list-disc list-inside space-y-2">
          <li>General questions —
            <a class="text-indigo-300 hover:text-indigo-400" href="mailto:hello@omnijoy.app">hello&#64;omnijoy.app</a>
          </li>
          <li>Privacy &amp; data requests —
            <a class="text-indigo-300 hover:text-indigo-400" href="mailto:privacy@omnijoy.app">privacy&#64;omnijoy.app</a>
          </li>
          <li>Security disclosures —
            <a class="text-indigo-300 hover:text-indigo-400" href="mailto:security@omnijoy.app">security&#64;omnijoy.app</a>
          </li>
          <li>Press &amp; partnerships —
            <a class="text-indigo-300 hover:text-indigo-400" href="mailto:press@omnijoy.app">press&#64;omnijoy.app</a>
          </li>
        </ul>
      </section>

      <!-- ── CTA ───────────────────────────────────────────────────────── -->
      <section class="mt-12 bg-gradient-to-br from-indigo-600 to-violet-600 rounded-2xl p-8 text-center">
        <h2 class="text-2xl font-bold text-white mb-2">
          Come join us.
        </h2>
        <p class="text-indigo-100 text-sm mb-5">
          Free forever. No ads. Real-time updates. No data sold.
        </p>
        <div class="flex flex-col sm:flex-row gap-3 justify-center">
          <RouterLink
            to="/register"
            class="inline-flex items-center justify-center bg-slate-800 hover:bg-slate-700 text-indigo-300 font-semibold px-6 py-3 rounded-xl text-sm transition-colors shadow"
          >
            Create a free account
          </RouterLink>
          <RouterLink
            to="/login"
            class="inline-flex items-center justify-center border border-indigo-300 hover:border-indigo-700 text-white font-semibold px-6 py-3 rounded-xl text-sm transition-colors"
          >
            Sign in
          </RouterLink>
        </div>
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
