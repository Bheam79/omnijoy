<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink } from 'vue-router'

// ── SEO / Open Graph meta tags ─────────────────────────────────────────────────
const META = {
  title: 'Omnijoy — Social media without ads or forced content',
  description: 'Connect with friends and communities on Omnijoy. No ads, no algorithm-forced content — just the people and topics you actually care about.',
  image: '/og-image.png',
}

onMounted(() => {
  document.title = META.title

  const setMeta = (property: string, content: string, attr = 'name') => {
    let el = document.querySelector(`meta[${attr}="${property}"]`) as HTMLMetaElement | null
    if (!el) {
      el = document.createElement('meta')
      el.setAttribute(attr, property)
      document.head.appendChild(el)
    }
    el.setAttribute('content', content)
  }

  setMeta('description', META.description)
  setMeta('keywords', 'social media, no ads, friends, community, omnijoy')

  // Open Graph
  setMeta('og:title', META.title, 'property')
  setMeta('og:description', META.description, 'property')
  setMeta('og:type', 'website', 'property')
  setMeta('og:url', window.location.origin, 'property')
  setMeta('og:image', META.image, 'property')
  setMeta('og:site_name', 'Omnijoy', 'property')

  // Twitter Card
  setMeta('twitter:card', 'summary_large_image')
  setMeta('twitter:title', META.title)
  setMeta('twitter:description', META.description)
  setMeta('twitter:image', META.image)
})

const features = [
  {
    icon: 'no-ads',
    title: 'Zero ads, forever',
    description: 'No sponsored posts, no tracking pixels, no data sold to advertisers. Your experience belongs to you.',
    color: 'indigo',
  },
  {
    icon: 'feed',
    title: 'Your feed, your rules',
    description: 'See posts from the people and pages you actually follow — chronologically, without algorithmic manipulation.',
    color: 'violet',
  },
  {
    icon: 'realtime',
    title: 'Real-time everywhere',
    description: 'Posts, messages, notifications — all delivered instantly via SignalR. No polling, no refresh needed.',
    color: 'blue',
  },
  {
    icon: 'privacy',
    title: 'Privacy first',
    description: 'Granular controls for every post. Public, friends-only, or just you — always your call.',
    color: 'emerald',
  },
  {
    icon: 'events',
    title: 'Events & communities',
    description: 'Create or join events, build company pages, and bring your community together in one place.',
    color: 'amber',
  },
  {
    icon: 'live',
    title: 'Go live',
    description: 'Stream to your audience in real time with live chat and instant viewer notifications.',
    color: 'rose',
  },
]

const colorMap: Record<string, string> = {
  indigo: 'bg-indigo-50 text-indigo-600',
  violet: 'bg-violet-50 text-violet-600',
  blue: 'bg-blue-50 text-blue-600',
  emerald: 'bg-emerald-50 text-emerald-600',
  amber: 'bg-amber-50 text-amber-600',
  rose: 'bg-rose-50 text-rose-600',
}
</script>

