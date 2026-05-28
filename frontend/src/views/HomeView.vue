<template>
  <div class="min-h-screen flex flex-col items-center justify-center bg-gray-50">
    <h1 class="text-4xl font-bold text-gray-900 mb-4">Welcome to Omnijoy</h1>
    <p class="text-lg text-gray-600 mb-8">A social platform without ads or forced content.</p>
    <div class="flex gap-4">
      <router-link to="/login" class="px-6 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700">Log In</router-link>
      <router-link to="/register" class="px-6 py-2 border border-blue-600 text-blue-600 rounded-lg hover:bg-blue-50">Sign Up</router-link>
    </div>
  </div>
</template>
