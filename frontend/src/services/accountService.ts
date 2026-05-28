import api from './api'

export interface ChangeEmailPayload {
  newEmail: string
  currentPassword: string
}

export interface ChangePasswordPayload {
  currentPassword: string
  newPassword: string
  confirmNewPassword: string
}

export const accountService = {
  async changeEmail(payload: ChangeEmailPayload): Promise<void> {
    await api.post('/api/account/change-email', payload)
  },

  async changePassword(payload: ChangePasswordPayload): Promise<void> {
    await api.post('/api/account/change-password', payload)
  },
}
