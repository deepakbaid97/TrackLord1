import { writable, get } from 'svelte/store';
import { getTenantSettings, updateTenantSettings } from '../api.js';
import { CONFIG_SCHEMA } from '../configRegistry.js';

const SETTINGS_STORAGE_KEY = 'tracklord_tenant_config';

function createTenantConfigStore() {
  // Load cached settings if available
  let initialClientMode = 'step';
  let initialOverrides = {};

  if (typeof window !== 'undefined') {
    try {
      const cached = JSON.parse(localStorage.getItem(SETTINGS_STORAGE_KEY) || 'null');
      if (cached) {
        if (cached.clientMode) initialClientMode = cached.clientMode;
        if (cached.featureOverrides) initialOverrides = cached.featureOverrides;
      }
    } catch {
      // ignore parse errors
    }
  }

  const { subscribe, set, update } = writable({
    clientMode: initialClientMode, // 'lot' | 'step' | 'none'
    featureOverrides: initialOverrides,
    isModalOpen: false,
    isLoading: false,
    isSaving: false,
    saveSuccess: false,
    errorMessage: null
  });

  function persistLocally(clientMode, featureOverrides) {
    if (typeof window !== 'undefined') {
      try {
        localStorage.setItem(
          SETTINGS_STORAGE_KEY,
          JSON.stringify({ clientMode, featureOverrides })
        );
      } catch (err) {
        console.warn('Failed to persist tenant config to localStorage:', err);
      }
    }
  }

  return {
    subscribe,

    /**
     * Initializes config from user auth payload without needing an extra roundtrip.
     * @param {{ clientMode?: string, featureOverrides?: Record<string, boolean> }} [settings] 
     */
    initSettings(settings) {
      if (!settings) return;
      const clientMode = settings.clientMode || 'step';
      const featureOverrides = settings.featureOverrides || {};

      persistLocally(clientMode, featureOverrides);
      update(s => ({
        ...s,
        clientMode,
        featureOverrides: { ...featureOverrides },
        isLoading: false
      }));
    },

    /**
     * Fetches current settings directly from backend API.
     */
    async loadSettings() {
      update(s => ({ ...s, isLoading: true, errorMessage: null }));
      try {
        const data = await getTenantSettings();
        if (data) {
          const clientMode = data.clientMode || 'step';
          const featureOverrides = data.featureOverrides || {};
          persistLocally(clientMode, featureOverrides);
          update(s => ({
            ...s,
            clientMode,
            featureOverrides,
            isLoading: false
          }));
        }
      } catch (err) {
        console.warn('Could not fetch tenant settings from API:', err);
        update(s => ({ ...s, isLoading: false }));
      }
    },

    /**
     * Checks if a specific feature key is enabled.
     * Defaults to true if unconfigured.
     * @param {string} key 
     * @param {boolean} [defaultValue=true]
     * @returns {boolean}
     */
    isFeatureEnabled(key, defaultValue = true) {
      const state = get({ subscribe });
      if (!key) return defaultValue;

      // Check for disabled client master page if client tracking is 'none'
      if (key === 'pages.clients' && state.clientMode === 'none') {
        return false;
      }

      if (state.featureOverrides && typeof state.featureOverrides[key] === 'boolean') {
        return state.featureOverrides[key];
      }
      return defaultValue;
    },

    /**
     * Checks if current tenant operates in the given client tracking mode.
     * @param {'lot' | 'step' | 'none' | 'any'} mode 
     * @returns {boolean}
     */
    isClientMode(mode) {
      const state = get({ subscribe });
      if (mode === 'any') return state.clientMode !== 'none';
      return state.clientMode === mode;
    },

    /**
     * Sets client tracking mode in local state.
     * @param {'lot' | 'step' | 'none'} mode 
     */
    setClientMode(mode) {
      update(s => {
        const overrides = { ...s.featureOverrides };
        // Automatically sync dependent field flags based on mode
        if (mode === 'lot') {
          overrides['fields.lots.client'] = true;
          overrides['pages.clients'] = true;
        } else if (mode === 'none') {
          overrides['fields.lots.client'] = false;
          overrides['pages.clients'] = false;
        }
        return {
          ...s,
          clientMode: mode,
          featureOverrides: overrides
        };
      });
    },

    /**
     * Toggles an individual feature key in local state.
     * @param {string} key 
     * @param {boolean} value 
     */
    setFeature(key, value) {
      update(s => ({
        ...s,
        featureOverrides: {
          ...s.featureOverrides,
          [key]: value
        }
      }));
    },

    /**
     * Resets all feature overrides to default (all enabled) with step-level mode.
     */
    resetToDefaults() {
      update(s => ({
        ...s,
        clientMode: 'step',
        featureOverrides: {}
      }));
    },

    /**
     * Enables all features across all categories.
     */
    enableAll() {
      /** @type {Record<string, boolean>} */
      const overrides = {};
      for (const cat of CONFIG_SCHEMA) {
        for (const item of cat.items) {
          overrides[item.key] = true;
        }
      }
      update(s => ({
        ...s,
        featureOverrides: overrides
      }));
    },

    /**
     * Saves current configuration to the backend server.
     */
    async saveSettings() {
      const state = get({ subscribe });
      update(s => ({ ...s, isSaving: true, saveSuccess: false, errorMessage: null }));

      try {
        await updateTenantSettings({
          clientMode: state.clientMode,
          featureOverrides: state.featureOverrides
        });

        persistLocally(state.clientMode, state.featureOverrides);

        update(s => ({
          ...s,
          isSaving: false,
          saveSuccess: true
        }));

        setTimeout(() => {
          update(s => ({ ...s, saveSuccess: false }));
        }, 3000);
      } catch (err) {
        const msg = err instanceof Error ? err.message : 'Failed to save settings.';
        update(s => ({ ...s, isSaving: false, errorMessage: msg }));
        throw err;
      }
    },

    openModal() {
      update(s => ({ ...s, isModalOpen: true, saveSuccess: false, errorMessage: null }));
    },

    closeModal() {
      update(s => ({ ...s, isModalOpen: false, saveSuccess: false, errorMessage: null }));
    }
  };
}

export const tenantConfigStore = createTenantConfigStore();

/**
 * Universal functional shorthand for Svelte template or script usage.
 * @param {string} key 
 * @param {boolean} [defaultVal=true]
 */
export function isVisible(key, defaultVal = true) {
  return tenantConfigStore.isFeatureEnabled(key, defaultVal);
}
