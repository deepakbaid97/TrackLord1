<script>
  import { authStore } from '../../stores/authStore.js';
  import { permissionStore } from '../../stores/permissionStore.js';
  import { tenantConfigStore } from '../../stores/tenantConfigStore.js';
  import { pwaStore } from '../../stores/pwaStore.js';
  import Modal from './Modal.svelte';

  let isOpen = $derived($authStore.showProfileModal);
  let user = $derived($authStore.user);

  let isEditingName = $state(false);
  let editNameValue = $state('');
  let isSavingName = $state(false);
  let nameSaveError = $state('');
  let isCopiedCode = $state(false);
  let copyTimeout = /** @type {any} */ (null);

  // Initialize edit name when modal opens
  $effect(() => {
    if (isOpen && user) {
      editNameValue = user.name || '';
      isEditingName = false;
      nameSaveError = '';
    }
  });

  function handleClose() {
    authStore.closeProfileModal();
    isEditingName = false;
    nameSaveError = '';
  }

  async function handleSaveName() {
    if (isSavingName) return;
    isSavingName = true;
    nameSaveError = '';

    try {
      await authStore.updateProfileName(editNameValue);
      isEditingName = false;
    } catch (/** @type {any} */ err) {
      nameSaveError = err.message || 'Failed to update name.';
    } finally {
      isSavingName = false;
    }
  }

  function handleCopyCompanyCode() {
    const codeToCopy = user?.tenantCode || user?.tenantId;
    if (!codeToCopy) return;
    navigator.clipboard.writeText(codeToCopy);
    isCopiedCode = true;
    if (copyTimeout) clearTimeout(copyTimeout);
    copyTimeout = setTimeout(() => {
      isCopiedCode = false;
    }, 2000);
  }

  function handleOpenTenantSettings() {
    authStore.closeProfileModal();
    tenantConfigStore.openModal();
  }

  function handleLogout() {
    authStore.closeProfileModal();
    authStore.logout();
  }

  function getInitials(name, phone) {
    if (name && name.trim()) {
      const parts = name.trim().split(/\s+/);
      if (parts.length >= 2) {
        return (parts[0][0] + parts[1][0]).toUpperCase();
      }
      return name.slice(0, 2).toUpperCase();
    }
    if (phone) {
      return phone.slice(-2);
    }
    return 'U';
  }

  const roleColors = {
    Admin: 'bg-purple-100 text-purple-800 dark:bg-purple-950/70 dark:text-purple-300 border-purple-300 dark:border-purple-800',
    Supervisor: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/70 dark:text-emerald-300 border-emerald-300 dark:border-emerald-800',
    Manager: 'bg-amber-100 text-amber-800 dark:bg-amber-950/70 dark:text-amber-300 border-amber-300 dark:border-amber-800',
    Operator: 'bg-blue-100 text-blue-800 dark:bg-blue-950/70 dark:text-blue-300 border-blue-300 dark:border-blue-800'
  };
</script>

