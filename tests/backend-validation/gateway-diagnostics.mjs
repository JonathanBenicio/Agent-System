import { createHmac } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const directory = process.env.BACKEND_VALIDATION_OUTPUT_DIR
  || resolve(import.meta.dirname, '../TestResults/backend-core-remediation/current');
const core = JSON.parse(readFileSync(resolve(directory, 'core-results.json'), 'utf8'));
const base = 'http://127.0.0.1:5188';
const tenantA = core.run + '-a';
const tenantB = core.run + '-b';
const userId = core.run + '-alice';
const platformAdminId = core.run + '-platform-admin';
const serviceName = 'validation-gateway-fixture';
const results = [];

function token(tenantId, subject = userId) {
  const b64 = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const body = b64({ alg: 'HS256', typ: 'JWT' }) + '.' + b64({
    iss: 'AgenticSystem', aud: 'AgenticSystem', sub: subject,
    tenant_id: tenantId, role: 'Viewer', exp: Math.floor(Date.now() / 1000) + 3600
  });
  return body + '.' + createHmac('sha256', 'documentation-validation-jwt-secret-local-only-2026')
    .update(body).digest('base64url');
}

async function request(route, tenantId, method = 'GET', subject = userId) {
  const response = await fetch(base + route, {
    method,
    headers: { Authorization: 'Bearer ' + token(tenantId, subject) },
    signal: AbortSignal.timeout(15000)
  });
  let data;
  try { data = await response.json(); } catch { data = null; }
  return { status: response.status, data };
}

function check(condition, message) {
  if (!condition) throw new Error(message);
}

