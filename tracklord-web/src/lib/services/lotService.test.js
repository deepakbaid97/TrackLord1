import { describe, it, expect, vi, beforeEach } from 'vitest';
import { STEP_ICON_MAP, STEP_COLOR_MAP, enrichLotsWithJourneys } from './lotService.js';
import * as api from '../api.js';

vi.mock('../api.js', () => ({
  getLotJourney: vi.fn()
}));

describe('lotService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('STEP_ICON_MAP', () => {
    it('returns correct icons for known step codes', () => {
      expect(STEP_ICON_MAP['CUT']).toBe('content_cut');
      expect(STEP_ICON_MAP['STITCH']).toBe('webhook');
      expect(STEP_ICON_MAP['WASH']).toBe('local_laundry_service');
      expect(STEP_ICON_MAP['FIN']).toBe('iron');
      expect(STEP_ICON_MAP['PACK']).toBe('inventory_2');
      expect(STEP_ICON_MAP['IMPORT']).toBe('file_upload');
    });

    it('returns undefined for unknown step codes', () => {
      expect(STEP_ICON_MAP['UNKNOWN']).toBeUndefined();
      expect(STEP_ICON_MAP['']).toBeUndefined();
    });
  });

  describe('STEP_COLOR_MAP', () => {
    it('returns correct tailwind color classes for known step codes', () => {
      expect(STEP_COLOR_MAP['CUT']).toBe('bg-blue-500');
      expect(STEP_COLOR_MAP['STITCH']).toBe('bg-violet-500');
      expect(STEP_COLOR_MAP['WASH']).toBe('bg-cyan-500');
      expect(STEP_COLOR_MAP['FIN']).toBe('bg-amber-500');
      expect(STEP_COLOR_MAP['PACK']).toBe('bg-emerald-500');
    });

    it('does not define a color for IMPORT code', () => {
      expect(STEP_COLOR_MAP['IMPORT']).toBeUndefined();
    });
  });

  describe('enrichLotsWithJourneys', () => {
    const mockSteps = [
      { id: 'step-1', code: 'CUT', name: 'Cutting', isTerminating: false },
      { id: 'step-2', code: 'STITCH', name: 'Stitching', isTerminating: false },
      { id: 'step-3', code: 'PACK', name: 'Packing', isTerminating: true }
    ];

    it('enriches lots with journey data and marks isTerminated=false when active balances are at non-terminating steps', async () => {
      vi.mocked(api.getLotJourney).mockResolvedValue({
        activeBalances: [
          { stepId: 'step-1', stepCode: 'CUT', quantity: 50 }
        ]
      });

      const rawLots = [{ id: 'lot-1', lotNumber: 'L-001' }];
      const result = await enrichLotsWithJourneys(rawLots, mockSteps);

      expect(result).toHaveLength(1);
      expect(result[0].id).toBe('lot-1');
      expect(result[0].isTerminated).toBe(false);
      expect(result[0].activeBalances).toHaveLength(1);
      expect(result[0].activeBalances[0].quantity).toBe(50);
    });

    it('marks isTerminated=true when all active balances are at terminating steps by stepId', async () => {
      vi.mocked(api.getLotJourney).mockResolvedValue({
        activeBalances: [
          { stepId: 'step-3', stepCode: 'PACK', quantity: 100 }
        ]
      });

      const rawLots = [{ id: 'lot-2', lotNumber: 'L-002' }];
      const result = await enrichLotsWithJourneys(rawLots, mockSteps);

      expect(result[0].isTerminated).toBe(true);
    });

    it('marks isTerminated=true when matched by stepCode case-insensitively', async () => {
      vi.mocked(api.getLotJourney).mockResolvedValue({
        activeBalances: [
          { stepId: 'custom-guid', stepCode: 'pack', quantity: 75 }
        ]
      });

      const rawLots = [{ id: 'lot-3', lotNumber: 'L-003' }];
      const result = await enrichLotsWithJourneys(rawLots, mockSteps);

      expect(result[0].isTerminated).toBe(true);
    });

    it('marks isTerminated=true when active balances exist but all quantities are 0', async () => {
      vi.mocked(api.getLotJourney).mockResolvedValue({
        activeBalances: [
          { stepId: 'step-1', stepCode: 'CUT', quantity: 0 },
          { stepId: 'step-2', stepCode: 'STITCH', quantity: '0' }
        ]
      });

      const rawLots = [{ id: 'lot-4', lotNumber: 'L-004' }];
      const result = await enrichLotsWithJourneys(rawLots, mockSteps);

      expect(result[0].isTerminated).toBe(true);
    });

    it('gracefully handles null or undefined rawLots', async () => {
      const resultNull = await enrichLotsWithJourneys(null, mockSteps);
      expect(resultNull).toEqual([]);

      const resultUndef = await enrichLotsWithJourneys(undefined, mockSteps);
      expect(resultUndef).toEqual([]);
    });

    it('gracefully handles null or undefined manufacturingSteps', async () => {
      vi.mocked(api.getLotJourney).mockResolvedValue({
        activeBalances: [{ stepId: 'step-1', stepCode: 'CUT', quantity: 20 }]
      });

      const rawLots = [{ id: 'lot-5', lotNumber: 'L-005' }];
      const result = await enrichLotsWithJourneys(rawLots, null);

      expect(result[0].isTerminated).toBe(false);
      expect(result[0].activeBalances).toHaveLength(1);
    });

    it('isolates errors if getLotJourney rejects for a single lot', async () => {
      vi.mocked(api.getLotJourney)
        .mockRejectedValueOnce(new Error('Network error'))
        .mockResolvedValueOnce({
          activeBalances: [{ stepId: 'step-3', stepCode: 'PACK', quantity: 10 }]
        });

      const rawLots = [
        { id: 'lot-err', lotNumber: 'L-ERR' },
        { id: 'lot-ok', lotNumber: 'L-OK' }
      ];

      const result = await enrichLotsWithJourneys(rawLots, mockSteps);

      expect(result).toHaveLength(2);
      expect(result[0].journey).toBeNull();
      expect(result[0].isTerminated).toBe(false);
      expect(result[0].activeBalances).toEqual([]);

      expect(result[1].isTerminated).toBe(true);
      expect(result[1].activeBalances).toHaveLength(1);
    });

    it('handles lot when journey response has no activeBalances field', async () => {
      vi.mocked(api.getLotJourney).mockResolvedValue({});

      const rawLots = [{ id: 'lot-empty', lotNumber: 'L-EMPTY' }];
      const result = await enrichLotsWithJourneys(rawLots, mockSteps);

      expect(result[0].journey).toBeNull();
      expect(result[0].isTerminated).toBe(false);
      expect(result[0].activeBalances).toEqual([]);
    });

    it('handles mixed-case step IDs in comparison', async () => {
      const mixedSteps = [
        { id: 'STEP-TERMINAL', code: 'TERM', isTerminating: true }
      ];
      vi.mocked(api.getLotJourney).mockResolvedValue({
        activeBalances: [
          { stepId: 'step-terminal', stepCode: 'TERM', quantity: 15 }
        ]
      });

      const rawLots = [{ id: 'lot-mixed', lotNumber: 'L-MIX' }];
      const result = await enrichLotsWithJourneys(rawLots, mixedSteps);

      expect(result[0].isTerminated).toBe(true);
    });
  });
});
