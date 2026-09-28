import { createHmac, createHash, randomUUID } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { assertSessionDenied, assertHubDenied, HubAuthorizationError } from './evidence.mjs';
const root = resolve(import.meta.dirname, '../..');
const base = 'http://127.0.0.1:5188'; // Fixed loopback target: never production.
const compose = resolve(import.meta.dirname, 'compose.yml');
const outputDirectory = process.env.BACKEND_VALIDATION_OUTPUT_DIR || resolve(root, 'tests/TestResults/backend-core-remediation/current');
const protectedHistoricalOutput = resolve(root, 'tests/TestResults/backend-documentation/current');
if (resolve(outputDirectory).toLowerCase() === protectedHistoricalOutput.toLowerCase())
  throw new Error('Refusing to overwrite historical backend-documentation validation artifacts.');
const run = 'doc-' + randomUUID().slice(0, 8), tenantA = run + '-a', tenantB = run + '-b';
const key = randomUUID(), keyId = randomUUID(), alice = run + '-alice', bob = run + '-bob';
const results = [];
function sql(statement) {
  return execFileSync('docker', ['compose', '-f', compose, '-p', 'agent-system-backend-fix', 'exec', '-T', 'postgres', 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'validation', '-d', 'backend_validation', '-At'], { input: statement, encoding: 'utf8' }).trim();
}
function jwt(user, tenant, role = 'Viewer') {
  const b64 = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const text = b64({ alg: 'HS256', typ: 'JWT' }) + '.' + b64({ iss: 'AgenticSystem', aud: 'AgenticSystem', sub: user, tenant_id: tenant, role, exp: Math.floor(Date.now()/1000) + 3600 });
  return text + '.' + createHmac('sha256', 'documentation-validation-jwt-secret-local-only-2026').update(text).digest('base64url');
}
function platformJwt(user) {
  const b64 = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const text = b64({ alg: 'HS256', typ: 'JWT' }) + '.' + b64({ iss: 'AgenticSystem', aud: 'AgenticSystem', sub: user, tenant_id: 'platform-unscoped', role: 'Viewer', exp: Math.floor(Date.now()/1000) + 3600 });
  return text + '.' + createHmac('sha256', 'documentation-validation-jwt-secret-local-only-2026').update(text).digest('base64url');
}
async function request(route, { token = jwt(alice, tenantA), tenant, apiKey, method = 'GET', body, timeout = 120000 } = {}) {
  const headers = apiKey ? { 'X-Api-Key': apiKey } : token ? { Authorization: 'Bearer ' + token } : {};
  if (tenant) headers['X-Tenant-Id'] = tenant;
  if (body && !(body instanceof FormData)) headers['Content-Type'] = 'application/json';
  const response = await fetch(base + route, { method, headers, body: body instanceof FormData ? body : body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(timeout) });
  const text = await response.text(); let data; try { data = JSON.parse(text); } catch { data = null; }
  return { status: response.status, data, text, contentType: response.headers.get('content-type') };
}
function check(condition, detail) { if (!condition) throw new Error(detail); }
async function test(id, criterion, fn) {
  const started = Date.now();
  try { const detail = await fn(); results.push({ id, criterion, result: 'passed', detail, durationMs: Date.now()-started }); }
  catch (error) { results.push({ id, criterion, result: String(error.message).startsWith('dependency') ? 'not_executed' : 'failed', detail: String(error.message).replaceAll(key, '[redacted]'), durationMs: Date.now()-started }); }
  console.log(id + ': ' + results.at(-1).result + ' — ' + results.at(-1).detail);
}
async function hubInvoke(token, tenantQuery, hub, target, args) {
  const messages = []; let socket;
  const query = tenantQuery ? '&X-Tenant-Id=' + encodeURIComponent(tenantQuery) : '';
  const negotiation = await fetch(base + hub + '/negotiate?negotiateVersion=1' + query, {
    method: 'POST', headers: { Authorization: 'Bearer ' + token }, signal: AbortSignal.timeout(10000)
  });
  if (negotiation.status === 403) throw new HubAuthorizationError('HTTP 403 at hub negotiation');
  check(negotiation.ok, 'hub negotiation status=' + negotiation.status);
  try {
    await new Promise((ok, reject) => {
      socket = new WebSocket(base.replace('http:', 'ws:') + hub + '?access_token=' + encodeURIComponent(token) + (tenantQuery ? '&X-Tenant-Id=' + encodeURIComponent(tenantQuery) : ''));
      const timeout = setTimeout(() => reject(new Error('hub timeout')), 120000);
      socket.onopen = () => socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\u001e');
      socket.onerror = () => { clearTimeout(timeout); reject(new Error('hub connection rejected')); };
      socket.onclose = () => { clearTimeout(timeout); reject(new Error('hub closed before completion')); };
      socket.onmessage = event => {
        for (const part of String(event.data).split('\u001e').filter(Boolean)) {
          let message;
          try { message = JSON.parse(part); }
          catch (error) { clearTimeout(timeout); reject(error); return; }
          messages.push(message);
          if (message.type === undefined && !message.error) socket.send(JSON.stringify({ type: 1, invocationId: '1', target, arguments: args }) + '\u001e');
          if (message.error) {
            clearTimeout(timeout);
            const explicitTenantDenial = /^Strict Multi-Tenancy Violation: (?:No active Tenant Context resolved for this hub invocation\.|A valid Tenant Context is required to connect to this Hub\.)$/.test(message.error);
            reject(explicitTenantDenial ? new HubAuthorizationError(message.error) : new Error('hub error: ' + message.error));
          }
          if (message.type === 3) { clearTimeout(timeout); ok(); }
        }
      };
    });
    return messages;
  } finally { socket?.close(); }
}
await test('ENV-01', 'API liveness and migrated real PostgreSQL', async () => {
  const health = await request('/health', { token: null }); check(health.status === 200, 'health status ' + health.status);
  const migrations = sql('SELECT count(*) FROM __ef_migrations_history;'); check(Number(migrations) > 0, 'no migrations');
  return 'migrations=' + migrations + '; PostgreSQL=' + sql('SHOW server_version;') + '; vector=' + sql("SELECT extversion FROM pg_extension WHERE extname='vector';");
});
sql(`INSERT INTO tenants(id,name,slug,plan,is_active,created_at,limits,provider_api_keys,settings) VALUES
('${tenantA}','Validation A','${tenantA}','Free',true,now(),'{}','{}','{}'),
('${tenantB}','Validation B','${tenantB}','Free',true,now(),'{}','{}','{}');
INSERT INTO access_api_keys(id,tenant_id,key_hash,name,role,is_enabled,created_at) VALUES
('${keyId}','${tenantA}','${createHash('sha256').update(key).digest('hex')}','Validation Viewer','Viewer',true,now());`);
sql(`INSERT INTO tenant_memberships(id,subject_id,subject_type,role,tenant_id,granted_at)
VALUES ('${run}-alice-a','${alice}','User','Viewer','${tenantA}',now()),
('${run}-bob-a','${bob}','User','Viewer','${tenantA}',now()),
('${run}-alice-b','${alice}','User','Viewer','${tenantB}',now()),
('${run}-alice-key','${keyId}','ApiKey','Viewer','${tenantA}',now());`);
await test('AUTH-01', 'invalid key denied for known tenant', async () => { const r=await request('/api/session',{apiKey:'invalid',tenant:tenantA}); check(r.status===401,'status='+r.status); return '401'; });
await test('AUTH-02', 'JWT tenant fallback accepted', async () => { const r=await request('/api/session'); check(r.status===200&&Array.isArray(r.data),'status='+r.status); return '200 array'; });
await test('AUTH-03', 'non-admin header override denied', async () => { const r=await request('/api/session',{tenant:tenantB}); check(r.status===403,'status='+r.status); return '403'; });
await test('AUTH-04', 'unknown tenant JWT denied', async () => { const r=await request('/api/session',{token:jwt(alice,run+'-unknown')}); check(r.status===403,'status='+r.status); return '403'; });
await test('AUTH-05', 'Viewer API key cannot override tenant', async () => { const r=await request('/api/session',{apiKey:key,tenant:tenantB}); check(r.status===403,'Viewer key accepted cross-tenant header: status='+r.status); return '403'; });
let room;
await test('ROOM-01', 'create private room and creator access', async () => { const r=await request('/api/knowledge/rooms',{method:'POST',body:{name:'Validation room'}}); check(r.status===201&&r.data?.id,'status='+r.status);room=r.data.id;return '201; creator owns room'; });
await test('ROOM-02', 'same tenant without ACL denied', async () => { check(room,'dependency ROOM-01');const r=await request('/api/knowledge/rooms/'+room,{token:jwt(bob,tenantA)});check(r.status===404,'status='+r.status);return '404'; });
await test('ROOM-03', 'other tenant denied', async () => { check(room,'dependency ROOM-01');const r=await request('/api/knowledge/rooms/'+room,{token:jwt(alice,tenantB)});check(r.status===403||r.status===404,'status='+r.status);return String(r.status)+'; no cross-tenant room access'; });
await test('ROOM-04', 'Reader grant/read then edit denied', async () => { check(room,'dependency ROOM-01');const grant=await request('/api/knowledge/rooms/'+room+'/permissions',{method:'POST',body:{userId:bob,role:'reader'}});check(grant.status===200,'grant='+grant.status);const read=await request('/api/knowledge/rooms/'+room,{token:jwt(bob,tenantA)});check(read.status===200,'read='+read.status);const edit=await request('/api/knowledge/rooms/'+room,{token:jwt(bob,tenantA),method:'PUT',body:{id:room,name:'Forbidden'}});check(edit.status===403,'edit='+edit.status);return 'grant 200/read 200/edit 403'; });
await test('ROOM-05', 'revocation blocks subsequent read', async () => { check(room,'dependency ROOM-01');const r=await request('/api/knowledge/rooms/'+room+'/permissions/'+bob,{method:'DELETE'});check(r.status===204,'revoke='+r.status);const read=await request('/api/knowledge/rooms/'+room,{token:jwt(bob,tenantA)});check(read.status===404,'read='+read.status);return '204 then 404'; });
const platformAdmin = run + '-platform-admin';
sql(`INSERT INTO platform_administrators(user_id,granted_at,granted_by) VALUES ('${platformAdmin}',now(),'validation-fixture');`);
await test('PLATFORM-01', 'tenant admins cannot use platform administration', async () => { const r=await request('/api/platform/tenants',{token:jwt(alice,tenantA)});check(r.status===403,'status='+r.status);return '403'; });
await test('PLATFORM-ACCESS-01', 'platform administration grants no implicit tenant content access', async () => { const r=await request('/api/knowledge/rooms/'+room,{token:platformJwt(platformAdmin)});check(r.status===403,'status='+r.status);return '403; no tenant membership or room ACL'; });
await test('PLATFORM-02', 'plan changes preserve stricter configured tenant limits', async () => { const token=platformJwt(platformAdmin);const list=await request('/api/platform/tenants',{token});const before=list.data?.find(item=>item.id===tenantB);check(list.status===200&&before,'list='+list.status);const changed=await request('/api/platform/tenants/'+tenantB+'/plan',{token,method:'PUT',body:{plan:'Pro'}});const plan=String(changed.data?.plan).toLowerCase();check(changed.status===200&&plan==='pro'&&changed.data.limits.maxTokensPerDay===before.limits.maxTokensPerDay,'update='+changed.status+' plan='+plan+' configured limit changed');const reset=await request('/api/platform/tenants/'+tenantB+'/plan',{token,method:'PUT',body:{plan:'Free'}});check(reset.status===200&&String(reset.data.plan).toLowerCase()==='free','restore='+reset.status);return 'list 200; Pro ceiling applied; configured limits preserved; restored Free'; });
let supportGrant;
await test('SUPPORT-01', 'temporary support grant preserves room ACL and audits use', async () => { check(room,'dependency ROOM-01');const token=platformJwt(platformAdmin);const create=await request('/api/platform/tenants/'+tenantA+'/rooms/'+room+'/support-grants',{token,method:'POST',body:{userId:bob,reason:'Validate temporary support access',expiresAt:new Date(Date.now()+5000).toISOString()}});check(create.status===201,'create='+create.status+' '+create.text);supportGrant=create.data.id;const read=await request('/api/knowledge/rooms/'+room,{token:jwt(bob,tenantA)});check(read.status===200,'read='+read.status);const audit=sql(`SELECT count(*) FROM audit_entries WHERE tenant_id='${tenantA}' AND action='TenantSupportRoomAccessed' AND details->>'grantId'='${supportGrant}';`);check(Number(audit)>0,'support access audit missing');return 'created; ACL read succeeded; audit entries='+audit; });
await test('SUPPORT-02', 'expired temporary support grant stops room access', async () => { check(supportGrant,'dependency SUPPORT-01');await new Promise(resolve=>setTimeout(resolve,5200));const read=await request('/api/knowledge/rooms/'+room,{token:jwt(bob,tenantA)});check(read.status===404,'read='+read.status);const grant=await request('/api/platform/tenants/'+tenantA+'/rooms/'+room+'/support-grants',{token:platformJwt(platformAdmin)});check(grant.status===200&&grant.data.some(item=>item.id===supportGrant),'grant listing='+grant.status);return '404 after expiry; grant retained in registry'; });
await test('SUPPORT-03', 'revocation removes only linked temporary ACL', async () => { const token=platformJwt(platformAdmin);const create=await request('/api/platform/tenants/'+tenantA+'/rooms/'+room+'/support-grants',{token,method:'POST',body:{userId:bob,reason:'Validate revocation',expiresAt:new Date(Date.now()+60000).toISOString()}});check(create.status===201,'create='+create.status);const revoke=await request('/api/platform/tenants/'+tenantA+'/rooms/'+room+'/support-grants/'+create.data.id,{token,method:'DELETE'});check(revoke.status===204,'revoke='+revoke.status);const read=await request('/api/knowledge/rooms/'+room,{token:jwt(bob,tenantA)});check(read.status===404,'read='+read.status);return '204; subsequent ACL read denied'; });
await test('LLM-01', 'real local Ollama generation', async () => { const r=await fetch('http://127.0.0.1:11435/api/generate',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({model:'qwen2.5:0.5b',prompt:'Say hello.',stream:false}),signal:AbortSignal.timeout(120000)});const d=await r.json();check(r.ok&&d.response?.length&&d.done,'generation failed');return 'model='+d.model+'; outputTokens='+d.eval_count; });
await test('RAG-01', 'real embedding generation', async () => { const r=await fetch('http://127.0.0.1:11435/api/embed',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({model:'nomic-embed-text',input:'Documentation validation.'}),signal:AbortSignal.timeout(120000)});const d=await r.json();check(r.ok&&d.embeddings?.[0]?.length,'embedding failed');return 'dimensions='+d.embeddings[0].length; });
await test('RAG-02', 'upload and tenant stats isolation', async () => { const form=new FormData();form.set('file',new Blob(['Validation document: the project has tenant-specific knowledge.'],{type:'text/plain'}),'validation-'+run+'.txt');const r=await request('/api/document/ingest?source='+run,{method:'POST',body:form});check(r.status===200&&r.data?.chunksCreated>0,'ingest status='+r.status+'; '+(r.data?.error||''));const a=await request('/api/document/stats'),b=await request('/api/document/stats',{token:jwt(alice,tenantB)});check(a.data?.totalChunks>0&&b.data?.totalChunks===0,'stats isolation failed');return 'ingest 200; A chunks='+a.data.totalChunks+'; B chunks=0'; });
let session;
await test('CHAT-01', 'synchronous chat succeeds and creates session', async () => { const r=await request('/api/chat',{method:'POST',body:{message:'Responda brevemente: olá.',provider:'Ollama',model:'qwen2.5:0.5b'}});session=r.data?.sessionId;check(r.status===200&&r.data?.success===true&&r.data.content?.length&&session,'status='+r.status+'; success='+r.data?.success+'; session='+Boolean(session)+'; error='+(r.data?.errorMessage||r.data?.error||''));return '200 success=true; session created'; });
await test('SESSION-01', 'session persisted and other user/tenant denied', async () => { check(session,'dependency CHAT-01');const own=await request('/api/session/'+session);const other=await request('/api/session/'+session,{token:jwt(bob,tenantA)});const cross=await request('/api/session/'+session,{token:jwt(alice,tenantB)});check(own.status===200&&other.status===404&&cross.status===404,'own/other/cross='+[own.status,other.status,cross.status]);return '200/404/404'; });
await test('CHAT-02', 'other user cannot resume session', async () => { check(session,'dependency CHAT-01');const r=await request('/api/chat',{token:jwt(bob,tenantA),method:'POST',body:{message:'Continue.',sessionId:session,provider:'Ollama',model:'qwen2.5:0.5b'}});return assertSessionDenied(r); });
await test('SSE-01', 'SSE terminates successfully with structured events', async () => { const r=await request('/api/chat/stream',{method:'POST',body:{message:'Diga olá brevemente.',provider:'Ollama',model:'qwen2.5:0.5b'}});check(r.status===200&&r.contentType?.startsWith('text/event-stream'),'status='+r.status);const events=[...r.text.matchAll(/^data: (.+)$/gm)].map(m=>JSON.parse(m[1]));const terminal=events.findLast(e=>e.Type===22);check(terminal&&terminal.Data?.success===true,'successful terminal event missing; terminal='+JSON.stringify(terminal)+'; errors='+JSON.stringify(events.filter(e=>e.Type===23).map(e=>({message:e.Message,data:e.Data}))));return 'events='+events.length+'; successful SessionCompleted; Type numeric/PascalCase confirmed'; });
await test('HUB-01', 'valid SignalR gateway invocation', async () => { const events=await hubInvoke(jwt(alice,tenantA),null,'/hubs/gateway','GetDashboard',[]);check(events.some(e=>e.target==='DashboardUpdate'),'no DashboardUpdate');return 'handshake/invocation completed'; });
await test('HUB-02', 'query tenant override rejected', async () => { check(results.find(r=>r.id==='HUB-01')?.result==='passed','dependency HUB-01');return assertHubDenied(()=>hubInvoke(jwt(alice,tenantA),tenantB,'/hubs/gateway','GetDashboard',[])); });
await test('HUB-03', 'unknown query tenant rejected', async () => { check(results.find(r=>r.id==='HUB-01')?.result==='passed','dependency HUB-01');return assertHubDenied(()=>hubInvoke(jwt(alice,tenantA),run+'-unknown','/hubs/gateway','GetDashboard',[])); });
await test('HUB-04', 'SignalR chat returns terminal success', async () => { const events=await hubInvoke(jwt(alice,tenantA),null,'/hubs/chat','SendMessage',['Olá.',null,'Ollama','qwen2.5:0.5b',null,null,null]);const terminal=events.find(e=>e.target==='ReceiveMessage');check(terminal?.arguments?.[0]?.success===true,'no successful ReceiveMessage; events='+events.map(e=>e.target).filter(Boolean));return 'ReceiveMessage success=true'; });
await test('RATE-01', 'HTTP rate limit and tenant partition', async () => { const statuses=[];for(let i=0;i<35;i++)statuses.push((await request('/api/chat',{method:'POST',body:{message:''}})).status);const b=await request('/api/chat',{token:jwt(alice,tenantB),method:'POST',body:{message:''}});check(statuses.includes(429)&&b.status===400,'429 present='+statuses.includes(429)+'; tenant B='+b.status);return '429 observed; other tenant 400 validation'; });
const directory=outputDirectory;mkdirSync(directory,{recursive:true});
writeFileSync(resolve(directory,'core-results.json'),JSON.stringify({baseline:'f8de7a6e3aa9d671a67ae60f2f52e0c1f80b3eae',run,services:'real PostgreSQL/pgvector and Ollama; no mock LLM',results},null,2)+'\n');
console.log(JSON.stringify({passed:results.filter(r=>r.result==='passed').length,failed:results.filter(r=>r.result==='failed').length}));
if(results.some(r=>r.result==='failed'))process.exitCode=1;