async function openHubConnection(accessToken) {
  const hub = '/hubs/gateway';
  const negotiation = await fetch(base + hub + '/negotiate?negotiateVersion=1', {
    method: 'POST', headers: { Authorization: 'Bearer ' + accessToken },
    signal: AbortSignal.timeout(10000)
  });
  check(negotiation.ok, 'Gateway negotiation status=' + negotiation.status);

  const socket = new WebSocket(base.replace('http:', 'ws:') + hub + '?access_token=' + encodeURIComponent(accessToken));
  let handshakeComplete = false;
  let nextInvocation = 1;
  let handshakeFailure;
  const events = [];
  const invocationWaiters = new Map();
  const eventWaiters = [];

  const onMessage = event => {
    for (const part of String(event.data).split('\u001e').filter(Boolean)) {
      const message = JSON.parse(part);
      if (!handshakeComplete) {
        if (message.error) {
          handshakeComplete = true;
          handshakeFailure = new Error('Gateway handshake: ' + message.error);
        } else if (message.type === undefined) {
          handshakeComplete = true;
        }
        continue;
      }
      if (message.type === 3 && message.invocationId && invocationWaiters.has(message.invocationId)) {
        const waiter = invocationWaiters.get(message.invocationId);
        invocationWaiters.delete(message.invocationId);
        message.error ? waiter.reject(new Error(message.error)) : waiter.resolve(message);
      } else if (message.type === 1) {
        events.push(message);
        const index = eventWaiters.findIndex(waiter => waiter.target === message.target && waiter.predicate(message));
        if (index >= 0) {
          const [waiter] = eventWaiters.splice(index, 1);
          clearTimeout(waiter.timer);
          waiter.resolve(message);
        }
      }
    }
  };

  const ready = new Promise((resolveReady, rejectReady) => {
    const timer = setTimeout(() => rejectReady(new Error('Gateway handshake timed out')), 10000);
    socket.addEventListener('open', () => socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\u001e'), { once: true });
    socket.addEventListener('message', event => {
      onMessage(event);
      if (handshakeComplete) {
        clearTimeout(timer);
        handshakeFailure ? rejectReady(handshakeFailure) : resolveReady();
      }
    });
    socket.addEventListener('error', () => { clearTimeout(timer); rejectReady(new Error('Gateway WebSocket failed')); }, { once: true });
    socket.addEventListener('close', () => { clearTimeout(timer); rejectReady(new Error('Gateway WebSocket closed')); }, { once: true });
  });
  await ready;

  return {
    waitForEvent(target, timeoutMs = 10000, predicate = () => true) {
      const existing = events.find(event => event.target === target && predicate(event));
      if (existing) return Promise.resolve(existing);
      return new Promise((resolveEvent, rejectEvent) => {
        const waiter = { target, predicate, resolve: resolveEvent, reject: rejectEvent };
        waiter.timer = setTimeout(() => {
          const index = eventWaiters.indexOf(waiter);
          if (index >= 0) eventWaiters.splice(index, 1);
          rejectEvent(new Error('Timed out waiting for ' + target));
        }, timeoutMs);
        eventWaiters.push(waiter);
      });
    },
    async expectNoEvent(target, timeoutMs = 500, predicate = () => true) {
      try { await this.waitForEvent(target, timeoutMs, predicate); return false; }
      catch (error) {
        if (String(error.message).startsWith('Timed out waiting for ')) return true;
        throw error;
      }
    },
    async invoke(target, args = []) {
      const invocationId = String(nextInvocation++);
      const completion = new Promise((resolveInvocation, rejectInvocation) =>
        invocationWaiters.set(invocationId, { resolve: resolveInvocation, reject: rejectInvocation }));
      socket.send(JSON.stringify({ type: 1, invocationId, target, arguments: args }) + '\u001e');
      await completion;
    },
    close() { socket.close(); }
  };
}

async function verify() {
  const viewerDashboard = await request('/api/admin/gateway/dashboard', tenantA);
  check(viewerDashboard.status === 403,
    'tenant Viewer accessed platform Gateway dashboard; status=' + viewerDashboard.status);
  const viewerDenied = await request('/api/admin/gateway/services/' + serviceName + '/disable', tenantA, 'POST');
  check(viewerDenied.status === 403, 'tenant Viewer changed a global Gateway service; status=' + viewerDenied.status);
  const initial = await request('/api/admin/gateway/services/' + serviceName, tenantA, 'GET', platformAdminId);
  check(initial.status === 200 && initial.data?.isEnabled === true,
    'Validation Gateway fixture missing or not enabled; status=' + initial.status);

  const a = await openHubConnection(token(tenantA, platformAdminId));
  const b = await openHubConnection(token(tenantB, platformAdminId));
  try {
    const isServiceEvent = enabled => event =>
      event.arguments?.[0]?.serviceName === serviceName && event.arguments?.[0]?.enabled === enabled;
    const missingA = a.expectNoEvent('ServiceStatusChanged', 300, event => event.arguments?.[0]?.serviceName === 'not-registered');
    const missingB = b.expectNoEvent('ServiceStatusChanged', 300, event => event.arguments?.[0]?.serviceName === 'not-registered');
    const missing = await request('/api/admin/gateway/services/not-registered/disable', tenantA, 'POST', platformAdminId);
    check(missing.status === 404, 'unregistered Gateway service should return 404; status=' + missing.status);
    check(await missingA && await missingB, 'unregistered service produced a status broadcast');

    const disabledA = a.waitForEvent('ServiceStatusChanged', 10000, isServiceEvent(false));
    const disabledB = b.expectNoEvent('ServiceStatusChanged', 1000, isServiceEvent(false));
    const disable = await request('/api/admin/gateway/services/' + serviceName + '/disable', tenantA, 'POST', platformAdminId);
    check(disable.status === 204, 'Disable status=' + disable.status);
    const disabledEvent = await disabledA;
    check(await disabledB, 'Disable event crossed into tenant B');
    const disabledPayload = disabledEvent.arguments?.[0];
    check(disabledPayload.enabled === false && disabledPayload.tenantId === tenantA,
      'Disable event has wrong state/tenant: ' + JSON.stringify(disabledPayload));

    const disabledStatus = await request('/api/admin/gateway/services/' + serviceName, tenantA, 'GET', platformAdminId);
    check(disabledStatus.status === 200 && disabledStatus.data?.isEnabled === false,
      'Gateway service did not persist disabled status');

    const enabledA = a.waitForEvent('ServiceStatusChanged', 10000, isServiceEvent(true));
    const enabledB = b.expectNoEvent('ServiceStatusChanged', 1000, isServiceEvent(true));
    const enable = await request('/api/admin/gateway/services/' + serviceName + '/enable', tenantA, 'POST', platformAdminId);
    check(enable.status === 204, 'Enable status=' + enable.status);
    const enabledEvent = await enabledA;
    check(await enabledB, 'Enable event crossed into tenant B');
    const enabledPayload = enabledEvent.arguments?.[0];
    check(enabledPayload.enabled === true && enabledPayload.tenantId === tenantA,
      'Enable event has wrong state/tenant: ' + JSON.stringify(enabledPayload));

    const enabledStatus = await request('/api/admin/gateway/services/' + serviceName, tenantA, 'GET', platformAdminId);
    check(enabledStatus.status === 200 && enabledStatus.data?.isEnabled === true,
      'Gateway service did not persist enabled status');
    return 'real Gateway service toggled; ServiceStatusChanged disable/enable reached tenant A only; tenant B received neither event';
  } finally {
    a.close();
    b.close();
  }
}

try {
  const detail = await verify();
  results.push({ id: 'HUB-CROSS-GATEWAY', result: 'passed', detail });
} catch (error) {
  results.push({ id: 'HUB-CROSS-GATEWAY', result: 'failed', detail: error.message });
}
writeFileSync(resolve(directory, 'gateway-results.json'), JSON.stringify({ baseline: core.baseline, run: core.run, results }, null, 2) + '\n');
console.log('HUB-CROSS-GATEWAY: ' + results[0].result + ' — ' + results[0].detail);
if (results.some(result => result.result !== 'passed')) process.exitCode = 1;
