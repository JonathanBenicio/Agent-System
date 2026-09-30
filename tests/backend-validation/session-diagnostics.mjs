import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { createHmac } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { messageSnapshot, assertMessagesPersisted } from './evidence.mjs';
const directory = process.env.BACKEND_VALIDATION_OUTPUT_DIR || resolve(import.meta.dirname, '../TestResults/backend-core-remediation/current');
const protectedHistoricalOutput = resolve(import.meta.dirname, '../TestResults/backend-documentation/current');
if (resolve(directory).toLowerCase() === protectedHistoricalOutput.toLowerCase())
  throw new Error('Refusing to overwrite historical backend-documentation validation artifacts.');
const core = JSON.parse(readFileSync(resolve(directory, 'core-results.json'), 'utf8'));
const compose = resolve(import.meta.dirname, 'compose.yml');
const tenant = core.run + '-a', otherTenant = core.run + '-b', alice = core.run + '-alice';
const phase = process.argv.includes('--after-restart') ? 'after-restart' : 'before-restart';
const results = [];
function token(user, tenantId) {
  const b64 = v => Buffer.from(JSON.stringify(v)).toString('base64url');
  const body = b64({ alg: 'HS256', typ: 'JWT' }) + '.' + b64({ iss: 'AgenticSystem', aud: 'AgenticSystem', sub: user, tenant_id: tenantId, role: 'Viewer', exp: Math.floor(Date.now()/1000)+3600 });
  return body + '.' + createHmac('sha256', 'documentation-validation-jwt-secret-local-only-2026').update(body).digest('base64url');
}
async function req(path, user = alice, tenantId = tenant, method = 'GET', body, timeoutMs = 10000) {
  const r = await fetch('http://127.0.0.1:5188'+path, { method, headers: { Authorization:'Bearer '+token(user,tenantId),'Content-Type':'application/json' }, body:body?JSON.stringify(body):undefined, signal:AbortSignal.timeout(timeoutMs) });
  const text = await r.text();
  let data;try{data=JSON.parse(text);}catch{}
  return {status:r.status,data,text,contentType:r.headers.get('content-type')};
}
async function test(id, criterion, fn) {
  try { const detail=await fn();results.push({id,criterion,result:'passed',detail}); }
  catch(e){results.push({id,criterion,result:'failed',detail:e.message});}
  console.log(id+': '+results.at(-1).result+' — '+results.at(-1).detail);
}
function check(value,detail){if(!value)throw new Error(detail);}
function sql(statement) {
  return execFileSync('docker', ['compose', '-f', compose, '-p', 'agent-system-backend-fix', 'exec', '-T', 'postgres',
    'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'validation', '-d', 'backend_validation', '-At'],
  { input: statement, encoding: 'utf8' }).trim();
}
const fixture = JSON.parse(readFileSync(resolve(directory, 'session-fixture.json'), 'utf8'));
if (fixture.run !== core.run || fixture.baseline !== core.baseline) throw new Error('Fixture belongs to another run/revision');
const previous=phase==='after-restart'?JSON.parse(readFileSync(resolve(directory,'session-before-restart.json'),'utf8')):null;
if (previous && (previous.run !== core.run || previous.baseline !== core.baseline)) {
  throw new Error('Before/after restart artifacts belong to different runs or revisions');
}
const id = fixture.sessionId;
if (previous && previous.sessionId !== id) throw new Error('Session changed between restart phases');
let messagesSnapshot;
if (!previous) {
  execFileSync('dotnet', [resolve(import.meta.dirname, 'bin/Release/net10.0/BackendDiagnostics.dll'), '--skills-before-restart'], { encoding: 'utf8' });
}
await test('SESSION-READ-'+phase,'persisted session is visible only to its owner and tenant',async()=>{
  check(id,'no persisted session');
  const own=await req('/api/session/'+id),foreign=await req('/api/session/'+id,core.run+'-bob'),cross=await req('/api/session/'+id,alice,otherTenant);
  check(own.status===200&&foreign.status===404&&cross.status===404,'statuses='+[own.status,foreign.status,cross.status]);
  if(previous)check(own.data.title==='Validation persisted title','title did not survive restart');
  return 'owner=200 other-user=404 other-tenant=404'+(previous?'; title survived restart':'');
});
if(!previous)await test('SESSION-TITLE','title change is persisted and foreign owner denied',async()=>{
  const correct=await req('/api/session/'+id+'/title',alice,tenant,'PUT',{title:'Validation persisted title'});
  const foreign=await req('/api/session/'+id+'/title',core.run+'-bob',tenant,'PUT',{title:'Forbidden'});
  check(correct.status===200&&foreign.status===404,'edit='+correct.status+' foreign='+foreign.status);
  return 'title=200 foreign=404';
});
await test('SESSION-MESSAGES-'+phase,'message ownership and nonempty content persist across restart',async()=>{
  const own=await req('/api/session/'+id+'/messages'),foreign=await req('/api/session/'+id+'/messages',core.run+'-bob');
  check(own.status===200&&Array.isArray(own.data)&&foreign.status===404,'status='+own.status+' foreign='+foreign.status);
  messagesSnapshot = messageSnapshot(own.data);
  check(own.data.some(m=>m.role==='user'&&m.content===fixture.prompt)&&own.data.some(m=>m.role==='assistant'&&m.content===fixture.answer),'known fixture messages missing');
  if (previous) assertMessagesPersisted(previous.messagesSnapshot, messagesSnapshot);
  return 'owner messages=200 other-user=404; count='+messagesSnapshot.count+(previous?'; content/IDs/order/timestamps survived restart':'; snapshot captured');
});
if(phase==='after-restart')await test('MAF-RESTORE','real chat resumes from persisted MAF state after API restart',async()=>{
  const maf=JSON.parse(readFileSync(resolve(directory,'maf-session-fixture.json'),'utf8'));
  if(maf.run!==core.run||maf.baseline!==core.baseline)throw new Error('MAF fixture belongs to another validation run/revision');
  const resumed=await req('/api/chat',alice,tenant,'POST',{message:'Continue this same conversation. Reply briefly.',sessionId:maf.sessionId,provider:'Ollama',model:'qwen2.5:0.5b'},240000);
  check(resumed.status===200&&resumed.data?.success===true&&resumed.data?.sessionId===maf.sessionId,'resume status='+resumed.status+' success='+resumed.data?.success+' session='+resumed.data?.sessionId);
  execFileSync('dotnet',[resolve(import.meta.dirname,'bin/Release/net10.0/BackendDiagnostics.dll'),'--maf-restore-verify'],{encoding:'utf8'});
  const restored=JSON.parse(readFileSync(resolve(directory,'maf-session-after-restore.json'),'utf8'));
  check(restored.result==='passed'&&restored.sessionId===maf.sessionId&&restored.restoredAt,'persisted MAF restore marker is missing');
  return 'same session resumed after process restart; MAF deserialization marker persisted; prior state hash='+maf.stateHash;
});
if (phase === 'after-restart') {
  await test('SKILL-RESTORE', 'tenant skill catalogs remain complete, isolated, and stable after API restart', async () => {
    execFileSync('dotnet', [resolve(import.meta.dirname, 'bin/Release/net10.0/BackendDiagnostics.dll'), '--skills-after-restart'], { encoding: 'utf8' });
    const restored = JSON.parse(readFileSync(resolve(directory, 'skills-after-restart.json'), 'utf8'));
    check(restored.result === 'passed' && restored.run === core.run, 'skill catalog snapshot did not match this diagnostic run');
    check(restored.quota.afterTokens > restored.quota.beforeTokens, 'post-restart real chat usage was not persisted');
    return 'four tenant-specific skill IDs per tenant survived restart; daily token total increased after resumed chat';
  });

  await test('QUOTA-RESTART', 'persisted tenant daily usage still blocks chat after API restart', async () => {
    const quotaBefore = JSON.parse(readFileSync(resolve(directory, 'skills-before-restart.json'), 'utf8')).quota;
    const state = sql(`SELECT "CurrentDailyTokens" || '|' || "CurrentDailyCostUsd" || '|' || "CurrentDailyRequests" || '|' || "MaxTokensPerDay" || '|' || "MaxDailyBudgetUsd" || '|' || "RequestsPerMinute" FROM tenant_quotas WHERE "TenantId"='${tenant}';`);
    const [tokensText, costText, requestsText, oldTokenLimitText, oldBudgetText, oldRequestsPerMinuteText] = state.split('|');
    const tokens = Number(tokensText), cost = Number(costText), requests = Number(requestsText), oldTokenLimit = Number(oldTokenLimitText);
    check(tokens >= quotaBefore.CurrentDailyTokens + 1 && requests > 0 && tokens > 1, 'persisted quota state did not advance after restart; state=' + state);
    const restrictiveLimit = tokens - 1;
    sql(`UPDATE tenant_quotas SET "MaxTokensPerDay"=${restrictiveLimit}, "UpdatedAt"=now() WHERE "TenantId"='${tenant}';`);
    try {
      const blocked = await req('/api/chat', alice, tenant, 'POST', {
        message: 'This request must be rejected by the persisted daily token ceiling.',
        provider: 'Ollama', model: 'qwen2.5:0.5b'
      }, 30000);
      const error = String(blocked.data?.errorMessage || blocked.data?.error || '');
      check(blocked.status === 429 && /Daily token quota exceeded/i.test(error),
        'expected quota-denied chat; status=' + blocked.status + ' success=' + blocked.data?.success + ' error=' + error);
      const apiKey = readFileSync(resolve(directory, 'validation-api-key.txt'), 'utf8').trim();
      check(apiKey.length > 0, 'synthetic OpenAI-compatible API key fixture is empty');
      const openAiResponse = await fetch('http://127.0.0.1:5188/v1/chat/completions', {
        method: 'POST',
        headers: { Authorization: 'Bearer ' + apiKey, 'Content-Type': 'application/json' },
        body: JSON.stringify({
          model: 'agentic-system', user: 'quota-openai-' + core.run, stream: false,
          messages: [{ role: 'user', content: 'This compatible API call must also be blocked by the persisted token ceiling.' }]
        }),
        signal: AbortSignal.timeout(30000)
      });
      let openAiData; try { openAiData = await openAiResponse.json(); } catch { openAiData = null; }
      check(openAiResponse.status === 429 && openAiData?.error?.code === 'quota_exceeded' &&
        /Daily token quota exceeded/i.test(openAiData?.error?.message || ''),
      'OpenAI-compatible endpoint did not expose quota denial: status=' + openAiResponse.status + ' body=' + JSON.stringify(openAiData));
      const streamBlocked = await req('/api/chat/stream', alice, tenant, 'POST', {
        message: 'This streaming chat must also expose the persisted token quota denial.',
        provider: 'Ollama', model: 'qwen2.5:0.5b'
      }, 30000);
      check(streamBlocked.status === 200 && streamBlocked.contentType?.startsWith('text/event-stream') &&
        /Daily token quota exceeded/i.test(streamBlocked.text),
      'SSE did not expose the persisted quota denial: status=' + streamBlocked.status + ' type=' + streamBlocked.contentType + ' body=' + streamBlocked.text);
      const after = sql(`SELECT "CurrentDailyTokens" || '|' || "CurrentDailyRequests" FROM tenant_quotas WHERE "TenantId"='${tenant}';`);
      check(after === `${tokens}|${requests}`, 'blocked request reached provider or changed persisted usage: before=' + state + ' after=' + after);
      return 'persisted usage survived restart; REST/OpenAI-compat returned quota 429 and SSE emitted a quota denial before LLM dispatch';
    } finally {
      sql(`UPDATE tenant_quotas SET "MaxTokensPerDay"=${oldTokenLimit}, "MaxDailyBudgetUsd"=${Number(oldBudgetText)}, "RequestsPerMinute"=${Number(oldRequestsPerMinuteText)}, "UpdatedAt"=now() WHERE "TenantId"='${tenant}';`);
    }
  });

  await test('QUOTA-BUDGET-RESTART', 'persisted daily cost ceiling blocks chat after API restart', async () => {
    const state = sql(`SELECT "CurrentDailyTokens" || '|' || "CurrentDailyCostUsd" || '|' || "CurrentDailyRequests" || '|' || "MaxTokensPerDay" || '|' || "MaxDailyBudgetUsd" FROM tenant_quotas WHERE "TenantId"='${tenant}';`);
    const [tokensText, costText, requestsText, tokenLimitText, oldBudgetText] = state.split('|');
    const tokens = Number(tokensText), cost = Number(costText), requests = Number(requestsText);
    check(cost > 0 && requests > 0, 'real chat did not persist cost and request counters; state=' + state);
    const budgetLimit = Math.max(0.000001, Number((cost - 0.000001).toFixed(6)));
    check(budgetLimit < cost, 'could not construct a stricter positive daily budget');
    sql(`UPDATE tenant_quotas SET "MaxTokensPerDay"=${Number(tokenLimitText)}, "MaxDailyBudgetUsd"=${budgetLimit}, "UpdatedAt"=now() WHERE "TenantId"='${tenant}';`);
    try {
      const blocked = await req('/api/chat', alice, tenant, 'POST', {
        message: 'This request must be rejected by the persisted daily budget ceiling.',
        provider: 'Ollama', model: 'qwen2.5:0.5b'
      }, 30000);
      const error = String(blocked.data?.errorMessage || blocked.data?.error || '');
      check(blocked.status === 429 && /Daily budget exceeded/i.test(error),
        'expected daily budget denial; status=' + blocked.status + ' success=' + blocked.data?.success + ' error=' + error);
      const after = sql(`SELECT "CurrentDailyTokens" || '|' || "CurrentDailyRequests" FROM tenant_quotas WHERE "TenantId"='${tenant}';`);
      check(after === `${tokens}|${requests}`, 'budget-blocked request changed persisted usage: before=' + state + ' after=' + after);
      return 'daily cost persisted across restart and the API rejected the next request before LLM dispatch';
    } finally {
      sql(`UPDATE tenant_quotas SET "MaxTokensPerDay"=${Number(tokenLimitText)}, "MaxDailyBudgetUsd"=${Number(oldBudgetText)}, "UpdatedAt"=now() WHERE "TenantId"='${tenant}';`);
    }
  });
}
writeFileSync(resolve(directory,'session-'+phase+'.json'),JSON.stringify({baseline:core.baseline,run:core.run,sessionId:id,phase,messagesSnapshot,context:'known synthetic messages saved via real PostgreSQL store; validates persistence/CRUD, not successful LLM conversation',results},null,2)+'\n');
if(results.some(r=>r.result==='failed'))process.exitCode=1;
