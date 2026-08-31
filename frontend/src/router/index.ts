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
    {
      path: '/forgot-password',
      name: 'forgot-password',
      component: () => import('@/views/auth/ForgotPasswordView.vue'),
      meta: { guest: true },
    },
    // Email-verification landing page reached from the welcome email link.
    // Token is the credential — no auth gate either way (lets guests, who
    // could land here mid-registration, finish verifying without logging in).
    {
      path: '/verify-email',
      name: 'verify-email',
      component: () => import('@/views/auth/VerifyEmailView.vue'),
    },
    // Static info pages — public (no auth gate), shown to guests and signed-in
    // users alike. The path segments `privacy`, `terms`, and `about` are
    // already in backend SlugValidator.ReservedSlugs.
    {
      path: '/privacy',
      name: 'privacy',
      component: () => import('@/views/PrivacyView.vue'),
    },
    {
      path: '/terms',
      name: 'terms',
      component: () => import('@/views/TermsView.vue'),
    },
    {
      path: '/about',
      name: 'about',
      component: () => import('@/views/AboutView.vue'),
    },

    // ── Authenticated routes (wrapped in AppShell) ────────────────────────────
    {
      path: '/wall',
      name: 'wall',
      component: () => import('@/views/feed/WallView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/saved',
      name: 'saved-posts',
      component: () => import('@/views/feed/SavedPostsView.vue'),
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
      path: '/events/:id/edit',
      name: 'event-edit',
      component: () => import('@/views/events/EventEditView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/events/:id/participants',
      name: 'event-participants',
      component: () => import('@/views/events/EventParticipantsView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/events/:id/settings',
      name: 'event-settings',
      component: () => import('@/views/events/EventSettingsView.vue'),
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
      path: '/search',
      name: 'search',
      component: () => import('@/views/search/SearchResultsView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/settings',
      name: 'settings',
      component: () => import('@/views/settings/SettingsView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/settings/profile',
      name: 'settings-profile',
      component: () => import('@/views/settings/ProfileSettingsView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/settings/privacy',
      name: 'settings-privacy',
      component: () => import('@/views/settings/PrivacySettingsView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/settings/account',
      name: 'settings-account',
      component: () => import('@/views/settings/AccountSettingsView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/settings/notifications',
      name: 'settings-notifications',
      component: () => import('@/views/settings/NotificationSettingsView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },
    {
      path: '/notifications',
      name: 'notifications',
      component: () => import('@/views/notifications/NotificationsView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
    },

    // ── Admin / Moderator panel ───────────────────────────────────────────────
    //
    // Guard: requires role 'Admin' OR 'Moderator' (see beforeEach below).
    // /admin redirects to /admin/reports. The Audit Log child is Admin-only
    // and is enforced both by the guard (requiresAdmin) and by the
    // AdminShell sidebar hiding the nav link for non-Admins.
    {
      path: '/admin',
      component: () => import('@/views/admin/AdminShell.vue'),
      meta: { requiresAuth: true, layout: 'app', requiresModeratorOrAdmin: true, wideContent: true },
      redirect: '/admin/reports',
      children: [
        {
          path: 'reports',
          name: 'admin-reports',
          component: () => import('@/views/admin/ReportQueueView.vue'),
        },
        {
          path: 'users',
          name: 'admin-users',
          component: () => import('@/views/admin/UserManagementView.vue'),
        },
        {
          path: 'audit-log',
          name: 'admin-audit-log',
          component: () => import('@/views/admin/AuditLogView.vue'),
          meta: { requiresAdmin: true },
        },
      ],
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

    // ── Friend invite redemption ──────────────────────────────────────────────
    // Public route — no auth required for the info page.
    // The view handles auth gating itself: unauthenticated users are prompted
    // to register/login and then auto-redirected back to accept.
    // "invite" is in ReservedSlugs so no vanity slug can shadow this path.
    {
      path: '/invite/:token',
      name: 'invite-accept',
      component: () => import('@/views/friends/InviteAcceptView.vue'),
    },

    // ── Vanity URL catch-all (:slug) ──────────────────────────────────────────
    //
    // IMPORTANT: Every top-level path segment added above this line MUST also
    // appear in the backend's ReservedSlugs constant so users cannot claim a
    // slug that would shadow an application route:
    //   backend/Omnijoy.Infrastructure/Services/SlugService.cs → SlugValidator.ReservedSlugs
    //
    // This route must remain LAST among real routes, just before the 404 catch-all.
    {
      path: '/:slug([a-z][a-z0-9_-]{2,29})',
      name: 'slug-resolver',
      component: () => import('@/views/SlugResolverView.vue'),
      meta: { requiresAuth: true, layout: 'app' },
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

  // Admin panel — gate by role. `requiresAdmin` is strict Admin; the looser
  // `requiresModeratorOrAdmin` admits both. Either check matches against the
  // role on the cached user record (which is hydrated from the JWT response).
  // Visit a non-admin location instead of throwing 401: it produces a less
  // surprising UX for users who follow a stale link.
  const role = auth.user?.role
  const matched = to.matched
  const needsAdminOnly = matched.some(r => r.meta?.requiresAdmin)
  const needsModeratorOrAdmin = matched.some(r => r.meta?.requiresModeratorOrAdmin)
  if (needsAdminOnly && role !== 'Admin') {
    return { name: 'wall' }
  }
  if (needsModeratorOrAdmin && role !== 'Admin' && role !== 'Moderator') {
    return { name: 'wall' }
  }
})

// ── Chunk-load failure recovery ───────────────────────────────────────────────
//
// After a backend redeploy, Vite's content-hashed filenames change (e.g.
// ShareEventView-DjoIrJyI.js). A client that still holds the old index.html
// will try to lazy-import a chunk that no longer exists, producing:
//   TypeError: Failed to fetch dynamically imported module
//
// We catch that here and perform a hard navigation to the intended route,
// which re-fetches index.html and picks up the updated chunk manifest.
// The user lands on the page they clicked with no visible error.
router.onError((error, to) => {
  const isChunkError =
    error instanceof TypeError &&
    (error.message.includes('Failed to fetch dynamically imported module') ||
     error.message.includes('Importing a module script failed')) // Safari variant

  if (!isChunkError) return

  window.location.href = to?.fullPath ?? window.location.pathname
})

export default router
