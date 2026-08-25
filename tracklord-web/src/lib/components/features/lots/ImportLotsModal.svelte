<script>
  import * as XLSX from 'xlsx';
  import Button from '$lib/components/ui/Button.svelte';
  import Select from '$lib/components/ui/Select.svelte';
  import { importProductionLots } from '$lib/api.js';

  let {
    isOpen = $bindable(false),
    steps = [],
    onSuccess,
    onError
  } = $props();

  let importFile = $state(/** @type {File|null} */ (null));
  let importPreviewData = $state(/** @type {Array<any>} */ ([]));
  let hasStepColumn = $state(false);
  let selectedGlobalStepId = $state('');
  let selectedGlobalStepName = $state('');
  let isParsing = $state(false);
  let isImporting = $state(false);
  let importErrorMessage = $state('');
  let importSuccessResult = $state(/** @type {any} */ (null));

  let stepOptions = $derived(
    steps.map(s => ({
      label: `${s.name} (${s.code})`,
      value: s.id
    }))
  );

  function resetState() {
    importFile = null;
    importPreviewData = [];
    hasStepColumn = false;
    selectedGlobalStepId = '';
    selectedGlobalStepName = '';
    isParsing = false;
    isImporting = false;
    importErrorMessage = '';
    importSuccessResult = null;
  }

  function handleClose() {
    isOpen = false;
    resetState();
  }

  async function handleFileSelect(event) {
    const file = event.target.files?.[0];
    if (!file) return;
    importFile = file;
    isParsing = true;
    importErrorMessage = '';
    importPreviewData = [];
    importSuccessResult = null;
    hasStepColumn = false;
    selectedGlobalStepId = '';
    selectedGlobalStepName = '';

    try {
      const data = await file.arrayBuffer();
      const workbook = XLSX.read(data, { type: 'array' });
      const firstSheetName = workbook.SheetNames[0];
      if (!firstSheetName) throw new Error('No worksheets found in file.');

      const worksheet = workbook.Sheets[firstSheetName];
      const jsonRows = XLSX.utils.sheet_to_json(worksheet, { defval: '' });
      if (!jsonRows || jsonRows.length === 0) throw new Error('Selected file contains no data rows.');

      const sampleRow = jsonRows[0];
      const keys = Object.keys(sampleRow);

      const findKey = (candidates) => {
        return keys.find(k => {
          const lower = k.toLowerCase().replace(/[^a-z0-9]/g, '');
          return candidates.some(c => lower.includes(c));
        });
      };

      const clientKey = findKey(['client', 'customer', 'buyer', 'name']);
      const qtyKey = findKey(['quantity', 'qty', 'count', 'pcs', 'amount']);
      const lotKey = findKey(['lot', 'batch', 'number', 'code']);
      const stepKey = findKey(['step', 'steps', 'stage', 'process', 'manufacturingstep']);

      const defaultClientKey = clientKey || keys[0];
      const defaultQtyKey = qtyKey || keys[1];
      const defaultLotKey = lotKey || keys[2];

      hasStepColumn = Boolean(stepKey);

      if (!hasStepColumn) {
        const defaultStep = steps.find(s => s.code.toUpperCase() === 'IMPORT') || steps[0];
        selectedGlobalStepId = defaultStep ? defaultStep.id : '';
        selectedGlobalStepName = defaultStep ? defaultStep.name : 'Import';
      }

      const parsed = jsonRows.map((row, idx) => {
        const clientName = String(row[defaultClientKey] ?? '').trim();
        const rawQty = row[defaultQtyKey] ?? 0;
        const lotNumber = String(row[defaultLotKey] ?? '').trim();
        const quantity = parseFloat(rawQty) || 0;

        let rowStepId = '';
        let rowStepName = '';

        if (hasStepColumn && stepKey) {
          const rawStep = String(row[stepKey] ?? '').trim();
          if (rawStep) {
            const matchedStep = steps.find(s =>
              s.name.toLowerCase() === rawStep.toLowerCase() ||
              s.code.toLowerCase() === rawStep.toLowerCase()
            );
            if (matchedStep) {
              rowStepId = matchedStep.id;
              rowStepName = matchedStep.name;
            } else {
              rowStepName = rawStep;
            }
          }
        }

        return {
          rowNum: idx + 1,
          clientName: clientName || 'General Client',
          quantity,
          lotNumber,
          stepId: rowStepId,
          stepName: rowStepName,
          isValid: Boolean(lotNumber && quantity > 0)
        };
      }).filter(r => r.lotNumber || r.quantity > 0);

      importPreviewData = parsed;
      if (parsed.length === 0) {
        importErrorMessage = 'Could not extract valid records. Ensure columns: Client Name, Quantity, Lot Number.';
      }
    } catch (err) {
      importErrorMessage = 'Failed to parse file: ' + (err?.message || err);
    } finally {
      isParsing = false;
    }
  }

  async function handleImportSubmit() {
    const validItems = importPreviewData.filter(r => r.isValid);
    if (validItems.length === 0) {
      importErrorMessage = 'No valid rows found to import.';
      return;
    }

    isImporting = true;
    importErrorMessage = '';
    importSuccessResult = null;

    try {
      const payload = {
        items: validItems.map(item => ({
          clientName: item.clientName,
          quantity: item.quantity,
          lotNumber: item.lotNumber,
          stepId: item.stepId || (!hasStepColumn ? selectedGlobalStepId : null),
          stepName: item.stepName || (!hasStepColumn ? selectedGlobalStepName : null)
        })),
        createdBy: 'File Import Tool'
      };

      const result = await importProductionLots(payload);
      importSuccessResult = result;
      if (onSuccess) onSuccess(result);
    } catch (err) {
      importErrorMessage = err?.message || 'Import failed.';
      if (onError) onError(err);
    } finally {
      isImporting = false;
    }
  }
