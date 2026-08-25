<script>
  import Modal from '$lib/components/ui/Modal.svelte';
  import Input from '$lib/components/ui/Input.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import Button from '$lib/components/ui/Button.svelte';
  import { createClient } from '$lib/api.js';

  /**
   * @type {{
   *   isOpen: boolean,
   *   onSuccess?: (newClient: any) => void,
   *   onError?: (err: any) => void
   * }}
   */
  let { isOpen = $bindable(false), onSuccess, onError } = $props();

  let form = $state({
    name: '',
    phoneNumber: '',
    clientType: 'Buyer'
  });

  let isSubmitting = $state(false);

  const clientTypeOptions = [
    { label: 'Buyer', value: 'Buyer' },
    { label: 'Wholesaler', value: 'Wholesaler' },
    { label: 'Job Worker', value: 'Job Worker' },
    { label: 'Customer', value: 'Customer' },
    { label: 'Retailer', value: 'Retailer' },
    { label: 'Supplier', value: 'Supplier' },
    { label: 'Self', value: 'Self' }
  ];

  /** @param {SubmitEvent} e */
  async function handleSubmit(e) {
    e.preventDefault();
    isSubmitting = true;
    try {
      const payload = {
        name: form.name.trim(),
        phoneNumber: form.phoneNumber ? form.phoneNumber.trim() : '',
        clientType: form.clientType
      };

      const res = await createClient(payload);
      isOpen = false;
      const createdName = form.name;
      form = { name: '', phoneNumber: '', clientType: 'Buyer' };
      if (onSuccess) {
        onSuccess({ id: res?.id, name: createdName, ...payload });
      }
    } catch (/** @type {any} */ err) {
      if (onError) {
        onError(err);
      }
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Modal bind:isOpen title="Register New Client">
  <form onsubmit={handleSubmit} class="space-y-4">
    <Input
      label="Client / Company Name"
      bind:value={form.name}
      placeholder="e.g. Apex Textiles Ltd."
      icon="badge"
      required={true}
    />

    <Input
      label="Phone Number (Optional)"
      type="tel"
      bind:value={form.phoneNumber}
      placeholder="e.g. +91 98765 43210"
      icon="call"
      required={false}
    />

    <Select
      label="Client Type"
      bind:value={form.clientType}
      options={clientTypeOptions}
      icon="category"
      required={true}
    />

    <div class="flex justify-end gap-3 pt-3 border-t border-outline-variant">
      <Button variant="outline" onclick={() => isOpen = false}>
        Cancel
      </Button>
      <Button variant="primary" type="submit" icon="save" disabled={isSubmitting}>
        {isSubmitting ? 'Saving...' : 'Save Client'}
      </Button>
    </div>
  </form>
</Modal>
