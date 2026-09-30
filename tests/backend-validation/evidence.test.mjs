import test from 'node:test';
import assert from 'node:assert/strict';
import { assertSessionDenied, assertHubDenied, HubAuthorizationError, messageSnapshot, assertMessagesPersisted } from './evidence.mjs';
import { parseTrx, parseCoverage, verifyArtifact } from '../../scripts/backend-report-evidence.mjs';

test('provider failures and HTTP 200 success=false cannot certify session authorization', () => {
  for (const status of [200, 400, 401, 429, 500, 503]) {
    assert.throws(() => assertSessionDenied({ status, data: { success: false } }));
  }
  for (const status of [403, 404]) assert.match(assertSessionDenied({ status }), /explicit denial/);
});

test('hub timeout, disconnect, parse failure and generic server errors never certify isolation', async () => {
  for (const error of [new Error('hub timeout'), new Error('hub closed'), new SyntaxError('invalid JSON'), new Error('LLM Unauthorized')]) {
    await assert.rejects(assertHubDenied(async () => { throw error; }), e => e === error);
  }
  await assert.rejects(assertHubDenied(async () => []), /accepted/);
  assert.match(await assertHubDenied(async () => { throw new HubAuthorizationError('HTTP 403'); }), /explicit/);
});

const messages = [
  { id: 'user_1', role: 'user', content: 'known prompt', timestamp: '2026-09-28T00:00:00Z' },
  { id: 'assistant_1', role: 'assistant', content: 'known answer', timestamp: '2026-09-28T00:00:00Z' }
];
test('restart rejects missing, lost, modified, reordered and replaced messages', () => {
  const before = messageSnapshot(messages);
  assert.doesNotThrow(() => assertMessagesPersisted(before, messageSnapshot(structuredClone(messages))));
  assert.throws(() => messageSnapshot([]));
  assert.throws(() => messageSnapshot([{ role: 'user', content: 'missing identity' }]));
  for (const after of [messages.slice(0, 1), [...messages].reverse(), messages.map(m => ({ ...m, content: 'changed' })), messages.map(m => ({ ...m, id: 'other' }))]) {
    assert.throws(() => assertMessagesPersisted(before, messageSnapshot(after)));
  }
  assert.throws(() => assertMessagesPersisted(undefined, before));
});

test('TRX failures, skips and incomplete outcomes come from counters instead of fixed totals', () => {
  assert.deepEqual(parseTrx('<Counters total="5" executed="4" passed="2" failed="1" />'),
    { total: 5, executed: 4, passed: 2, failed: 1, skipped: 1, otherOutcomes: 1 });
  assert.equal(parseTrx('<t:Counters total="1" executed="1" passed="0" failed="1" />').failed, 1);
  for (const xml of ['<TestRun/>', '<Counters total="1" executed="2" passed="1" failed="0" />', '<Counters total="x" />']) {
    assert.throws(() => parseTrx(xml));
  }
});

test('coverage percentages and denominator reflect the supplied artifact', () => {
  const coverage = parseCoverage('<coverage line-rate="0.5" branch-rate="0.25" lines-covered="10" lines-valid="20">');
  assert.equal(coverage.linePercent, 50);
  assert.equal(coverage.branchPercent, 25);
  assert.equal(coverage.linesValid, 20);
  assert.equal(coverage.passed, false);
  assert.throws(() => parseCoverage('<coverage line-rate="NaN">'));
});

test('manifest rejects corrupted or unrelated artifacts even with plausible contents', () => {
  const hash = 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad';
  assert.equal(verifyArtifact(Buffer.from('abc'), hash, 'TRX'), hash);
  assert.throws(() => verifyArtifact(Buffer.from('new run'), hash, 'TRX'), /differs/);
  assert.throws(() => verifyArtifact(Buffer.from('abc'), undefined, 'TRX'), /differs/);
});
