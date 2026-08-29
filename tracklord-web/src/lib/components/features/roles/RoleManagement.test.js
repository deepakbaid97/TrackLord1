import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import PermissionMatrix from './PermissionMatrix.svelte';
import RoleCard from './RoleCard.svelte';
import DeleteRoleModal from './DeleteRoleModal.svelte';
import PermissionGate from '$lib/components/ui/PermissionGate.svelte';
import { authStore } from '$lib/stores/authStore.js';

describe('RBAC Frontend Components', () => {
  beforeEach(() => {
    authStore.logout();
  });

  describe('PermissionMatrix', () => {
    it('renders categorized permission modules', () => {
      render(PermissionMatrix, {
        props: {
          permissions: {}
        }
      });

      expect(screen.getByText('Production & Manufacturing')).toBeInTheDocument();
      expect(screen.getByText('Inventory & Materials')).toBeInTheDocument();
      expect(screen.getByText('Workflow & Partners')).toBeInTheDocument();
      expect(screen.getByText('System & Administration')).toBeInTheDocument();
    });

    it('toggles permissions when pills are clicked', async () => {
      let perms = { production_lots: ['read'] };
      const handleChange = vi.fn();

      render(PermissionMatrix, {
        props: {
          permissions: perms,
          onChange: handleChange
        }
      });

      const createEditButton = screen.getByRole('button', { name: /Create & Edit/i });
      await fireEvent.click(createEditButton);

      expect(handleChange).toHaveBeenCalled();
    });
  });

  describe('RoleCard', () => {
    it('renders role details, badge, and permission chips', () => {
      const mockRole = {
        id: 'role-fi',
        name: 'Floor Incharge',
        description: 'Supervises material stock and consumption',
        isSystemRole: false,
        permissions: {
          material_lots: ['read', 'write'],
          movement_reversals: ['write']
        }
      };

      render(RoleCard, {
        props: {
          role: mockRole,
          assignedCount: 4,
          canManage: true
        }
      });

      expect(screen.getByText('Floor Incharge')).toBeInTheDocument();
      expect(screen.getByText('Custom Role')).toBeInTheDocument();
      expect(screen.getByText('4 members assigned')).toBeInTheDocument();
      expect(screen.getByText('Reversals: Allowed')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Edit Role/i })).toBeInTheDocument();
    });

    it('displays System Role badge and hides delete button for system roles', () => {
      const systemRole = {
        id: 'role-admin',
        name: 'Admin',
        description: 'Full tenant control',
        isSystemRole: true,
        permissions: {}
      };

      render(RoleCard, {
        props: {
          role: systemRole,
          assignedCount: 1,
          canManage: true
        }
      });

      expect(screen.getByText('System Role')).toBeInTheDocument();
      expect(screen.getByText('All Modules: Full Control')).toBeInTheDocument();
      expect(screen.queryByTitle('Delete this custom role')).not.toBeInTheDocument();
    });
  });

  describe('DeleteRoleModal', () => {
    it('prevents deletion if system role', () => {
      const systemRole = { id: 'r1', name: 'Manager', isSystemRole: true };
      render(DeleteRoleModal, {
        props: {
          isOpen: true,
          role: systemRole,
          assignedCount: 0
        }
      });

      expect(screen.getByText('System Role Protected')).toBeInTheDocument();
      const deleteBtn = screen.getByRole('button', { name: /Delete Role/i });
      expect(deleteBtn).toBeDisabled();
    });

    it('prevents deletion if users are currently assigned to role', () => {
      const customRole = { id: 'r2', name: 'Custom Worker', isSystemRole: false };
      render(DeleteRoleModal, {
        props: {
          isOpen: true,
          role: customRole,
          assignedCount: 3
        }
      });

      expect(screen.getByText('Team Members Assigned')).toBeInTheDocument();
      const deleteBtn = screen.getByRole('button', { name: /Delete Role/i });
      expect(deleteBtn).toBeDisabled();
    });
  });
});
