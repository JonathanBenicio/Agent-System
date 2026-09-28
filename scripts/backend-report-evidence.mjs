import { createHash } from 'node:crypto';

function attributes(tag) {
  return Object.fromEntries([...tag.matchAll(/([\w-]+)="([^"]*)"/g)].map(m => [m[1], m[2]]));
}

export function parseTrx(xml) {
  const tag = xml.match(/<(?:\w+:)?Counters\b[^>]*\/>/)?.[0];
  if (!tag) throw new Error('TRX Counters missing');
  const attrs = attributes(tag);
  const count = name => {
    if (!/^\d+$/.test(attrs[name] ?? '')) throw new Error('Invalid TRX counter: ' + name);
    return Number(attrs[name]);
  };
  const total = count('total'), executed = count('executed'), passed = count('passed'), failed = count('failed');
  if (executed > total || passed + failed > executed) throw new Error('Inconsistent TRX counters');
  return { passed, skipped: total - executed, failed, total, executed,
    otherOutcomes: executed - passed - failed };
}

export function parseCoverage(xml) {
  const tag = xml.match(/<coverage\b[^>]*>/)?.[0];
  if (!tag) throw new Error('Cobertura root missing');
  const attrs = attributes(tag);
  const numeric = name => {
    if (!/^(?:\d+)(?:\.\d+)?$/.test(attrs[name] ?? '')) throw new Error('Invalid coverage attribute: ' + name);
    return Number(attrs[name]);
  };
  const rate = numeric('line-rate'), branchRate = numeric('branch-rate');
  const linesCovered = numeric('lines-covered'), linesValid = numeric('lines-valid');
  if (rate > 1 || branchRate > 1 || linesCovered > linesValid) throw new Error('Inconsistent coverage values');
  return { linePercent: Number((rate * 100).toFixed(2)), branchPercent: Number((branchRate * 100).toFixed(2)),
    linesCovered, linesValid, minimum: 80, passed: rate >= 0.8 };
}

export function verifyArtifact(bytes, expectedHash, name) {
  const actual = createHash('sha256').update(bytes).digest('hex');
  if (!/^[a-f0-9]{64}$/i.test(expectedHash ?? '') || actual !== expectedHash.toLowerCase()) {
    throw new Error('Artifact differs from execution manifest: ' + name);
  }
  return actual;
}
