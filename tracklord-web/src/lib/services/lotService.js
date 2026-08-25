import { getLotJourney } from '../api.js';

/**
 * Common Manufacturing Step Icon mapping
 * @type {Record<string, string>}
 */
export const STEP_ICON_MAP = {
  'CUT': 'content_cut',
  'STITCH': 'webhook',
  'WASH': 'local_laundry_service',
  'FIN': 'iron',
  'PACK': 'inventory_2',
  'IMPORT': 'file_upload'
};

/**
 * Common Manufacturing Step Color mapping
 * @type {Record<string, string>}
 */
export const STEP_COLOR_MAP = {
  'CUT': 'bg-blue-500',
  'STITCH': 'bg-violet-500',
  'WASH': 'bg-cyan-500',
  'FIN': 'bg-amber-500',
  'PACK': 'bg-emerald-500'
};

/**
 * Enriches production lots with their journeys, active balances, and termination status.
 * @param {Array<any>} rawLots 
 * @param {Array<any>} manufacturingSteps 
 * @returns {Promise<Array<any>>}
 */
export async function enrichLotsWithJourneys(rawLots, manufacturingSteps) {
  const stepsList = manufacturingSteps || [];
  const terminatingStepIds = new Set(
    stepsList
      .filter((s) => s.isTerminating)
      .map((s) => (s.id || '').toLowerCase())
  );
  const terminatingStepCodes = new Set(
    stepsList
      .filter((s) => s.isTerminating)
      .map((s) => (s.code || '').toUpperCase())
  );

  const enriched = await Promise.all(
    (rawLots || []).map(async (lot) => {
      try {
        const journey = await getLotJourney(lot.id);
        if (journey && journey.activeBalances) {
          const activeSteps = journey.activeBalances.filter((/** @type {any} */ b) => Number(b.quantity) > 0);
          const isTerminated = activeSteps.length === 0 || activeSteps.every((/** @type {any} */ b) => {
            const stepIdLower = (b.stepId || '').toLowerCase();
            const stepCodeUpper = (b.stepCode || '').toUpperCase();
            return terminatingStepIds.has(stepIdLower) || terminatingStepCodes.has(stepCodeUpper);
          });
          return { ...lot, journey, isTerminated, activeBalances: journey.activeBalances };
        }
        return { ...lot, journey: null, isTerminated: false, activeBalances: [] };
      } catch {
        return { ...lot, journey: null, isTerminated: false, activeBalances: [] };
      }
    })
  );

  return enriched;
}
