<script>
  import '../app.css';
  import { onMount } from 'svelte';
  import Header from '$lib/components/ui/Header.svelte';
  import Navbar from '$lib/components/ui/Navbar.svelte';
  import PhoneAuthModal from '$lib/components/ui/PhoneAuthModal.svelte';
  import TenantSettingsModal from '$lib/components/ui/TenantSettingsModal.svelte';
  import UserProfileModal from '$lib/components/ui/UserProfileModal.svelte';
  import PwaInstallPrompt from '$lib/components/ui/PwaInstallPrompt.svelte';
  import { authStore } from '$lib/stores/authStore.js';
  import { pwaStore } from '$lib/stores/pwaStore.js';

  let { children } = $props();

  let showModal = $derived($authStore.showModal);
  let isAuthenticated = $derived($authStore.isAuthenticated);
  let isLoading = $derived($authStore.isLoading);

  onMount(() => {
    authStore.initAuth();
    pwaStore.initPwa();
  });
</script>

<!-- Top App Header -->
<Header />

<!-- Main Content Area -->
<div class="pt-4 pb-24 min-h-screen bg-slate-50 dark:bg-slate-950 text-slate-900 dark:text-slate-100">
  {@render children()}
</div>

<!-- Floating Bottom Navbar -->
<Navbar />

<!-- User Profile Modal -->
<UserProfileModal />

<!-- Tenant Settings & Feature Configuration Modal -->
<TenantSettingsModal />

<!-- PWA Install Prompt & Offline Connectivity Banner -->
<PwaInstallPrompt />

<!-- Phone Authentication Modal -->
{#if showModal || (!isAuthenticated && !isLoading)}
  <PhoneAuthModal />
{/if}

