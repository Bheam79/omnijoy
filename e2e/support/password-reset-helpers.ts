/**
 * Helpers for the password-reset E2E specs.
 *
 * The `/api/auth/password/forgot` flow stores only the SHA-256 hash of the
 * plaintext OTP code in the `OtpCodes` table (column `CodeHash`), never the
 * plaintext.  In production this is sent to the user via email — but the
 * E2E suite has no test inbox to scrape.  Rather than introduce a
 * dev-only endpoint that leaks plaintext (or maintain a custom
 * test-email-capture service), this helper:
 *
 *   1. Reads the most-recent unused `OtpCode` row for the given email
 *      via `docker exec ... mariadb`  (same pattern global-setup uses
 *      to promote the admin seed user).
 *
 *   2. Brute-forces the 6-digit search space (100 000 – 999 999) once
 *      per Node process — every code is hashed, table is cached, then
 *      hash lookups are O(1).  Building the table takes ~1 second.
 *
 * The hashing algorithm mirrors `AuthService.HashCode`:
 *
 *   ```csharp
 *   var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
 *   return Convert.ToBase64String(bytes);
 *   ```
 *
 * which is equivalent to Node's
 * `crypto.createHash('sha256').update(code, 'utf8').digest('base64')`.
 */

import { execSync } from 'child_process'
import { createHash } from 'crypto'

const MYSQL_CONTAINER = '07ad0b82_omnijoy_mysql'
const MYSQL_USER      = 'omnijoy'
const MYSQL_PASS      = 'omnijoy_pass'
const MYSQL_DB        = 'omnijoy'

/** Mirrors `AuthService.HashCode` exactly. */
function hashCode(code: string): string {
  return createHash('sha256').update(code, 'utf8').digest('base64')
}

let codeTable: Map<string, string> | null = null

/**
 * Build a hash → plaintext lookup table for every 6-digit code.
 * Built lazily, once per Node process (≈ 1s on a modern CPU).
 */
function buildCodeTable(): Map<string, string> {
  if (codeTable) return codeTable
  const map = new Map<string, string>()
  for (let i = 100_000; i < 1_000_000; i++) {
    const code = String(i)
    map.set(hashCode(code), code)
  }
  codeTable = map
  return map
}

/**
 * Returns the most-recent unused PasswordReset code (purpose=1) for the
 * given email, or `null` if no such row exists.
 *
 * Polls a few times to absorb the small window between the API returning
 * and the row being visible to a sibling client (negligible in practice,
 * but cheap insurance against test flakes).
 */
export function fetchLatestPasswordResetCode(email: string): string | null {
  // Email is restricted by the API itself to `\w@\w` patterns and is
  // lowercased server-side — no SQL-injection vector here, but be
  // defensive anyway: refuse anything containing a quote or backslash.
  if (/['"\\]/.test(email)) {
    throw new Error(`Refusing to query for suspicious email: ${email}`)
  }

  const sql =
    `SELECT CodeHash FROM OtpCodes ` +
    `WHERE Email='${email.toLowerCase()}' AND Purpose=1 AND IsUsed=0 ` +
    `ORDER BY CreatedAt DESC LIMIT 1;`

  for (let attempt = 0; attempt < 5; attempt++) {
    const out = execSync(
      `docker exec ${MYSQL_CONTAINER} mariadb -u${MYSQL_USER} -p${MYSQL_PASS} ${MYSQL_DB} -N -B -e "${sql}"`,
      { stdio: ['ignore', 'pipe', 'pipe'] },
    ).toString().trim()

    if (out) {
      const codeHash = out.split('\n')[0].trim()
      const code = buildCodeTable().get(codeHash)
      if (code) return code
      // Hash was returned but not found in the table — that should never
      // happen (the search space covers every 6-digit code).  Surface it
      // loudly rather than silently returning null.
      throw new Error(
        `Password-reset hash for ${email} did not match any 6-digit code. ` +
        `Hash=${codeHash} — has AuthService.HashCode changed?`,
      )
    }

    // Row not yet visible — give the DB ~50ms and retry.
    const until = Date.now() + 50
    while (Date.now() < until) { /* spin */ }
  }

  return null
}
