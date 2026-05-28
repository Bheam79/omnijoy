import api from './api'

// ── DTOs (mirrors backend CompanyPageDtos.cs) ─────────────────────────────────

export interface CompanyPageCreator {
  id: string
  displayName: string
  avatarUrl?: string
}

export interface CompanyPageAdminDto {
  userId: string
  displayName: string
  avatarUrl?: string
  role: 'Owner' | 'Admin' | 'Editor'
}

export interface CompanyPageDto {
  id: string
  name: string
  description?: string
  logoUrl?: string
  coverUrl?: string
  createdBy: CompanyPageCreator
  followerCount: number
  isFollowing: boolean
  myRole?: 'Owner' | 'Admin' | 'Editor'
  createdAt: string
}

export interface CompanyPagesPageResult {
  items: CompanyPageDto[]
  page: number
  pageSize: number
  hasMore: boolean
}

export interface AdminsResult {
  admins: CompanyPageAdminDto[]
}

export interface CreatePagePayload {
  name: string
  description?: string
  logo?: File
  cover?: File
}

export interface UpdatePagePayload {
  name?: string
  description?: string
  logo?: File
  cover?: File
}

export interface AddAdminPayload {
  userId: string
  role: 'Admin' | 'Editor'
}

// ── Service ───────────────────────────────────────────────────────────────────

export const companyPageService = {
  async createPage(payload: CreatePagePayload): Promise<CompanyPageDto> {
    const form = new FormData()
    form.append('name', payload.name)
    if (payload.description) form.append('description', payload.description)
    if (payload.logo)        form.append('logo',        payload.logo)
    if (payload.cover)       form.append('cover',       payload.cover)

    const { data } = await api.post<CompanyPageDto>('/api/company-pages', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async getPages(params: { mine?: boolean; page?: number; pageSize?: number } = {}): Promise<CompanyPagesPageResult> {
    const { data } = await api.get<CompanyPagesPageResult>('/api/company-pages', {
      params: {
        mine:     params.mine     ?? false,
        page:     params.page     ?? 1,
        pageSize: params.pageSize ?? 20,
      },
    })
    return data
  },

  async getMyAdminPages(): Promise<CompanyPageDto[]> {
    const { data } = await api.get<CompanyPageDto[]>('/api/company-pages/mine')
    return data
  },

  async getPage(id: string): Promise<CompanyPageDto> {
    const { data } = await api.get<CompanyPageDto>(`/api/company-pages/${id}`)
    return data
  },

  async updatePage(id: string, payload: UpdatePagePayload): Promise<CompanyPageDto> {
    const form = new FormData()
    if (payload.name        !== undefined) form.append('name',        payload.name)
    if (payload.description !== undefined) form.append('description', payload.description ?? '')
    if (payload.logo)                      form.append('logo',        payload.logo)
    if (payload.cover)                     form.append('cover',       payload.cover)

    const { data } = await api.put<CompanyPageDto>(`/api/company-pages/${id}`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async getAdmins(id: string): Promise<AdminsResult> {
    const { data } = await api.get<AdminsResult>(`/api/company-pages/${id}/admins`)
    return data
  },

  async addAdmin(pageId: string, payload: AddAdminPayload): Promise<AdminsResult> {
    const { data } = await api.post<AdminsResult>(`/api/company-pages/${pageId}/admins`, payload)
    return data
  },

  async removeAdmin(pageId: string, userId: string): Promise<AdminsResult> {
    const { data } = await api.delete<AdminsResult>(`/api/company-pages/${pageId}/admins/${userId}`)
    return data
  },

  async follow(id: string): Promise<CompanyPageDto> {
    const { data } = await api.post<CompanyPageDto>(`/api/company-pages/${id}/follow`)
    return data
  },

  async unfollow(id: string): Promise<CompanyPageDto> {
    const { data } = await api.delete<CompanyPageDto>(`/api/company-pages/${id}/follow`)
    return data
  },
}
