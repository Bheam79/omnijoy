import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    // Public routes
    {
      path: '/',
      name: 'home',
      component: () => import('@/views/HomeView.vue'),
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

    // Authenticated routes
    {
      path: '/wall',
      name: 'wall',
      component: () => import('@/views/feed/WallView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/profile/:userId',
      name: 'profile',
      component: () => import('@/views/profile/ProfileView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/events',
      name: 'events',
      component: () => import('@/views/events/EventsView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/events/:id',
      name: 'event-detail',
      component: () => import('@/views/events/EventDetailView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/company/:id',
      name: 'company',
      component: () => import('@/views/company/CompanyView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/live',
      name: 'live',
      component: () => import('@/views/live/LiveView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/live/:id',
      name: 'live-stream',
      component: () => import('@/views/live/LiveStreamView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/settings',
      name: 'settings',
      component: () => import('@/views/settings/SettingsView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/settings/privacy',
      name: 'settings-privacy',
      component: () => import('@/views/settings/PrivacySettingsView.vue'),
      meta: { requiresAuth: true },
    },

    // Public share routes (for OG meta tag rendering)
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

    // 404
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/views/NotFoundView.vue'),
    },
  ],
})

// Navigation guards
router.beforeEach((to) => {
  const auth = useAuthStore()

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  if (to.meta.guest && auth.isAuthenticated) {
    return { name: 'wall' }
  }
})

export default router
