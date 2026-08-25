import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { get } from 'svelte/store';
import { authStore } from './authStore.js';
import { tenantConfigStore } from './tenantConfigStore.js';

vi.mock('./tenantConfigStore.js', () => ({
  tenantConfigStore: {
    initSettings: vi.fn()
  }
}));

describe('authStore', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    authStore.logout();
  });

  afterEach(() => {
    global.fetch = originalFetch;
  });

  describe('token and session storage', () => {
    it('returns null token when unauthenticated', () => {
      expect(authStore.getToken()).toBeNull();
      expect(authStore.getTenantId()).toBeNull();
      const state = get(authStore);
      expect(state.isAuthenticated).toBe(false);
      expect(state.user).toBeNull();
    });

    it('logout clears local storage and resets state', () => {
      localStorage.setItem('tracklord_jwt_token', 'test-token');
      localStorage.setItem('tracklord_user_info', JSON.stringify({ id: 'u1', name: 'Admin' }));

      authStore.logout();

      expect(localStorage.getItem('tracklord_jwt_token')).toBeNull();
      expect(localStorage.getItem('tracklord_user_info')).toBeNull();
      const state = get(authStore);
      expect(state.token).toBeNull();
      expect(state.user).toBeNull();
      expect(state.isAuthenticated).toBe(false);
    });
  });

  describe('initAuth verification flow', () => {
    it('does nothing when no token exists in localStorage', async () => {
      await authStore.initAuth();
      const state = get(authStore);
      expect(state.isAuthenticated).toBe(false);
      expect(state.isLoading).toBe(false);
    });

    it('successfully restores session on 200 response from /auth/me', async () => {
      localStorage.setItem('tracklord_jwt_token', 'valid-jwt-token');

      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({
          id: 'user-1',
          tenantId: 'tenant-1',
          tenantName: 'Apex Textiles',
          name: 'John Doe',
          role: 'Admin',
          tenantSettings: { clientMode: 'lot' }
        })
      });

      await authStore.initAuth();

      const state = get(authStore);
      expect(state.isAuthenticated).toBe(true);
      expect(state.token).toBe('valid-jwt-token');
      expect(state.user.tenantName).toBe('Apex Textiles');
      expect(state.isLoading).toBe(false);
      expect(tenantConfigStore.initSettings).toHaveBeenCalledWith({ clientMode: 'lot' });
    });

    it('logs out user when server responds with 401 Unauthorized', async () => {
      localStorage.setItem('tracklord_jwt_token', 'expired-token');

      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 401
      });

      await authStore.initAuth();

      const state = get(authStore);
      expect(state.isAuthenticated).toBe(false);
      expect(state.token).toBeNull();
    });

    it('retains session on transient server errors (500 Internal Server Error)', async () => {
      localStorage.setItem('tracklord_jwt_token', 'stored-token');

      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 500
      });

      await authStore.initAuth();

      // Session should NOT be deleted on transient 500
      expect(localStorage.getItem('tracklord_jwt_token')).toBe('stored-token');
      const state = get(authStore);
      expect(state.isLoading).toBe(false);
    });

    it('retains session on network throw during check', async () => {
      localStorage.setItem('tracklord_jwt_token', 'stored-token');

      global.fetch = vi.fn().mockRejectedValue(new Error('Network offline'));

      await authStore.initAuth();

      expect(localStorage.getItem('tracklord_jwt_token')).toBe('stored-token');
      const state = get(authStore);
      expect(state.isLoading).toBe(false);
    });
  });

  describe('loginWithFirebaseToken', () => {
    it('authenticates with verify-otp endpoint and stores token/user', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({
          token: 'jwt-from-backend',
          user: {
            id: 'u-100',
            name: 'Operator 1',
            tenantId: 't-1',
            tenantSettings: { clientMode: 'step' }
          }
        })
      });

      const res = await authStore.loginWithFirebaseToken('firebase-id-token', '+919876543210');

      expect(res.token).toBe('jwt-from-backend');
      expect(localStorage.getItem('tracklord_jwt_token')).toBe('jwt-from-backend');

      const state = get(authStore);
      expect(state.isAuthenticated).toBe(true);
      expect(state.token).toBe('jwt-from-backend');
      expect(state.user.id).toBe('u-100');
      expect(tenantConfigStore.initSettings).toHaveBeenCalledWith({ clientMode: 'step' });
    });

    it('throws error and sets error in state when verify-otp fails', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({ message: 'Invalid OTP token' })
      });

      await expect(authStore.loginWithFirebaseToken('bad-token')).rejects.toThrow('Invalid OTP token');

      const state = get(authStore);
      expect(state.isAuthenticated).toBe(false);
      expect(state.error).toBe('Invalid OTP token');
      expect(state.isLoading).toBe(false);
    });
  });

  describe('modal visibility controls', () => {
    it('toggles showModal correctly', () => {
      authStore.openModal();
      expect(get(authStore).showModal).toBe(true);

      authStore.closeModal();
      expect(get(authStore).showModal).toBe(false);
    });
  });
});
