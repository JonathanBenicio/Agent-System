import { createHash } from 'node:crypto';

export function assertSessionDenied(response) {
  if (![403, 404].includes(response.status)) {
    throw new Error(`Session denial not proven: HTTP ${response.status}; success=${response.data?.success}`);
  }
  return `explicit denial HTTP ${response.status}`;
}

export class HubAuthorizationError extends Error {}

export async function assertHubDenied(invoke) {
  try { await invoke(); }
  catch (error) {
    if (error instanceof HubAuthorizationError) return 'explicit hub authorization denial';
    throw error; // Timeout, disconnect, malformed JSON and provider errors are failures.
  }
  throw new Error('Tenant accepted and hub invocation completed');
}

export function messageSnapshot(messages) {
  if (!Array.isArray(messages) || !messages.length ||
      !messages.some(m => m.role === 'user' && typeof m.content === 'string' && m.content.trim())) {
    throw new Error('No nonempty user message to verify persistence');
  }
  const fields = messages.map(m => {
    if (typeof m.id !== 'string' || typeof m.role !== 'string' || typeof m.content !== 'string' || typeof m.timestamp !== 'string') {
      throw new Error('Incomplete message payload');
    }
    return { id: m.id, role: m.role, content: m.content, timestamp: m.timestamp };
  });
  return { count: fields.length, sha256: createHash('sha256').update(JSON.stringify(fields)).digest('hex') };
}

export function assertMessagesPersisted(before, after) {
  if (!before?.count || !before.sha256 || before.count !== after.count || before.sha256 !== after.sha256) {
    throw new Error('Message content/identity/order did not survive restart or prior snapshot is missing');
  }
}
