import { describe, it, expect, vi, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import { tenantConfigStore, isVisible } from './tenantConfigStore.js';
import * as api from '../api.js';

vi.mock('../api.js', () => ({
  getTenantSettings: vi.fn(),
  updateTenantSettings: vi.fn()
}));

describe('tenantConfigStore', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    tenantConfigStore.resetToDefaults();
  });

  describe('initSettings and state management', () => {
    it('initializes clientMode and featureOverrides from auth payload', () => {
      tenantConfigStore.initSettings({
        clientMode: 'lot',
        featureOverrides: { 'pages.dashboard': false }
      });

      const state = get(tenantConfigStore);
      expect(state.clientMode).toBe('lot');
      expect(state.featureOverrides['pages.dashboard']).toBe(false);
    });

    it('ignores null or undefined payload in initSettings', () => {
      const stateBefore = get(tenantConfigStore);
      tenantConfigStore.initSettings(null);
      tenantConfigStore.initSettings(undefined);
      const stateAfter = get(tenantConfigStore);

      expect(stateAfter.clientMode).toBe(stateBefore.clientMode);
    });
  });

  describe('isFeatureEnabled and isVisible', () => {
    it('returns default value (true) when feature key is not overridden', () => {
      expect(tenantConfigStore.isFeatureEnabled('pages.dashboard')).toBe(true);
      expect(isVisible('pages.dashboard')).toBe(true);
    });

    it('returns custom defaultValue when feature is unconfigured', () => {
      expect(tenantConfigStore.isFeatureEnabled('custom.unknown.key', false)).toBe(false);
    });

    it('respects explicitly set feature override', () => {
      tenantConfigStore.setFeature('forms.lots.create', false);
      expect(tenantConfigStore.isFeatureEnabled('forms.lots.create')).toBe(false);

      tenantConfigStore.setFeature('forms.lots.create', true);
      expect(tenantConfigStore.isFeatureEnabled('forms.lots.create')).toBe(true);
    });

    it('automatically disables pages.clients when clientMode is "none"', () => {
      tenantConfigStore.setClientMode('none');
      expect(tenantConfigStore.isFeatureEnabled('pages.clients')).toBe(false);
    });

    it('allows pages.clients when clientMode is "lot" or "step"', () => {
      tenantConfigStore.setClientMode('lot');
      expect(tenantConfigStore.isFeatureEnabled('pages.clients')).toBe(true);

      tenantConfigStore.setClientMode('step');
      expect(tenantConfigStore.isFeatureEnabled('pages.clients')).toBe(true);
    });
  });

  describe('isClientMode helper', () => {
    it('accurately tests against specific client mode', () => {
      tenantConfigStore.setClientMode('lot');
      expect(tenantConfigStore.isClientMode('lot')).toBe(true);
      expect(tenantConfigStore.isClientMode('step')).toBe(false);
      expect(tenantConfigStore.isClientMode('none')).toBe(false);
    });

    it('returns true for "any" when clientMode is not none', () => {
      tenantConfigStore.setClientMode('lot');
      expect(tenantConfigStore.isClientMode('any')).toBe(true);

      tenantConfigStore.setClientMode('step');
      expect(tenantConfigStore.isClientMode('any')).toBe(true);

      tenantConfigStore.setClientMode('none');
      expect(tenantConfigStore.isClientMode('any')).toBe(false);
    });
  });

  describe('mode transitions and side-effects', () => {
    it('switching to "lot" mode auto-enables client fields and pages', () => {
      tenantConfigStore.setClientMode('lot');
      const state = get(tenantConfigStore);
      expect(state.featureOverrides['fields.lots.client']).toBe(true);
      expect(state.featureOverrides['pages.clients']).toBe(true);
    });

    it('switching to "none" mode auto-disables client fields and pages', () => {
      tenantConfigStore.setClientMode('none');
      const state = get(tenantConfigStore);
      expect(state.featureOverrides['fields.lots.client']).toBe(false);
      expect(state.featureOverrides['pages.clients']).toBe(false);
    });

    it('enableAll sets all schema keys to true', () => {
      tenantConfigStore.enableAll();
      const state = get(tenantConfigStore);
      expect(state.featureOverrides['pages.dashboard']).toBe(true);
      expect(state.featureOverrides['forms.lots.import']).toBe(true);
      expect(state.featureOverrides['fields.inventory.cost']).toBe(true);
    });
  });

  describe('backend synchronization (loadSettings and saveSettings)', () => {
    it('loadSettings fetches settings from backend and updates store', async () => {
      vi.mocked(api.getTenantSettings).mockResolvedValue({
        clientMode: 'lot',
        featureOverrides: { 'pages.inventory': false }
      });

      await tenantConfigStore.loadSettings();
      const state = get(tenantConfigStore);
      expect(state.clientMode).toBe('lot');
      expect(state.featureOverrides['pages.inventory']).toBe(false);
      expect(state.isLoading).toBe(false);
    });

    it('saveSettings sends current state to API and toggles saveSuccess', async () => {
      vi.mocked(api.updateTenantSettings).mockResolvedValue({ success: true });

      tenantConfigStore.setClientMode('lot');
      await tenantConfigStore.saveSettings();

      expect(api.updateTenantSettings).toHaveBeenCalledWith({
        clientMode: 'lot',
        featureOverrides: expect.any(Object)
      });

      const state = get(tenantConfigStore);
      expect(state.isSaving).toBe(false);
      expect(state.saveSuccess).toBe(true);
    });

    it('saveSettings handles API errors gracefully', async () => {
      vi.mocked(api.updateTenantSettings).mockRejectedValue(new Error('Save failed'));

      await expect(tenantConfigStore.saveSettings()).rejects.toThrow('Save failed');

      const state = get(tenantConfigStore);
      expect(state.isSaving).toBe(false);
      expect(state.errorMessage).toBe('Save failed');
    });
  });

  describe('modal open / close lifecycle', () => {
    it('toggles isModalOpen properly', () => {
      tenantConfigStore.openModal();
      expect(get(tenantConfigStore).isModalOpen).toBe(true);

      tenantConfigStore.closeModal();
      expect(get(tenantConfigStore).isModalOpen).toBe(false);
    });
  });
});
