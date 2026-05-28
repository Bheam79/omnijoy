import { APIRequestContext } from '@playwright/test'

/**
 * Thin wrapper around Playwright's request context for authenticated
 * API calls.  Pass the base URL and a resolved access token.
 */
export class ApiClient {
  constructor(
    private readonly request: APIRequestContext,
    public readonly baseUrl: string,
    private token: string | null = null,
  ) {}

  private headers(): Record<string, string> {
    return this.token ? { Authorization: `Bearer ${this.token}` } : {}
  }

  setToken(token: string) { this.token = token }

  async register(payload: {
    email: string
    displayName: string
    authMethod: 'password' | 'otp'
    password?: string
    gender?: string
    birthDate?: string
  }) {
    return this.request.post(`${this.baseUrl}/api/auth/register`, {
      data: payload,
    })
  }

  async login(email: string, password: string) {
    return this.request.post(`${this.baseUrl}/api/auth/login`, {
      data: { email, password },
    })
  }

  async loginAndSetToken(email: string, password: string): Promise<string> {
    const resp = await this.login(email, password)
    const body = await resp.json()
    this.token = body.accessToken
    return body.accessToken
  }

  async get(path: string, params?: Record<string, string>) {
    return this.request.get(`${this.baseUrl}${path}`, {
      headers: this.headers(),
      params,
    })
  }

  async post(path: string, data?: unknown) {
    return this.request.post(`${this.baseUrl}${path}`, {
      headers: this.headers(),
      data,
    })
  }

  async put(path: string, data?: unknown) {
    return this.request.put(`${this.baseUrl}${path}`, {
      headers: this.headers(),
      data,
    })
  }

  async delete(path: string) {
    return this.request.delete(`${this.baseUrl}${path}`, {
      headers: this.headers(),
    })
  }

  async postForm(path: string, form: Record<string, string | { name: string; mimeType: string; buffer: Buffer }>) {
    return this.request.post(`${this.baseUrl}${path}`, {
      headers: this.headers(),
      multipart: form,
    })
  }
}
