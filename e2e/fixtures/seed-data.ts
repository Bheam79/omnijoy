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
}

export const SEED: Record<string, SeedUser> & {
  user1: SeedUser
  user2: SeedUser
  user3: SeedUser
} = {
  /** Primary test user — used for most browser flows */
  user1: {
    email: 'e2e_alice@omnijoy.test',
    password: 'Test@12345!',
    displayName: 'Alice E2E',
    gender: 'Female',
    birthDate: '1990-01-15',
  },
  /** Secondary user — friend / conversation partner */
  user2: {
    email: 'e2e_bob@omnijoy.test',
    password: 'Test@12345!',
    displayName: 'Bob E2E',
    gender: 'Male',
    birthDate: '1988-03-22',
  },
  /** Third user — used for privacy / block tests */
  user3: {
    email: 'e2e_carol@omnijoy.test',
    password: 'Test@12345!',
    displayName: 'Carol E2E',
    gender: 'NotDisclosed',
    birthDate: '1995-07-04',
  },
}
