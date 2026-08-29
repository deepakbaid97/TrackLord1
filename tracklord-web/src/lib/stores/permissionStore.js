import { derived } from 'svelte/store';
import { authStore } from './authStore.js';

/**
 * Reactive store and helper utilities for Role-Based Access Control (RBAC).
 * Evaluates current user's permissions against the dynamic permission dictionary.
 */
export const permissionStore = derived(authStore, ($auth) => {
  const user = $auth.user;
  const permissions = user?.permissions || {};
  const roleName = user?.role || '';
  const isAdmin = roleName.toLowerCase() === 'admin';

  /**
   * Checks if the authenticated user has the specified permission for a feature.
   * Admin role unconditionally returns true for all features.
   *
   * @param {string} feature - e.g. 'production_lots', 'movements', 'movement_reversals'
   * @param {string} [accessLevel='read'] - 'read', 'write', or 'delete'
   * @returns {boolean}
   */
  function can(feature, accessLevel = 'read') {
    if (!user) return false;
    if (isAdmin) return true;

    const featurePerms = permissions[feature];
    if (!featurePerms || !Array.isArray(featurePerms)) {
      return false;
    }

    return featurePerms.includes(accessLevel);
  }

  /**
   * Checks read access for a feature.
   * @param {string} feature
   */
  function canRead(feature) {
    return can(feature, 'read');
  }

  /**
   * Checks write/edit/action access for a feature.
   * @param {string} feature
   */
  function canWrite(feature) {
    return can(feature, 'write');
  }

  /**
   * Checks delete or reversal access for a feature.
   * @param {string} feature
   */
  function canDelete(feature) {
    return can(feature, 'delete');
  }

  /**
   * Checks if user has permission for any of the given features.
   * @param {string[]} features
   * @param {string} [accessLevel='read']
   */
  function hasAny(features, accessLevel = 'read') {
    if (!features || features.length === 0) return false;
    return features.some(f => can(f, accessLevel));
  }

  return {
    user,
    permissions,
    roleName,
    isAdmin,
    can,
    canRead,
    canWrite,
    canDelete,
    hasAny
  };
});
