<script>
  import { tenantConfigStore } from '../../stores/tenantConfigStore.js';

  /**
   * Declarative FeatureGate component.
   * Renders its children snippet only if the specified feature is enabled for the tenant.
   *
   * @type {{
   *   feature: string,
   *   defaultVal?: boolean,
   *   fallback?: import('svelte').Snippet,
   *   children?: import('svelte').Snippet
   * }}
   */
  let { feature, defaultVal = true, fallback, children } = $props();

  let isEnabled = $derived.by(() => {
    const overrides = $tenantConfigStore.featureOverrides;
    const clientMode = $tenantConfigStore.clientMode;

    if (feature === 'pages.clients' && clientMode === 'none') {
      return false;
    }
    if (overrides && typeof overrides[feature] === 'boolean') {
      return overrides[feature];
    }
    return defaultVal;
  });
</script>

{#if isEnabled}
  {@render children?.()}
{:else if fallback}
  {@render fallback()}
{/if}
