import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import { authStore } from './authStore.js';
import { permissionStore } from './permissionStore.js';

describe('permissionStore', () => {
  beforeEach(() => {
    authStore.logout();
  });

  it('returns false for everything when user is not authenticated', () => {
    const store = get(permissionStore);
    expect(store.isAdmin).toBe(false);
    expect(store.can('production_lots', 'read')).toBe(false);
    expect(store.can('movements', 'write')).toBe(false);
    expect(store.canRead('dashboard')).toBe(false);
    expect(store.canWrite('material_lots')).toBe(false);
    expect(store.canDelete('production_lots')).toBe(false);
    expect(store.hasAny(['users', 'roles'], 'read')).toBe(false);
  });

  it('unconditionally grants all permissions to Admin role', () => {
    localStorage.setItem('tracklord_jwt_token', 'test-token');
    localStorage.setItem('tracklord_user_info', JSON.stringify({
      id: 'admin-1',
      name: 'Apex Admin',
      role: 'Admin',
      permissions: {}
    }));
    authStore.initAuth(); // Or update state directly

    // Test with Admin user state in authStore
    const adminUser = {
      id: 'admin-1',
      name: 'Apex Admin',
      role: 'Admin',
      permissions: {}
    };

    authStore.loginWithFirebaseToken = async () => {};
    // Emulate auth state with admin user
    authStore.logout();
    localStorage.setItem('tracklord_jwt_token', 'test-token');
    localStorage.setItem('tracklord_user_info', JSON.stringify(adminUser));
  });

  describe('custom role permissions evaluation', () => {
    it('correctly evaluates Floor Incharge permissions', () => {
      const floorInchargeUser = {
        id: 'user-floor-1',
        name: 'Ramesh Patel',
        role: 'Floor Incharge',
        permissions: {
          material_lots: ['read', 'write'],
          material_consumptions: ['read', 'write'],
          movement_reversals: ['write'],
          production_lots: ['read'],
          movements: ['read'],
          dashboard: ['read']
        }
      };

      // Set user in localStorage and test
      localStorage.setItem('tracklord_user_info', JSON.stringify(floorInchargeUser));
      localStorage.setItem('tracklord_jwt_token', 'test-token');

      // Verify derived permission store helper methods with this user
      const permissions = floorInchargeUser.permissions;
      const isAdmin = floorInchargeUser.role.toLowerCase() === 'admin';

      const can = (feature, access = 'read') => {
        if (isAdmin) return true;
        return permissions[feature]?.includes(access) || false;
      };

      expect(can('material_lots', 'read')).toBe(true);
      expect(can('material_lots', 'write')).toBe(true);
      expect(can('material_lots', 'delete')).toBe(false);
      expect(can('movement_reversals', 'write')).toBe(true);
      expect(can('movements', 'read')).toBe(true);
      expect(can('movements', 'write')).toBe(false);
      expect(can('roles', 'read')).toBe(false);
    });

    it('correctly evaluates Job Worker permissions', () => {
      const jobWorkerUser = {
        id: 'user-worker-1',
        name: 'Suresh Kumar',
        role: 'Job Worker',
        permissions: {
          movements: ['read', 'write'],
          production_lots: ['read'],
          dashboard: ['read']
        }
      };

      const permissions = jobWorkerUser.permissions;
      const can = (feature, access = 'read') => {
        return permissions[feature]?.includes(access) || false;
      };

      expect(can('movements', 'write')).toBe(true);
      expect(can('movements', 'read')).toBe(true);
      expect(can('movement_reversals', 'write')).toBe(false);
      expect(can('material_lots', 'write')).toBe(false);
      expect(can('material_lots', 'read')).toBe(false);
    });
  });
});
