import { createHmac, randomUUID } from 'node:crypto';

const baseUrl = process.env.MAF_SMOKE_API_URL ?? 'http://127.0.0.1:5188';
if (baseUrl !== 'http://127.0.0.1:5188')
  throw new Error('This validation script only allows the loopback backend-validation API on port 5188.');

const secret = 'documentation-validation-jwt-secret-local-only-2026';
const tenantId = 'admin';
const userId = 'gateway-real-validation-user';
const base64url = value => Buffer.from(JSON.stringify(value)).toString('base64url');
const unsigned = `${base64url({ alg: 'HS256', typ: 'JWT' })}.${base64url({
  iss: 'AgenticSystem', aud: 'AgenticSystem', sub: userId, tenant_id: tenantId, role: 'Owner',
  exp: Math.floor(Date.now() / 1000) + 600
})}`;
const token = `${unsigned}.${createHmac('sha256', secret).update(unsigned).digest('base64url')}`;

async function request(path, method = 'GET', body) {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      'X-Tenant-Id': tenantId,
      ...(body ? { 'Content-Type': 'application/json' } : {})
    },
    body: body ? JSON.stringify(body) : undefined,
    signal: AbortSignal.timeout(20_000)
  });
  const text = await response.text();
  let data;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  if (!response.ok) throw new Error(`${method} ${path}: HTTP ${response.status} ${JSON.stringify(data)}`);
  return data;
}

if (process.argv[2] === 'start') {
  const definitionId = `wait-restart-${randomUUID()}`;
  const definition = {
    id: definitionId,
    name: 'Validation scheduled wait across API restart',
    version: 1,
    steps: [{ id: 'wait', name: 'Wait', stepType: 'wait', timeout: '00:01:00', dependsOn: [] }],
    edges: []
  };
  await request('/api/workflow/definitions', 'POST', definition);
  const started = await request(`/api/workflow/executions/start/${encodeURIComponent(definitionId)}`, 'POST', {});
  const executionId = started.executionId ?? started.id;
  const statusPath = `/api/workflow/executions/${encodeURIComponent(executionId)}`;
  const deadline = Date.now() + 20_000;
  while (Date.now() < deadline) {
    const state = await request(statusPath);
    const wait = state.stepExecutions?.find(step => step.stepId === 'wait');
    if (state.status?.toLowerCase() === 'pending' && wait?.waitUntilUtc) {
      process.stdout.write(JSON.stringify({ definitionId, executionId, waitUntilUtc: wait.waitUntilUtc, status: state.status }));
      process.exit(0);
    }
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  throw new Error('The workflow did not persist a scheduled wait before the deadline.');
}

if (process.argv[2] === 'poll') {
  const executionId = process.env.MAF_SMOKE_EXECUTION_ID;
  if (!executionId) throw new Error('Set MAF_SMOKE_EXECUTION_ID to the execution started before the API process restart.');
  const statusPath = `/api/workflow/executions/${encodeURIComponent(executionId)}`;
  const deadline = Date.now() + 45_000;
  while (Date.now() < deadline) {
    const state = await request(statusPath);
    const status = state.status?.toLowerCase();
    if (status === 'completed') {
      process.stdout.write(JSON.stringify({ executionId, status: state.status, stepCount: state.stepExecutions?.length ?? 0 }));
      process.exit(0);
    }
    if (status === 'failed' || status === 'cancelled')
      throw new Error(`Recovered execution reached ${state.status}: ${state.errorMessage ?? ''}`);
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  throw new Error('The workflow did not complete after the API process restart.');
}

throw new Error('Usage: node workflow-wait-process-restart.mjs start|poll');
