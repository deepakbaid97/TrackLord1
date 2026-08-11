<script>
  import '../app.css';
  import { onMount } from 'svelte';
  import Header from '$lib/components/ui/Header.svelte';
  import Navbar from '$lib/components/ui/Navbar.svelte';
  import PhoneAuthModal from '$lib/components/ui/PhoneAuthModal.svelte';
  import { authStore } from '$lib/stores/authStore.js';

  let { children } = $props();

  let showModal = $derived($authStore.showModal);
  let isAuthenticated = $derived($authStore.isAuthenticated);
  let isLoading = $derived($authStore.isLoading);

  onMount(() => {
    authStore.initAuth();
  });
</script>

<!-- Top App Header -->
<Header title="TRACKLORD ERP" />

<!-- Main Content Area -->
<main class="pb-24 min-h-screen bg-slate-50 dark:bg-slate-950 text-slate-900 dark:text-slate-100">
  {@render children()}
</main>

<!-- Floating Bottom Navbar -->
<Navbar />

<!-- Phone Authentication Modal -->
{#if showModal || (!isAuthenticated && !isLoading)}
  <PhoneAuthModal />
{/if}
