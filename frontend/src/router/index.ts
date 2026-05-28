import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

/**
 * Routes tagged `meta: { guest: true }` redirect authenticated users to /wall.
 * Routes tagged `meta: { requiresAuth: true, layout: 'app' }` are guarded and
 * rendered inside the AppShell layout.
 */

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    // ── Public / guest-only routes ────────────────────────────────────────────
    {
      path: '/',
      name: 'home',
      component: () => import('@/views/HomeView.vue'),
      meta: { guest: true },
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/auth/LoginView.vue'),
      meta: { guest: true },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/auth/RegisterView.vue'),
      meta: { guest: true },
    },

    // ── Authenticated routes (wrapped in AppShell) ────────────────────────────
    {
      path: '/wall',
      name: 'wall',
      component: () => import('@/views/feed/WallView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/friends',
      name: 'friends',
      component: () => import('@/views/friends/FriendsView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/profile/:userId',
      name: 'profile',
      component: () => import('@/views/profile/ProfileView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/events',
      name: 'events',
      component: () => import('@/views/events/EventsView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/events/:id',
      name: 'event-detail',
      component: () => import('@/views/events/EventDetailView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/company',
      name: 'company-list',
      component: () => import('@/views/company/CompanyListView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/company/:id',
      name: 'company',
      component: () => import('@/views/company/CompanyView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/live',
      name: 'live',
      component: () => import('@/views/live/LiveView.vue'),
      meta: { requiresAuth: true, layout: 'app', wideContent: true },
    },
    {
      path: '/live/:id',
      name: 'live-stream',
      component: () => import('@/views/live/LiveStreamView.vue'),
      meta: { requiresAuth: true, layout: 'app', wideContent: true },
    },
    {
      path: '/settings',
      name: 'settings',
      component: () => import('@/views/settings/SettingsView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/settings/privacy',
      name: 'settings-privacy',
      component: () => import('@/views/settings/PrivacySettingsView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },

    // ── Public share routes (OG meta tag rendering) ───────────────────────────
    {
      path: '/share/posts/:id',
      name: 'share-post',
      component: () => import('@/views/share/SharePostView.vue'),
    },
    {
      path: '/share/users/:id',
      name: 'share-user',
      component: () => import('@/views/share/ShareUserView.vue'),
    },
    {
      path: '/share/events/:id',
      name: 'share-event',
      component: () => import('@/views/share/ShareEventView.vue'),
    },

    // ── 404 ───────────────────────────────────────────────────────────────────
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/views/NotFoundView.vue'),
    },
  ],
})

// ── Navigation guards ─────────────────────────────────────────────────────────

router.beforeEach((to) => {
  const auth = useAuthStore()

  // Redirect unauthenticated users away from protected pages
  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  // Redirect authenticated users away from guest-only pages
  if (to.meta.guest && auth.isAuthenticated) {
    return { name: 'wall' }
  }
})

export default router
