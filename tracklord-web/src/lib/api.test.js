import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as api from './api.js';
import { authStore } from './stores/authStore.js';

vi.mock('./stores/authStore.js', () => ({
  authStore: {
    getToken: vi.fn(),
    logout: vi.fn(),
    openModal: vi.fn()
  }
}));

describe('api.js client library', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    vi.clearAllMocks();
    api.invalidateCache();
  });

  afterEach(() => {
    global.fetch = originalFetch;
  });

  describe('authFetch behavior and token injection', () => {
    it('injects Authorization Bearer token header when token is available', async () => {
      vi.mocked(authStore.getToken).mockReturnValue('mock-jwt-token-123');
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => [{ id: '1', name: 'Cutting' }]
      });

      await api.getManufacturingSteps();

      expect(global.fetch).toHaveBeenCalledTimes(1);
      const [url, options] = vi.mocked(global.fetch).mock.calls[0];
      expect(url).toContain('/ManufacturingSteps');
      expect(options.headers.get('Authorization')).toBe('Bearer mock-jwt-token-123');
    });

    it('does not inject Authorization header if no token is present', async () => {
      vi.mocked(authStore.getToken).mockReturnValue(null);
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => []
      });

      await api.getProductionLots();

      const [, options] = vi.mocked(global.fetch).mock.calls[0];
      expect(options.headers.has('Authorization')).toBe(false);
    });

    it('calls authStore.logout() and authStore.openModal() on 401 response', async () => {
      vi.mocked(authStore.getToken).mockReturnValue('expired-token');
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
        statusText: 'Unauthorized',
        json: async () => ({ message: 'Token has expired' })
      });

      await expect(api.getManufacturingSteps()).rejects.toThrow('Token has expired');
      expect(authStore.logout).toHaveBeenCalledTimes(1);
      expect(authStore.openModal).toHaveBeenCalledTimes(1);
    });
  });

  describe('handleResponse error and payload parsing', () => {
    it('returns parsed json on 200 OK', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ success: true, count: 42 })
      });

      const res = await api.getDashboardOverview();
      expect(res).toEqual({ success: true, count: 42 });
    });

    it('returns null on 204 No Content', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204
      });

      const res = await api.deleteProductionLot('lot-123');
      expect(res).toBeNull();
    });

    it('throws error message from errorData.message when response is not ok', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        json: async () => ({ message: 'Invalid lot quantity specified' })
      });

      await expect(api.createProductionLot({ lotNumber: 'L-1' })).rejects.toThrow('Invalid lot quantity specified');
    });

    it('throws error string directly if errorData is a string', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        json: async () => 'Custom error string payload'
      });

      await expect(api.createClient({ name: 'Apex' })).rejects.toThrow('Custom error string payload');
    });

    it('falls back to HTTP status text if json parsing fails on non-ok response', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        json: async () => { throw new Error('Not JSON'); }
      });

      await expect(api.getUsers()).rejects.toThrow('HTTP Error 500: Internal Server Error');
    });
  });

  describe('validation guards and endpoints', () => {
    it('editMaterialConsumption throws immediately if consumptionId is missing or "undefined"', async () => {
      await expect(api.editMaterialConsumption('', {})).rejects.toThrow('Material Consumption ID is missing or invalid.');
      await expect(api.editMaterialConsumption('undefined', {})).rejects.toThrow('Material Consumption ID is missing or invalid.');
      await expect(api.editMaterialConsumption(null, {})).rejects.toThrow('Material Consumption ID is missing or invalid.');
    });

    it('createManufacturingStep sends POST with JSON payload', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ id: 's-1', code: 'CUT' })
      });

      await api.createManufacturingStep({ code: 'CUT', name: 'Cutting' });
      const [url, options] = vi.mocked(global.fetch).mock.calls[0];
      expect(url).toContain('/ManufacturingSteps');
      expect(options.method).toBe('POST');
      expect(options.body).toBe(JSON.stringify({ code: 'CUT', name: 'Cutting' }));
    });

    it('reorderManufacturingSteps sends PUT to /ManufacturingSteps/reorder', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ success: true })
      });

      await api.reorderManufacturingSteps([{ id: 's-1', sequenceOrder: 1 }]);
      const [url, options] = vi.mocked(global.fetch).mock.calls[0];
      expect(url).toContain('/ManufacturingSteps/reorder');
      expect(options.method).toBe('PUT');
      expect(JSON.parse(options.body)).toEqual({ steps: [{ id: 's-1', sequenceOrder: 1 }] });
    });

    it('reverseMovement sends POST with reason and user payload', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ success: true })
      });

      await api.reverseMovement('mov-1', { reason: 'Accidental move', user: 'Supervisor' });
      const [url, options] = vi.mocked(global.fetch).mock.calls[0];
      expect(url).toContain('/Movements/mov-1/reverse');
      expect(options.method).toBe('POST');
    });

    it('getBatchLotJourneys sends POST with lotIds to /ProductionLots/journeys/batch', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ 'lot-1': { activeBalances: [] } })
      });

      const res = await api.getBatchLotJourneys(['lot-1', 'lot-2']);
      expect(global.fetch).toHaveBeenCalledTimes(1);
      const [url, options] = vi.mocked(global.fetch).mock.calls[0];
      expect(url).toContain('/ProductionLots/journeys/batch');
      expect(options.method).toBe('POST');
      expect(JSON.parse(options.body)).toEqual({ lotIds: ['lot-1', 'lot-2'] });
      expect(res).toEqual({ 'lot-1': { activeBalances: [] } });
    });

    it('getBatchLotJourneys returns empty object immediately for empty array without network request', async () => {
      global.fetch = vi.fn();
      const res = await api.getBatchLotJourneys([]);
      expect(global.fetch).not.toHaveBeenCalled();
      expect(res).toEqual({});
    });

    it('caches getManufacturingSteps and deduplicates concurrent calls', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => [{ id: 's1', name: 'Step 1' }]
      });

      const [res1, res2] = await Promise.all([
        api.getManufacturingSteps(),
        api.getManufacturingSteps()
      ]);

      expect(global.fetch).toHaveBeenCalledTimes(1);
      expect(res1).toEqual(res2);
    });

    it('invalidates steps cache on mutation', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => [{ id: 's1' }]
      });

      await api.getManufacturingSteps();
      expect(global.fetch).toHaveBeenCalledTimes(1);

      // Create step mutates and should invalidate cache
      await api.createManufacturingStep({ code: 'WASH', name: 'Washing' });
      expect(global.fetch).toHaveBeenCalledTimes(2);

      // Next read should fetch fresh
      await api.getManufacturingSteps();
      expect(global.fetch).toHaveBeenCalledTimes(3);
    });
  });
});
