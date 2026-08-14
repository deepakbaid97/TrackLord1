<script>
  import { tenantConfigStore } from '../../stores/tenantConfigStore.js';

  /**
   * Declarative ClientModeGate component.
   * Renders children based on the tenant's active client tracking mode.
   *
   * @type {{
   *   mode?: 'lot' | 'step' | 'none' | 'any',
   *   fallback?: import('svelte').Snippet,
   *   children?: import('svelte').Snippet
   * }}
   */
  let { mode = 'any', fallback, children } = $props();

  let isMatching = $derived.by(() => {
    const clientMode = $tenantConfigStore.clientMode;
    if (mode === 'any') return clientMode !== 'none';
    return clientMode === mode;
  });
</script>

{#if isMatching}
  {@render children?.()}
{:else if fallback}
  {@render fallback()}
{/if}
