/**
 * Test fixture seed data.
 *
 * These constants define the users, passwords, and shared references
 * used throughout the E2E test suite.  The `globalSetup` fixture
 * registers all users via the API before any test runs.
 */

export interface SeedUser {
  email: string
  password: string
  displayName: string
  gender: string
  birthDate: string
  locationCountry?: string
  locationCountryCode?: string
  locationCity?: string
  locationName?: string
}

/**
 * Minimal location payload required by the registration endpoint.
 * Spread into any inline register call: `{ ...TEST_LOCATION, ... }`.
 */
export const TEST_LOCATION = {
  locationCountry: 'Norway',
  locationCountryCode: 'NO',
  locationCity: 'Oslo',
  locationName: 'Oslo, Norway',
} as const

export const SEED: Record<string, SeedUser> & {
  user1: SeedUser
  user2: SeedUser
  user3: SeedUser
  admin: SeedUser
} = {
  /** Primary test user — used for most browser flows */
  user1: {
    email: 'e2e_alice@omnijoy.test',
    password: 'Test@12345!',
    displayName: 'Alice E2E',
    gender: 'Female',
    birthDate: '1990-01-15',
    ...TEST_LOCATION,
  },
  /** Secondary user — friend / conversation partner */
  user2: {
    email: 'e2e_bob@omnijoy.test',
    password: 'Test@12345!',
    displayName: 'Bob E2E',
    gender: 'Male',
    birthDate: '1988-03-22',
    ...TEST_LOCATION,
  },
  /** Third user — used for privacy / block tests */
  user3: {
    email: 'e2e_carol@omnijoy.test',
    password: 'Test@12345!',
    displayName: 'Carol E2E',
    gender: 'NotDisclosed',
    birthDate: '1995-07-04',
    ...TEST_LOCATION,
  },
  /**
   * Admin user — promoted to the Admin platform role by global-setup via a
   * direct DB update so that admin/moderator endpoint tests work without
   * circular bootstrapping through the API.
   */
  admin: {
    email: 'e2e_admin@omnijoy.test',
    password: 'Test@12345!',
    displayName: 'Admin E2E',
    gender: 'NotDisclosed',
    birthDate: '1985-06-15',
    ...TEST_LOCATION,
  },
}