</script>

{#if isOpen}
  <div class="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
    <div class="bg-surface dark:bg-surface-dim border border-outline-variant rounded-3xl shadow-2xl w-full max-w-3xl overflow-hidden flex flex-col max-h-[90vh]">
      <!-- Modal Header -->
      <div class="px-6 py-5 border-b border-outline-variant flex items-center justify-between bg-surface-container-low/50">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-2xl bg-primary/10 text-primary flex items-center justify-center">
            <span class="material-symbols-outlined text-[24px]">upload_file</span>
          </div>
          <div>
            <h3 class="font-bold text-lg text-on-surface">Import Production Lots</h3>
            <p class="text-xs text-on-surface-variant">Upload an Excel (.xlsx, .xls) or CSV (.csv) file</p>
          </div>
        </div>
        <button
          type="button"
          onclick={handleClose}
          class="p-2 rounded-full hover:bg-surface-container-high text-on-surface-variant transition-colors cursor-pointer"
        >
          <span class="material-symbols-outlined text-[20px]">close</span>
        </button>
      </div>

      <!-- Modal Body -->
      <div class="p-6 overflow-y-auto space-y-4 flex-1">
        {#if importErrorMessage}
          <div class="p-3.5 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900/60 text-red-700 dark:text-red-300 text-xs flex items-center gap-2">
            <span class="material-symbols-outlined text-[18px]">error</span>
            <span>{importErrorMessage}</span>
          </div>
        {/if}

        {#if importSuccessResult}
          <div class="p-4 rounded-2xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900/60 text-emerald-800 dark:text-emerald-300 text-xs space-y-2">
            <div class="flex items-center gap-2 font-bold text-sm">
              <span class="material-symbols-outlined text-[20px]">check_circle</span>
              <span>Import Completed Successfully!</span>
            </div>
            <p>{importSuccessResult.message || `Processed ${importSuccessResult.successfulCount ?? 0} lots successfully.`}</p>
          </div>
        {:else}
          <!-- File Upload Zone -->
          <div class="border-2 border-dashed border-outline-variant/80 rounded-2xl p-6 text-center hover:border-primary/60 transition-colors bg-surface-container-low/30 flex flex-col items-center justify-center gap-3">
            <span class="material-symbols-outlined text-[40px] text-primary">cloud_upload</span>
            <div>
              <p class="text-xs sm:text-sm font-semibold text-on-surface">Click to select an Excel or CSV file</p>
              <p class="text-[11px] text-on-surface-variant mt-0.5">Supports .xlsx, .xls, .csv files</p>
            </div>
            <label class="px-4 py-2 bg-surface border border-outline-variant text-on-surface rounded-xl text-xs font-bold hover:bg-surface-container transition-colors cursor-pointer shadow-xs">
              Browse File
              <input type="file" accept=".xlsx, .xls, .csv" onchange={handleFileSelect} class="hidden" />
            </label>
            {#if importFile}
              <div class="flex items-center gap-1.5 text-xs text-primary font-medium mt-1">
                <span class="material-symbols-outlined text-[16px]">draft</span>
                <span>{importFile.name} ({(importFile.size / 1024).toFixed(1)} KB)</span>
              </div>
            {/if}
          </div>

          <!-- Step Selector if no step column was detected -->
          {#if importPreviewData.length > 0 && !hasStepColumn}
            <div class="p-3.5 bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-900/50 rounded-2xl space-y-2">
              <div class="flex items-center gap-2 text-xs font-bold text-amber-800 dark:text-amber-300">
                <span class="material-symbols-outlined text-[18px]">info</span>
                <span>No Manufacturing Step Column Detected in File</span>
              </div>
              <p class="text-[11px] text-amber-700 dark:text-amber-400">
                All imported lots will be assigned to the following step:
              </p>
              <Select
                label="Assign All Lots to Step"
                bind:value={selectedGlobalStepId}
                options={stepOptions}
                icon="alt_route"
              />
            </div>
          {/if}

          <!-- Preview Table -->
          {#if isParsing}
            <div class="p-8 text-center text-xs text-on-surface-variant flex items-center justify-center gap-2">
              <span class="material-symbols-outlined animate-spin text-[20px] text-primary">sync</span>
              Parsing spreadsheet rows...
            </div>
          {:else if importPreviewData.length > 0}
            <div class="space-y-2">
              <div class="flex items-center justify-between">
                <span class="text-xs font-bold text-on-surface font-mono">
                  Preview ({importPreviewData.filter(r => r.isValid).length} valid of {importPreviewData.length} rows)
                </span>
              </div>
              <div class="max-h-56 overflow-y-auto rounded-xl border border-outline-variant/60">
                <table class="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr class="bg-surface-container-low border-b border-outline-variant/60 font-mono text-on-surface-variant">
                      <th class="px-3 py-2">#</th>
                      <th class="px-3 py-2">Lot Number</th>
                      <th class="px-3 py-2">Client / Buyer</th>
                      <th class="px-3 py-2 text-right">Quantity</th>
                      <th class="px-3 py-2">Step</th>
                      <th class="px-3 py-2 text-center">Status</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-outline-variant/40">
                    {#each importPreviewData.slice(0, 50) as row}
                      <tr class="hover:bg-surface-container-low/50 {row.isValid ? '' : 'bg-red-50/50 dark:bg-red-950/20 text-red-700'}">
                        <td class="px-3 py-2 font-mono">{row.rowNum}</td>
                        <td class="px-3 py-2 font-bold font-mono">{row.lotNumber || 'MISSING'}</td>
                        <td class="px-3 py-2">{row.clientName}</td>
                        <td class="px-3 py-2 text-right font-mono">{row.quantity} Pcs</td>
                        <td class="px-3 py-2">{row.stepName || (hasStepColumn ? '—' : selectedGlobalStepName)}</td>
                        <td class="px-3 py-2 text-center">
                          {#if row.isValid}
                            <span class="text-emerald-600 dark:text-emerald-400 font-bold">Valid</span>
                          {:else}
                            <span class="text-red-500 font-bold">Invalid</span>
                          {/if}
                        </td>
                      </tr>
                    {/each}
                  </tbody>
                </table>
              </div>
            </div>
          {/if}
        {/if}
      </div>

      <!-- Modal Footer -->
      <div class="px-6 py-4 border-t border-outline-variant flex justify-end gap-3 bg-surface-container-low/30">
        <Button variant="outline" onclick={handleClose}>
          {importSuccessResult ? 'Done' : 'Cancel'}
        </Button>
        {#if !importSuccessResult && importPreviewData.length > 0}
          <Button
            variant="primary"
            icon="file_upload"
            disabled={isImporting || importPreviewData.filter(r => r.isValid).length === 0}
            onclick={handleImportSubmit}
          >
            {isImporting ? 'Importing...' : `Import ${importPreviewData.filter(r => r.isValid).length} Lots`}
          </Button>
        {/if}
      </div>
    </div>
  </div>
{/if}
