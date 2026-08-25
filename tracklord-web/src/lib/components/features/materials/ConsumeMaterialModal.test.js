import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import ConsumeMaterialModal from './ConsumeMaterialModal.svelte';
import * as api from '$lib/api.js';

vi.mock('$lib/api.js', () => ({
  consumeMaterial: vi.fn()
}));

describe('ConsumeMaterialModal component', () => {
  const mockMaterials = [
    { id: 'm1', lotNumber: 'FAB-01', materialName: 'Denim', remainingQuantity: 100, unit: 'Meters' }
  ];

  const mockSteps = [
    { id: 's1', code: 'CUT', name: 'Cutting' }
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('submits material consumption record for active lot', async () => {
    vi.mocked(api.consumeMaterial).mockResolvedValue({ id: 'cons-1' });
    const handleSuccess = vi.fn();

    render(ConsumeMaterialModal, {
      props: {
        isOpen: true,
        lotId: 'lot-123',
        materialLots: mockMaterials,
        steps: mockSteps,
        onSuccess: handleSuccess
      }
    });

    const submitBtn = screen.getByRole('button', { name: /log consumption/i });
    await fireEvent.click(submitBtn);

    expect(api.consumeMaterial).toHaveBeenCalledWith(expect.objectContaining({
      productionLotId: 'lot-123',
      materialLotId: 'm1',
      stepId: 's1',
      quantityConsumed: 25
    }));

    expect(handleSuccess).toHaveBeenCalledTimes(1);
  });
});
