<script>
  import { permissionStore } from '../../stores/permissionStore.js';

  /**
   * Declarative PermissionGate component.
   * Renders children only if current user's role grants permission for the given feature & access level.
   *
   * @type {{
   *   feature: string,
   *   access?: string,
   *   fallback?: import('svelte').Snippet,
   *   children?: import('svelte').Snippet
   * }}
   */
  let { feature, access = 'read', fallback, children } = $props();

  let isAllowed = $derived($permissionStore.can(feature, access));
</script>

{#if isAllowed}
  {@render children?.()}
{:else if fallback}
  {@render fallback()}
{/if}
