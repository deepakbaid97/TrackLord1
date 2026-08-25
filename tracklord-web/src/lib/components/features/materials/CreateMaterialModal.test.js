import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import CreateMaterialModal from './CreateMaterialModal.svelte';
import * as api from '$lib/api.js';

vi.mock('$lib/api.js', () => ({
  createMaterialLot: vi.fn()
}));

describe('CreateMaterialModal component', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('submits raw material batch details', async () => {
    vi.mocked(api.createMaterialLot).mockResolvedValue({ id: 'mat-1' });
    const handleSuccess = vi.fn();

    render(CreateMaterialModal, {
      props: {
        isOpen: true,
        onSuccess: handleSuccess
      }
    });

    const lotInput = screen.getByPlaceholderText('e.g. FAB-2026-001');
    const nameInput = screen.getByPlaceholderText('e.g. Denim Grey Cloth 300GSM');

    await fireEvent.input(lotInput, { target: { value: 'FAB-999' } });
    await fireEvent.input(nameInput, { target: { value: 'Denim Fabric' } });

    const submitBtn = screen.getByRole('button', { name: /register material/i });
    await fireEvent.click(submitBtn);

    expect(api.createMaterialLot).toHaveBeenCalledWith(expect.objectContaining({
      lotNumber: 'FAB-999',
      materialName: 'Denim Fabric',
      initialQuantity: 500,
      unit: 2
    }));

    expect(handleSuccess).toHaveBeenCalledTimes(1);
  });
});
