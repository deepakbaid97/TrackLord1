import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import UserProfileModal from './UserProfileModal.svelte';
import { authStore } from '$lib/stores/authStore.js';

describe('UserProfileModal component', () => {
  const mockUser = {
    id: '11111111-2222-3333-4444-555555555555',
    name: 'Jitendra Bothra',
    phoneNumber: '+919427554218',
    tenantId: 'a8bfcd63-1cfb-4a43-b463-d663f7c1ccb2',
    tenantName: 'Aliya Wash',
    tenantCode: 'ALIYA',
    role: 'Admin',
    roleId: 'cacb46e5-e293-4284-9453-0360654a13d0'
  };

  beforeEach(() => {
    authStore.setUser(mockUser, 'mock-jwt-token');
    authStore.closeProfileModal();
  });

  it('does not render when showProfileModal is false', () => {
    render(UserProfileModal);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders user details, company name, company code, and role when open', () => {
    authStore.openProfileModal();
    render(UserProfileModal);

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Jitendra Bothra')).toBeInTheDocument();
    expect(screen.getByText('+919427554218')).toBeInTheDocument();
    expect(screen.getByText('Aliya Wash')).toBeInTheDocument();
    expect(screen.getByText('ALIYA')).toBeInTheDocument();
    expect(screen.getByText('Admin')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /copy/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign out/i })).toBeInTheDocument();
  });

  it('calls logout when Sign Out button is clicked', async () => {
    const logoutSpy = vi.spyOn(authStore, 'logout');
    authStore.openProfileModal();
    render(UserProfileModal);

    const logoutBtn = screen.getByRole('button', { name: /sign out/i });
    await fireEvent.click(logoutBtn);

    expect(logoutSpy).toHaveBeenCalled();
  });
});