<Modal bind:isOpen onclose={handleClose} title="User Profile">
  {#if user}
    <div class="space-y-5">
      <!-- User Identity Header Banner -->
      <div class="flex items-start gap-4 p-4 rounded-2xl bg-surface-container-low border border-outline-variant/60">
        <!-- Avatar Circle with Initial -->
        <div class="relative shrink-0">
          <div class="w-14 h-14 rounded-2xl bg-gradient-to-br from-primary to-blue-600 flex items-center justify-center text-white font-bold text-lg shadow-md tracking-wider">
            {getInitials(user.name, user.phoneNumber)}
          </div>
          <span class="absolute -bottom-1 -right-1 w-4 h-4 rounded-full border-2 border-surface {$pwaStore.isOnline ? 'bg-emerald-500' : 'bg-amber-500'}"></span>
        </div>

        <!-- Name & Phone Details -->
        <div class="flex-1 min-w-0">
          {#if isEditingName}
            <form onsubmit={(e) => { e.preventDefault(); handleSaveName(); }} class="space-y-2">
              <div class="flex items-center gap-2">
                <input
                  type="text"
                  bind:value={editNameValue}
                  placeholder="Enter your full name"
                  class="flex-1 px-2.5 py-1 text-sm font-semibold rounded-lg border border-primary bg-surface text-on-surface focus:outline-hidden focus:ring-2 focus:ring-primary/20"
                />
                <button
                  type="submit"
                  disabled={isSavingName}
                  class="px-2.5 py-1 bg-primary text-white rounded-lg text-xs font-bold hover:bg-blue-700 transition-colors disabled:opacity-50 cursor-pointer"
                >
                  {isSavingName ? 'Saving...' : 'Save'}
                </button>
                <button
                  type="button"
                  onclick={() => { isEditingName = false; nameSaveError = ''; }}
                  class="px-2 py-1 text-on-surface-variant hover:bg-surface-container rounded-lg text-xs font-medium cursor-pointer"
                >
                  Cancel
                </button>
              </div>
              {#if nameSaveError}
                <p class="text-xs text-red-600 dark:text-red-400">{nameSaveError}</p>
              {/if}
            </form>
          {:else}
            <div class="flex items-center justify-between gap-2">
              <div class="truncate">
                <h4 class="text-base font-bold text-on-surface truncate">
                  {user.name || 'Set display name'}
                </h4>
                <p class="text-xs text-on-surface-variant font-mono mt-0.5 flex items-center gap-1">
                  <span class="material-symbols-outlined text-[13px]">phone</span>
                  {user.phoneNumber}
                </p>
              </div>
              <button
                type="button"
                onclick={() => { editNameValue = user.name || ''; isEditingName = true; }}
                class="p-1.5 rounded-lg text-on-surface-variant hover:text-primary hover:bg-surface-container transition-colors cursor-pointer shrink-0"
                title="Edit name"
              >
                <span class="material-symbols-outlined text-[16px]">edit</span>
              </button>
            </div>
          {/if}

          <!-- Role Badge -->
          <div class="mt-2.5 flex items-center gap-2 flex-wrap">
            <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold border {roleColors[user.role] || roleColors.Operator}">
              <span class="material-symbols-outlined text-[14px]">verified_user</span>
              {user.role}
            </span>
          </div>
        </div>
      </div>

      <!-- Company & Organization Details Card -->
      <div class="p-4 rounded-2xl bg-surface-container-low border border-outline-variant/60 space-y-3">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-on-surface-variant">
            <span class="material-symbols-outlined text-[16px] text-primary">domain</span>
            Company / Organization
          </div>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-1">
          <div>
            <span class="text-[10px] uppercase font-bold text-on-surface-variant tracking-wider">Company Name</span>
            <p class="text-sm font-semibold text-on-surface mt-0.5 truncate">
              {user.tenantName || 'Default Organization'}
            </p>
          </div>

          <div>
            <span class="text-[10px] uppercase font-bold text-on-surface-variant tracking-wider">Tracking Strategy</span>
            <p class="text-sm font-semibold text-on-surface mt-0.5 capitalize">
              {$tenantConfigStore.clientMode === 'lot' ? 'Per Lot' : $tenantConfigStore.clientMode === 'step' ? 'Per Step' : 'None'}
            </p>
          </div>
        </div>

        <!-- Company Code with One-Click Copy -->
        <div class="pt-2 border-t border-outline-variant/40">
          <div class="flex items-center justify-between mb-1">
            <span class="text-[10px] uppercase font-bold text-on-surface-variant tracking-wider">Company Code</span>
            {#if isCopiedCode}
              <span class="text-[10px] font-bold text-emerald-600 dark:text-emerald-400 flex items-center gap-0.5 animate-in fade-in duration-150">
                <span class="material-symbols-outlined text-[12px]">check</span>
                Copied!
              </span>
            {/if}
          </div>
          <div class="flex items-center justify-between p-2.5 rounded-xl bg-surface border border-outline-variant/60">
            <div class="flex items-center gap-2 min-w-0">
              <span class="material-symbols-outlined text-[18px] text-primary shrink-0">tag</span>
              <span class="text-sm font-mono font-bold text-on-surface tracking-wider truncate select-all">
                {user.tenantCode || '—'}
              </span>
            </div>
            <button
              type="button"
              onclick={handleCopyCompanyCode}
              class="ml-2 px-2.5 py-1 rounded-lg bg-surface-container hover:bg-primary/10 hover:text-primary transition-colors text-xs font-semibold flex items-center gap-1.5 shrink-0 cursor-pointer shadow-2xs"
              title="Copy Company Code"
            >
              <span class="material-symbols-outlined text-[14px]">
                {isCopiedCode ? 'check' : 'content_copy'}
              </span>
              <span>{isCopiedCode ? 'Copied' : 'Copy'}</span>
            </button>
          </div>
        </div>
      </div>

      <!-- Quick Action Buttons -->
      <div class="space-y-2">
        {#if $permissionStore.can('tenant_settings', 'read') || $permissionStore.isAdmin}
          <button
            type="button"
            onclick={handleOpenTenantSettings}
            class="w-full flex items-center justify-between p-3 rounded-xl bg-surface-container-low hover:bg-surface-container border border-outline-variant/60 transition-colors text-left cursor-pointer group"
          >
            <div class="flex items-center gap-2.5">
              <span class="material-symbols-outlined text-[20px] text-primary">settings_suggest</span>
              <div>
                <div class="text-xs sm:text-sm font-semibold text-on-surface">Company Settings</div>
                <div class="text-[10px] text-on-surface-variant">Configure tracking strategy & feature flags</div>
              </div>
            </div>
            <span class="material-symbols-outlined text-[18px] text-on-surface-variant group-hover:text-primary transition-colors">
              chevron_right
            </span>
          </button>
        {/if}
      </div>

      <!-- Sign Out / Logout Action -->
      <div class="pt-2 border-t border-outline-variant/60">
        <button
          type="button"
          onclick={handleLogout}
          class="w-full py-2.5 px-4 rounded-xl bg-red-50 hover:bg-red-100 text-red-700 dark:bg-red-950/40 dark:hover:bg-red-900/60 dark:text-red-300 border border-red-200 dark:border-red-800 font-bold text-xs sm:text-sm flex items-center justify-center gap-2 transition-all active:scale-[0.99] cursor-pointer shadow-xs"
        >
          <span class="material-symbols-outlined text-[18px]">logout</span>
          <span>Sign Out of TrackLord</span>
        </button>
      </div>
    </div>
  {:else}
    <div class="py-8 text-center text-on-surface-variant text-sm">
      No user session active.
    </div>
  {/if}
</Modal>