<template>
  <div class="min-h-screen bg-white flex flex-col">

    <!-- ── Navigation bar ────────────────────────────────────────────────────── -->
    <nav class="sticky top-0 z-30 bg-white/90 backdrop-blur border-b border-gray-100">
      <div class="max-w-6xl mx-auto px-4 h-16 flex items-center justify-between">
        <!-- Logo -->
        <div class="flex items-center gap-2">
          <div class="h-8 w-8 rounded-lg bg-indigo-600 flex items-center justify-center">
            <span class="text-white font-bold text-sm">OJ</span>
          </div>
          <span class="text-lg font-bold text-gray-900 tracking-tight">Omnijoy</span>
        </div>

        <!-- CTA buttons -->
        <div class="flex items-center gap-3">
          <RouterLink
            to="/login"
            class="text-sm font-medium text-gray-700 hover:text-indigo-600 transition-colors px-3 py-2"
          >
            Log in
          </RouterLink>
          <RouterLink
            to="/register"
            class="text-sm font-medium bg-indigo-600 hover:bg-indigo-700 text-white px-4 py-2 rounded-lg transition-colors"
          >
            Sign up — it's free
          </RouterLink>
        </div>
      </div>
    </nav>

    <!-- ── Hero section ──────────────────────────────────────────────────────── -->
    <section class="relative overflow-hidden bg-gradient-to-br from-indigo-50 via-white to-violet-50 py-24 sm:py-32">
      <!-- Decorative blobs -->
      <div class="absolute -top-40 -right-20 h-80 w-80 rounded-full bg-indigo-100 opacity-50 blur-3xl pointer-events-none" />
      <div class="absolute -bottom-20 -left-20 h-72 w-72 rounded-full bg-violet-100 opacity-40 blur-3xl pointer-events-none" />

      <div class="relative max-w-4xl mx-auto px-4 text-center">
        <!-- Badge -->
        <div class="inline-flex items-center gap-2 bg-indigo-50 text-indigo-700 border border-indigo-100 rounded-full px-4 py-1.5 text-sm font-medium mb-8">
          <span class="h-2 w-2 rounded-full bg-indigo-500 animate-pulse" />
          No ads. No algorithms forcing content. Just people.
        </div>

        <!-- Headline -->
        <h1 class="text-5xl sm:text-6xl lg:text-7xl font-extrabold text-gray-900 tracking-tight leading-tight mb-6">
          Social media<br />
          <span class="text-transparent bg-clip-text bg-gradient-to-r from-indigo-600 to-violet-600">
            the way it should be.
          </span>
        </h1>

        <!-- Subheadline -->
        <p class="text-xl sm:text-2xl text-gray-600 max-w-2xl mx-auto mb-10 leading-relaxed">
          Connect with the people you care about. Share freely. No ads, no sponsored posts,
          no data sold to the highest bidder.
        </p>

        <!-- CTA buttons -->
        <div class="flex flex-col sm:flex-row gap-4 justify-center">
          <RouterLink
            to="/register"
            class="inline-flex items-center justify-center gap-2 bg-indigo-600 hover:bg-indigo-700 text-white font-semibold px-8 py-4 rounded-xl text-lg transition-colors shadow-lg shadow-indigo-200"
          >
            Create your free account
            <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 8l4 4m0 0l-4 4m4-4H3"/>
            </svg>
          </RouterLink>
          <RouterLink
            to="/login"
            class="inline-flex items-center justify-center gap-2 bg-white hover:bg-gray-50 text-gray-700 font-semibold px-8 py-4 rounded-xl text-lg transition-colors border border-gray-200 shadow-sm"
          >
            Sign in
          </RouterLink>
        </div>

        <!-- Social proof hint -->
        <p class="mt-8 text-sm text-gray-400">
          Free forever · No credit card required · No tracking
        </p>
      </div>
    </section>

    <!-- ── Features grid ─────────────────────────────────────────────────────── -->
    <section class="py-20 sm:py-24 bg-white">
      <div class="max-w-6xl mx-auto px-4">
        <!-- Section heading -->
        <div class="text-center mb-16">
          <h2 class="text-3xl sm:text-4xl font-bold text-gray-900 mb-4">
            Built for humans, not advertisers
          </h2>
          <p class="text-lg text-gray-500 max-w-2xl mx-auto">
            Every decision we make starts with: "does this make the experience better for users?"
          </p>
        </div>

        <!-- Feature cards -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          <div
            v-for="feature in features"
            :key="feature.title"
            class="rounded-2xl border border-gray-100 bg-white p-6 hover:border-gray-200 hover:shadow-md transition-all group"
          >
            <!-- Icon -->
            <div class="mb-4 inline-flex">
              <div :class="['h-11 w-11 rounded-xl flex items-center justify-center', colorMap[feature.color]]">

                <!-- No ads icon -->
                <svg v-if="feature.icon === 'no-ads'" class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636"/>
                </svg>

                <!-- Feed icon -->
                <svg v-else-if="feature.icon === 'feed'" class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16"/>
                </svg>

                <!-- Real-time icon -->
                <svg v-else-if="feature.icon === 'realtime'" class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
                </svg>

                <!-- Privacy icon -->
                <svg v-else-if="feature.icon === 'privacy'" class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
                </svg>

                <!-- Events icon -->
                <svg v-else-if="feature.icon === 'events'" class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
                </svg>

                <!-- Live icon -->
                <svg v-else-if="feature.icon === 'live'" class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 10l4.553-2.069A1 1 0 0121 8.876V15.124a1 1 0 01-1.447.894L15 14M3 8a2 2 0 012-2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V8z"/>
                </svg>

              </div>
            </div>

            <h3 class="text-base font-semibold text-gray-900 mb-2">{{ feature.title }}</h3>
            <p class="text-sm text-gray-500 leading-relaxed">{{ feature.description }}</p>
          </div>
        </div>
      </div>
    </section>

    <!-- ── "Why Omnijoy?" section ────────────────────────────────────────────── -->
    <section class="py-20 bg-gray-50">
      <div class="max-w-4xl mx-auto px-4">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-8 text-center">
          <div>
            <p class="text-4xl font-extrabold text-indigo-600 mb-2">0</p>
            <p class="text-base font-medium text-gray-700">Ads shown, ever</p>
          </div>
          <div>
            <p class="text-4xl font-extrabold text-indigo-600 mb-2">100%</p>
            <p class="text-base font-medium text-gray-700">Chronological feed</p>
          </div>
          <div>
            <p class="text-4xl font-extrabold text-indigo-600 mb-2">Real-time</p>
            <p class="text-base font-medium text-gray-700">Updates via SignalR</p>
          </div>
        </div>
      </div>
    </section>

    <!-- ── Bottom CTA ────────────────────────────────────────────────────────── -->
    <section class="py-20 sm:py-24 bg-gradient-to-br from-indigo-600 to-violet-600">
      <div class="max-w-2xl mx-auto px-4 text-center">
        <h2 class="text-3xl sm:text-4xl font-bold text-white mb-4">
          Ready to enjoy social media again?
        </h2>
        <p class="text-indigo-100 text-lg mb-10">
          Join Omnijoy today — free, private, and always ad-free.
        </p>
        <div class="flex flex-col sm:flex-row gap-4 justify-center">
          <RouterLink
            to="/register"
            class="inline-flex items-center justify-center bg-white hover:bg-gray-50 text-indigo-700 font-semibold px-8 py-4 rounded-xl text-lg transition-colors shadow"
          >
            Get started for free
          </RouterLink>
          <RouterLink
            to="/login"
            class="inline-flex items-center justify-center border border-indigo-300 hover:border-indigo-100 text-white font-semibold px-8 py-4 rounded-xl text-lg transition-colors"
          >
            Sign in
          </RouterLink>
        </div>
      </div>
    </section>

    <!-- ── Footer ────────────────────────────────────────────────────────────── -->
    <footer class="bg-gray-900 text-gray-400 py-10">
      <div class="max-w-6xl mx-auto px-4 flex flex-col sm:flex-row items-center justify-between gap-4">
        <div class="flex items-center gap-2">
          <div class="h-6 w-6 rounded-md bg-indigo-500 flex items-center justify-center">
            <span class="text-white font-bold text-xs">OJ</span>
          </div>
          <span class="text-sm font-medium text-gray-300">Omnijoy</span>
        </div>
        <p class="text-sm">© 2025 Omnijoy · No ads. No data sales. Ever.</p>
        <div class="flex gap-5 text-sm">
          <a href="#" class="hover:text-gray-200 transition-colors">Privacy</a>
          <a href="#" class="hover:text-gray-200 transition-colors">Terms</a>
          <a href="#" class="hover:text-gray-200 transition-colors">About</a>
        </div>
      </div>
    </footer>

  </div>
</template>
