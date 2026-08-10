import { writable, get } from 'svelte/store';

const TOKEN_KEY = 'tracklord_jwt_token';
const USER_KEY = 'tracklord_user_info';

function createAuthStore() {
  const initialToken = typeof window !== 'undefined' ? localStorage.getItem(TOKEN_KEY) : null;
  const initialUser = typeof window !== 'undefined' ? JSON.parse(localStorage.getItem(USER_KEY) || 'null') : null;

  const store = writable({
    token: initialToken,
    user: initialUser,
    isAuthenticated: !!initialToken,
    isLoading: false,
    showModal: false,
    error: null
  });

  const { subscribe, set, update } = store;

  return {
    subscribe,

    /**
     * Initializes auth state and checks token validity with backend.
     */
    async initAuth() {
      if (typeof window === 'undefined') return;

      const token = localStorage.getItem(TOKEN_KEY);
      if (!token) {
        update(s => ({ ...s, isAuthenticated: false, user: null, token: null }));
        return;
      }

      update(s => ({ ...s, isLoading: true }));

      try {
        const API_BASE = import.meta.env.VITE_API_BASE || '/api';
        const res = await fetch(`${API_BASE}/auth/me`, {
          headers: {
            'Authorization': `Bearer ${token}`
          }
        });

        if (res.ok) {
          const data = await res.json();
          const user = {
            id: data.id,
            userId: data.id,
            tenantId: data.tenantId,
            phoneNumber: data.phoneNumber,
            name: data.name,
            role: data.role
          };
          localStorage.setItem(USER_KEY, JSON.stringify(user));
          update(s => ({
            ...s,
            token,
            user,
            isAuthenticated: true,
            isLoading: false,
            error: null
          }));
        } else {
          // Token expired or invalid
          this.logout();
        }
      } catch (err) {
        console.warn('Network error checking auth status, retaining local token if present:', err);
        update(s => ({ ...s, isLoading: false }));
      }
    },

    /**
     * Authenticates with Backend using Firebase ID token and obtains custom JWT.
     * @param {string} idToken - Firebase ID token
     * @param {string} [phoneNumber] - User phone number
     */
    async loginWithFirebaseToken(idToken, phoneNumber) {
      update(s => ({ ...s, isLoading: true, error: null }));

      try {
        const API_BASE = import.meta.env.VITE_API_BASE || '/api';
        const response = await fetch(`${API_BASE}/auth/verify-otp`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ idToken, phoneNumber })
        });

        if (!response.ok) {
          const errData = await response.json().catch(() => ({}));
          throw new Error(errData.message || 'Authentication with backend failed.');
        }

        const data = await response.json();
        const { token, user } = data;

        localStorage.setItem(TOKEN_KEY, token);
        localStorage.setItem(USER_KEY, JSON.stringify(user));

        update(s => ({
          ...s,
          token,
          user,
          isAuthenticated: true,
          isLoading: false,
          showModal: false,
          error: null
        }));

        return data;
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Login failed.';
        update(s => ({ ...s, isLoading: false, error: message }));
        throw err;
      }
    },

    /**
     * Logs out the user and clears tokens.
     */
    logout() {
      if (typeof window !== 'undefined') {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(USER_KEY);
      }
      set({
        token: null,
        user: null,
        isAuthenticated: false,
        isLoading: false,
        showModal: false,
        error: null
      });
    },

    /** Gets raw token string */
    getToken() {
      const state = get(store);
      return state.token || (typeof window !== 'undefined' ? localStorage.getItem(TOKEN_KEY) : null);
    },

    /** Gets current user's tenantId */
    getTenantId() {
      const state = get(store);
      return state.user?.tenantId || null;
    },

    openModal() {
      update(s => ({ ...s, showModal: true }));
    },

    closeModal() {
      update(s => ({ ...s, showModal: false }));
    }
  };
}

export const authStore = createAuthStore();
