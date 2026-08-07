const API_BASE_URL = import.meta.env.VITE_API_BASE || '/api';

/** @param {Response} response */
async function handleResponse(response) {
  if (!response.ok) {
    let errorMessage = `HTTP Error ${response.status}: ${response.statusText}`;
    try {
      const errorData = await response.json();
      if (errorData.message) errorMessage = errorData.message;
      else if (typeof errorData === 'string') errorMessage = errorData;
    } catch {
      // Use fallback error message if JSON parsing fails
    }
    throw new Error(errorMessage);
  }
  if (response.status === 204) return null;
  return await response.json();
}

// 1. Fetch Manufacturing Steps
export async function getManufacturingSteps() {
  const response = await fetch(`${API_BASE_URL}/ManufacturingSteps`);
  return await handleResponse(response);
}

// 1b. Create Manufacturing Step
/** @param {any} data */
export async function createManufacturingStep(data) {
  const response = await fetch(`${API_BASE_URL}/ManufacturingSteps`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 1c. Update Manufacturing Step
/**
 * @param {string} id
 * @param {any} data
 */
export async function updateManufacturingStep(id, data) {
  const response = await fetch(`${API_BASE_URL}/ManufacturingSteps/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 2. Fetch all Production Lots
export async function getProductionLots() {
  const response = await fetch(`${API_BASE_URL}/ProductionLots`);
  return await handleResponse(response);
}

// 3. Fetch all Raw Material Lots
export async function getMaterialLots() {
  const response = await fetch(`${API_BASE_URL}/MaterialConsumptions/lots`);
  return await handleResponse(response);
}

// 4. Fetch Lot WIP summary
/** @param {string} lotId */
export async function getLotWip(lotId) {
  const response = await fetch(`${API_BASE_URL}/ProductionLots/${lotId}/wip`);
  return await handleResponse(response);
}

// 5. Fetch Lot Journey & Movements History
/** @param {string} lotId */
export async function getLotJourney(lotId) {
  const response = await fetch(`${API_BASE_URL}/ProductionLots/${lotId}/journey`);
  return await handleResponse(response);
}

// 6. Fetch Material Traceability for Lot
/** @param {string} lotId */
export async function getMaterialTraceability(lotId) {
  const response = await fetch(`${API_BASE_URL}/ProductionLots/${lotId}/material-traceability`);
  return await handleResponse(response);
}

// 7. Create New Production Lot
/** @param {any} data */
export async function createProductionLot(data) {
  const response = await fetch(`${API_BASE_URL}/ProductionLots`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 8. Move Lot Quantity Between Steps
/** @param {any} data */
export async function moveLot(data) {
  const response = await fetch(`${API_BASE_URL}/Movements`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 9. Create New Raw Material Lot
/** @param {any} data */
export async function createMaterialLot(data) {
  const response = await fetch(`${API_BASE_URL}/MaterialConsumptions/lots`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 10. Consume Raw Material for Production Lot
/** @param {any} data */
export async function consumeMaterial(data) {
  const response = await fetch(`${API_BASE_URL}/MaterialConsumptions/consume`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 11. Reject Items at Step
/**
 * @param {string} lotId
 * @param {any} data
 */
export async function rejectItems(lotId, data) {
  const response = await fetch(`${API_BASE_URL}/ProductionLots/${lotId}/reject`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 12. Reverse Movement
/**
 * @param {string} movementId
 * @param {any} data
 */
export async function reverseMovement(movementId, data) {
  const response = await fetch(`${API_BASE_URL}/Movements/${movementId}/reverse`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 13. Edit Raw Material Consumption
/**
 * @param {string} consumptionId
 * @param {any} data
 */
export async function editMaterialConsumption(consumptionId, data) {
  if (!consumptionId || consumptionId === 'undefined') {
    throw new Error('Material Consumption ID is missing or invalid.');
  }
  const response = await fetch(`${API_BASE_URL}/MaterialConsumptions/consume/${consumptionId}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 14. Fetch All Clients
export async function getClients() {
  const response = await fetch(`${API_BASE_URL}/Clients`);
  return await handleResponse(response);
}

// 15. Create New Client
/** @param {any} data */
export async function createClient(data) {
  const response = await fetch(`${API_BASE_URL}/Clients`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return await handleResponse(response);
}

// 16. Fetch Dashboard Overview
export async function getDashboardOverview() {
  const response = await fetch(`${API_BASE_URL}/Dashboards/overview`);
  return await handleResponse(response);
}

