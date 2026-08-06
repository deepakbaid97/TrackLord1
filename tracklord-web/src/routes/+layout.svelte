<script>
  import '../app.css';
  import Navbar from '$lib/components/ui/Navbar.svelte';
  let { children } = $props();
</script>

<!-- Main Content Area -->
<div class="pb-24">
  {@render children()}
</div>

<!-- Floating Bottom Navbar -->
<Navbar />
