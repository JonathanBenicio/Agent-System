import { createHmac, createHash, randomUUID } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { assertSessionDenied, assertHubDenied, HubAuthorizationError } from './evidence.mjs';
const root = resolve(import.meta.dirname, '../..');
const base = 'http://127.0.0.1:5188'; // Fixed loopback target: never production.
const compose = resolve(import.meta.dirname, 'compose.yml');
const sourceCommit = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim();
const outputDirectory = process.env.BACKEND_VALIDATION_OUTPUT_DIR || resolve(root, 'tests/TestResults/backend-core-remediation/current');
const protectedHistoricalOutput = resolve(root, 'tests/TestResults/backend-documentation/current');
if (resolve(outputDirectory).toLowerCase() === protectedHistoricalOutput.toLowerCase())
  throw new Error('Refusing to overwrite historical backend-documentation validation artifacts.');
mkdirSync(outputDirectory, { recursive: true });
const run = 'doc-' + randomUUID().slice(0, 8), tenantA = run + '-a', tenantB = run + '-b';
const key = randomUUID(), keyId = randomUUID(), alice = run + '-alice', bob = run + '-bob';
writeFileSync(resolve(outputDirectory, 'validation-api-key.txt'), key + '\n', { mode: 0o600 });
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
async function openHubConnection(token, hub) {
  const negotiation = await fetch(base + hub + '/negotiate?negotiateVersion=1', {
    method: 'POST', headers: { Authorization: 'Bearer ' + token }, signal: AbortSignal.timeout(10000)
  });
  check(negotiation.ok, hub + ' negotiation status=' + negotiation.status);
  const socket = new WebSocket(base.replace('http:', 'ws:') + hub + '?access_token=' + encodeURIComponent(token));
  let handshakeComplete = false;
  let nextInvocation = 1;
  const events = [];
  const invocationWaiters = new Map();
  const eventWaiters = [];
  let handshakeFailure;
  const parseMessages = event => {
    for (const part of String(event.data).split('\u001e').filter(Boolean)) {
      const message = JSON.parse(part);
      if (!handshakeComplete) {
        if (message.error) {
          handshakeComplete = true;
          handshakeFailure = new Error('hub handshake: ' + message.error);
          eventWaiters.splice(0).forEach(waiter => waiter.reject(new Error('hub handshake: ' + message.error)));
        } else if (message.type === undefined) {
          handshakeComplete = true;
        }
        continue;
      }
      if (message.type === 3 && message.invocationId && invocationWaiters.has(message.invocationId)) {
        const waiter = invocationWaiters.get(message.invocationId);
        invocationWaiters.delete(message.invocationId);
        message.error ? waiter.reject(new Error('hub invocation: ' + message.error)) : waiter.resolve(message);
      } else if (message.type === 1) {
        events.push(message);
        const matchingIndex = eventWaiters.findIndex(waiter => waiter.target === message.target && waiter.predicate(message));
        if (matchingIndex >= 0) {
          const [waiter] = eventWaiters.splice(matchingIndex, 1);
          clearTimeout(waiter.timeout);
          waiter.resolve(message);
        }
      } else if (message.error) {
        eventWaiters.splice(0).forEach(waiter => { clearTimeout(waiter.timeout); waiter.reject(new Error('hub error: ' + message.error)); });
      }
    }
  };
  const ready = new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(hub + ' handshake timeout')), 10000);
    socket.addEventListener('open', () => socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\u001e'), { once: true });
    socket.addEventListener('message', event => {
      parseMessages(event);
      if (handshakeComplete) { clearTimeout(timer); handshakeFailure ? reject(handshakeFailure) : resolve(); }
    });
    socket.addEventListener('error', () => { clearTimeout(timer); reject(new Error(hub + ' WebSocket connection failed')); }, { once: true });
    socket.addEventListener('close', () => { clearTimeout(timer); reject(new Error(hub + ' WebSocket closed')); }, { once: true });
  });
  await ready;
  return {
    async invoke(target, args = []) {
      const invocationId = String(nextInvocation++);
      const completion = new Promise((resolve, reject) => invocationWaiters.set(invocationId, { resolve, reject }));
      socket.send(JSON.stringify({ type: 1, invocationId, target, arguments: args }) + '\u001e');
      await completion;
    },
    waitForEvent(target, timeoutMs = 120000, predicate = () => true) {
      const existing = events.find(event => event.target === target && predicate(event));
      if (existing) return Promise.resolve(existing);
      const result = new Promise((resolve, reject) => {
        const waiter = { target, predicate, resolve, reject, timeout: setTimeout(() => {
          const index = eventWaiters.indexOf(waiter);
          if (index >= 0) eventWaiters.splice(index, 1);
          reject(new Error('timed out waiting for ' + target + ' on ' + hub));
        }, timeoutMs) };
        eventWaiters.push(waiter);
      });
      result.catch(() => {});
      return result;
    },
    async expectNoEvent(target, durationMs = 350) {
      try {
        await this.waitForEvent(target, durationMs);
        return false;
      } catch (error) {
        if (String(error.message).startsWith('timed out waiting for')) return true;
        throw error;
      }
    },
    close() { socket.close(); }
  };
}
await test('ENV-01', 'API liveness and migrated real PostgreSQL', async () => {
  const health = await request('/health', { token: null }); check(health.status === 200, 'health status ' + health.status);
  const migrations = sql('SELECT count(*) FROM __ef_migrations_history;'); check(Number(migrations) > 0, 'no migrations');
  return 'migrations=' + migrations + '; PostgreSQL=' + sql('SHOW server_version;') + '; vector=' + sql("SELECT extversion FROM pg_extension WHERE extname='vector';");
});
sql(`INSERT INTO tenants(id,name,slug,plan,is_active,created_at,limits,provider_api_keys,settings) VALUES
('${tenantA}','Validation A','${tenantA}','Free',true,now(),'{"maxRequestsPerMinute":10,"maxTokensPerDay":50000,"maxDailyCostUsd":1,"maxConcurrentSessions":3,"maxAgents":5,"maxDocumentsMb":100,"maxDocuments":10000}','{}','{}'),
('${tenantB}','Validation B','${tenantB}','Free',true,now(),'{"maxRequestsPerMinute":10,"maxTokensPerDay":50000,"maxDailyCostUsd":1,"maxConcurrentSessions":3,"maxAgents":5,"maxDocumentsMb":100,"maxDocuments":10000}','{}','{}');
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
await test('PLATFORM-MEMBER-01', 'platform admin can replace tenant role without changing another tenant or becoming platform admin', async () => {const token=platformJwt(platformAdmin);const changed=await request('/api/platform/tenants/'+tenantB+'/memberships/User/'+encodeURIComponent(alice),{token,method:'PUT',body:{role:'Operator'}});check(changed.status===200&&changed.data.role==='Operator','assign='+changed.status+' role='+changed.data?.role);const a=await request('/api/platform/tenants/'+tenantA+'/memberships',{token}),b=await request('/api/platform/tenants/'+tenantB+'/memberships',{token});const roleA=a.data?.find(m=>m.subjectId===alice)?.role,roleB=b.data?.find(m=>m.subjectId===alice)?.role;check(a.status===200&&b.status===200&&roleA==='Viewer'&&roleB==='Operator','roles A/B='+roleA+'/'+roleB);check(Number(sql(`SELECT count(*) FROM platform_administrators WHERE user_id='${alice}';`))===0,'tenant role promoted principal to platform admin');const denied=await request('/api/session',{token:jwt(alice,tenantB,'Viewer')});check(denied.status===200,'updated membership should authorize own tenant; status='+denied.status);return 'tenant A Viewer; tenant B Operator; user not promoted; auth succeeds in B';});
await test('PLATFORM-MEMBER-02', 'membership role validation and audit', async () => {const token=platformJwt(platformAdmin);const invalid=await request('/api/platform/tenants/'+tenantB+'/memberships/User/'+encodeURIComponent(alice),{token,method:'PUT',body:{role:'SuperAdmin'}});check(invalid.status===400,'invalid role='+invalid.status);const audit=sql(`SELECT count(*) FROM audit_entries WHERE tenant_id='${tenantB}' AND action='TenantMembershipAssigned' AND details->>'subjectId'='${alice}';`);check(Number(audit)>0,'assignment audit missing');return 'unsupported role rejected; audited assignment count='+audit;});
await test('PLATFORM-MEMBER-03', 'API key membership role stays synchronized and revocation removes tenant access', async () => {const token=platformJwt(platformAdmin);const update=await request('/api/platform/tenants/'+tenantA+'/memberships/ApiKey/'+encodeURIComponent(keyId),{token,method:'PUT',body:{role:'Operator'}});check(update.status===200&&update.data.role==='Operator','update='+update.status);check(sql(`SELECT role FROM access_api_keys WHERE id='${keyId}';`)==='Operator','API key role was not synchronized');const active=await request('/api/session',{apiKey:key,tenant:tenantA});check(active.status===200,'updated key status='+active.status);const revoke=await request('/api/platform/tenants/'+tenantA+'/memberships/ApiKey/'+encodeURIComponent(keyId),{token,method:'DELETE'});check(revoke.status===204,'revoke='+revoke.status);const denied=await request('/api/session',{apiKey:key,tenant:tenantA});check(denied.status===403,'revoked API key membership status='+denied.status);return 'API key role synchronized; membership revocation blocks tenant request';});
await test('RESOURCE-AGENT-01', 'tenant plan limits new agents but permits updating an existing agent', async () => {const createSpec=index=>({name:`tenant-agent-${run}-${index}`,description:'Tenant resource limit fixture',tier:'Specialist',domain:'validation',instructions:'Return a short validation response.',allowedTools:[],capabilities:[],autonomyLevel:'Supervised'});for(let index=0;index<5;index++){const created=await request('/api/agent/agents',{method:'POST',body:createSpec(index)});check(created.status===201,'create '+index+'='+created.status+' '+created.text);}const denied=await request('/api/agent/agents',{method:'POST',body:createSpec(5)});check(denied.status===409,'sixth agent status='+denied.status);const update=await request('/api/agent/agents/'+encodeURIComponent(createSpec(0).name),{method:'PUT',body:{...createSpec(0),description:'Updated under the same resource slot'}});check(update.status===200,'existing-agent update='+update.status);return 'Free plan created 5 dynamic agents, denied the 6th, and allowed an update';});
let supportGrant;
await test('SUPPORT-01', 'temporary support grant preserves room ACL and audits use', async () => { check(room,'dependency ROOM-01');const token=platformJwt(platformAdmin);const create=await request('/api/platform/tenants/'+tenantA+'/rooms/'+room+'/support-grants',{token,method:'POST',body:{userId:bob,reason:'Validate temporary support access',expiresAt:new Date(Date.now()+5000).toISOString()}});check(create.status===201,'create='+create.status+' '+create.text);supportGrant=create.data.id;const read=await request('/api/knowledge/rooms/'+room,{token:jwt(bob,tenantA)});check(read.status===200,'read='+read.status);const audit=sql(`SELECT count(*) FROM audit_entries WHERE tenant_id='${tenantA}' AND action='TenantSupportRoomAccessed' AND details->>'grantId'='${supportGrant}';`);check(Number(audit)>0,'support access audit missing');return 'created; ACL read succeeded; audit entries='+audit; });
await test('SUPPORT-02', 'expired temporary support grant stops room access', async () => { check(supportGrant,'dependency SUPPORT-01');await new Promise(resolve=>setTimeout(resolve,5200));const read=await request('/api/knowledge/rooms/'+room,{token:jwt(bob,tenantA)});check(read.status===404,'read='+read.status);const grant=await request('/api/platform/tenants/'+tenantA+'/rooms/'+room+'/support-grants',{token:platformJwt(platformAdmin)});check(grant.status===200&&grant.data.some(item=>item.id===supportGrant),'grant listing='+grant.status);return '404 after expiry; grant retained in registry'; });
await test('SUPPORT-03', 'revocation removes only linked temporary ACL', async () => { const token=platformJwt(platformAdmin);const create=await request('/api/platform/tenants/'+tenantA+'/rooms/'+room+'/support-grants',{token,method:'POST',body:{userId:bob,reason:'Validate revocation',expiresAt:new Date(Date.now()+60000).toISOString()}});check(create.status===201,'create='+create.status);const revoke=await request('/api/platform/tenants/'+tenantA+'/rooms/'+room+'/support-grants/'+create.data.id,{token,method:'DELETE'});check(revoke.status===204,'revoke='+revoke.status);const read=await request('/api/knowledge/rooms/'+room,{token:jwt(bob,tenantA)});check(read.status===404,'read='+read.status);return '204; subsequent ACL read denied'; });
await test('LLM-01', 'real local Ollama generation', async () => { const r=await fetch('http://127.0.0.1:11435/api/generate',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({model:'qwen2.5:0.5b',prompt:'Say hello.',stream:false}),signal:AbortSignal.timeout(120000)});const d=await r.json();check(r.ok&&d.response?.length&&d.done,'generation failed');return 'model='+d.model+'; outputTokens='+d.eval_count; });
await test('RAG-01', 'real embedding generation', async () => { const r=await fetch('http://127.0.0.1:11435/api/embed',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({model:'nomic-embed-text',input:'Documentation validation.'}),signal:AbortSignal.timeout(120000)});const d=await r.json();check(r.ok&&d.embeddings?.[0]?.length,'embedding failed');return 'dimensions='+d.embeddings[0].length; });
let ragDocumentId, ragPhrase, ragSession;
await test('RAG-02', 'ingest authorized room content and isolate tenant stats', async () => { check(room,'dependency ROOM-01');ragPhrase='CITRUS-'+run.slice(-6).toUpperCase();const content=`Authorized room knowledge. The project validation access phrase is ${ragPhrase}. When asked for the access phrase, return ${ragPhrase}. This document belongs only to the room owner and must not appear in other tenants or rooms. `.repeat(12);const form=new FormData();form.set('file',new Blob([content],{type:'text/plain'}),'validation-'+run+'.txt');const r=await request('/api/document/ingest?source='+run+'&roomId='+encodeURIComponent(room),{method:'POST',body:form});ragDocumentId=r.data?.documentId;check(r.status===200&&r.data?.chunksCreated>0,'ingest status='+r.status+'; '+(r.data?.error||''));const a=await request('/api/document/stats'),b=await request('/api/document/stats',{token:jwt(alice,tenantB)});check(a.data?.totalChunks>0&&b.data?.totalChunks===0,'stats isolation failed');const metadata=sql(`SELECT count(*) FROM vector_documents WHERE "TenantId"='${tenantA}' AND metadata->>'room_id'='${room}';`);check(Number(metadata)>0,'room_id metadata was not stored in PostgreSQL');const usage=sql(`SELECT count(DISTINCT metadata->>'document_id') || ':' || COALESCE(sum(NULLIF(metadata->>'source_bytes','')::bigint),0) FROM vector_documents WHERE "TenantId"='${tenantA}' AND metadata->>'room_id'='${room}';`);check(usage===`1:${Buffer.byteLength(content)}`,'persisted logical document/byte usage='+usage);return 'ingest 200; room chunks='+metadata+'; logical document/raw bytes='+usage+'; B chunks=0'; });
await test('RAG-03', 'authorized room context reaches a real chat completion', async () => {check(room&&ragPhrase&&ragDocumentId,'dependency RAG-02');const r=await request('/api/chat',{method:'POST',timeout:240000,body:{message:'What is the validation access phrase in the selected knowledge room? Return the exact phrase.',context:{'rag.knowledgeRoomId':room},provider:'Ollama',model:'qwen2.5:0.5b'}});ragSession=r.data?.sessionId;check(r.status===200&&r.data?.success===true&&ragSession,'chat status='+r.status+' success='+r.data?.success+' content='+(r.data?.content||r.data?.error||''));const artifacts=sql(`SELECT count(*) FROM runtime_artifacts a JOIN vector_documents d ON a.data::text LIKE '%' || d.id || '%' WHERE a."TenantId"='${tenantA}' AND a.session_id='${ragSession}' AND a.type='RagContext' AND d."TenantId"='${tenantA}' AND d.metadata->>'room_id'='${room}';`);check(Number(artifacts)>0,'chat session has no persisted RAG artifact referencing the authorized room chunk');check(String(r.data.content||'').toUpperCase().includes(ragPhrase),'completed chat response omitted the authorized room phrase; response='+(r.data.content||''));return 'real chat completed with the authorized room chunk in its RAG artifact and the phrase in its answer';});
let session;
await test('CHAT-01', 'synchronous chat succeeds and creates session', async () => { const r=await request('/api/chat',{method:'POST',body:{message:'Responda brevemente: olá.',provider:'Ollama',model:'qwen2.5:0.5b'}});session=r.data?.sessionId;check(r.status===200&&r.data?.success===true&&r.data.content?.length&&session,'status='+r.status+'; success='+r.data?.success+'; session='+Boolean(session)+'; error='+(r.data?.errorMessage||r.data?.error||''));return '200 success=true; session created'; });
await test('SESSION-01', 'session persisted and other user/tenant denied', async () => { check(session,'dependency CHAT-01');const own=await request('/api/session/'+session);const other=await request('/api/session/'+session,{token:jwt(bob,tenantA)});const cross=await request('/api/session/'+session,{token:jwt(alice,tenantB)});check(own.status===200&&other.status===404&&cross.status===404,'own/other/cross='+[own.status,other.status,cross.status]);return '200/404/404'; });
await test('CHAT-02', 'other user cannot resume session', async () => { check(session,'dependency CHAT-01');const r=await request('/api/chat',{token:jwt(bob,tenantA),method:'POST',body:{message:'Continue.',sessionId:session,provider:'Ollama',model:'qwen2.5:0.5b'}});return assertSessionDenied(r); });
await test('SSE-01', 'SSE terminates successfully with structured events', async () => { const r=await request('/api/chat/stream',{method:'POST',body:{message:'Diga olá brevemente.',provider:'Ollama',model:'qwen2.5:0.5b'}});check(r.status===200&&r.contentType?.startsWith('text/event-stream'),'status='+r.status);const events=[...r.text.matchAll(/^data: (.+)$/gm)].map(m=>JSON.parse(m[1]));const terminal=events.findLast(e=>e.Type===22);check(terminal&&terminal.Data?.success===true,'successful terminal event missing; terminal='+JSON.stringify(terminal)+'; errors='+JSON.stringify(events.filter(e=>e.Type===23).map(e=>({message:e.Message,data:e.Data}))));return 'events='+events.length+'; successful SessionCompleted; Type numeric/PascalCase confirmed'; });
await test('HUB-01', 'valid SignalR gateway invocation', async () => { const events=await hubInvoke(jwt(alice,tenantA),null,'/hubs/gateway','GetDashboard',[]);check(events.some(e=>e.target==='DashboardUpdate'),'no DashboardUpdate');return 'handshake/invocation completed'; });
await test('HUB-02', 'query tenant override rejected', async () => { check(results.find(r=>r.id==='HUB-01')?.result==='passed','dependency HUB-01');return assertHubDenied(()=>hubInvoke(jwt(alice,tenantA),tenantB,'/hubs/gateway','GetDashboard',[])); });
await test('HUB-03', 'unknown query tenant rejected', async () => { check(results.find(r=>r.id==='HUB-01')?.result==='passed','dependency HUB-01');return assertHubDenied(()=>hubInvoke(jwt(alice,tenantA),run+'-unknown','/hubs/gateway','GetDashboard',[])); });
await test('HUB-04', 'SignalR chat returns terminal success', async () => { const events=await hubInvoke(jwt(alice,tenantA),null,'/hubs/chat','SendMessage',['Olá.',null,'Ollama','qwen2.5:0.5b',null,null,null]);const terminal=events.find(e=>e.target==='ReceiveMessage');check(terminal?.arguments?.[0]?.success===true,'no successful ReceiveMessage; events='+events.map(e=>e.target).filter(Boolean));return 'ReceiveMessage success=true'; });
await test('HUB-CROSS-CHAT', 'session notifications do not cross tenants for the same user', async () => {
  check(session, 'dependency CHAT-01');
  const a = await openHubConnection(jwt(alice, tenantA), '/hubs/chat');
  const b = await openHubConnection(jwt(alice, tenantB), '/hubs/chat');
  try {
    const own = a.waitForEvent('SessionUpdated');
    const foreign = b.expectNoEvent('SessionUpdated', 500);
    const changed = await request('/api/session/' + session + '/title', { method: 'PUT', body: { title: 'Tenant scoped notification' } });
    check(changed.status === 200, 'title status=' + changed.status);
    const event = await own;
    check(await foreign, 'SessionUpdated crossed tenants');
    check(event.arguments?.[0] === session, 'session event id mismatch');
    return 'same subject connected to A/B; SessionUpdated delivered only to A';
  } finally { a.close(); b.close(); }
});
await test('HUB-CROSS-GATEWAY', 'gateway invocation result is not delivered to another tenant connection', async () => {
  const a = await openHubConnection(jwt(alice, tenantA), '/hubs/gateway');
  const b = await openHubConnection(jwt(alice, tenantB), '/hubs/gateway');
  try {
    const own = a.waitForEvent('DashboardUpdate');
    const foreign = b.expectNoEvent('DashboardUpdate', 700);
    await a.invoke('GetDashboard');
    const event = await own;
    check(await foreign, 'DashboardUpdate crossed tenants');
    check(event.target === 'DashboardUpdate', 'gateway dashboard response missing');
    return 'same subject connected to A/B; GetDashboard response went only to invoking connection; broadcaster groups are tenant scoped';
  } finally {
    a.close(); b.close();
  }
});
await test('HUB-CROSS-EXTERNAL', 'external-agent results do not cross tenant orchestrator groups', async () => {
  const a = await openHubConnection(jwt(alice, tenantA), '/hubs/external-agent');
  const b = await openHubConnection(jwt(alice, tenantB), '/hubs/external-agent');
  try {
    await a.invoke('JoinAsOrchestrator');
    await b.invoke('JoinAsOrchestrator');
    const own = a.waitForEvent('TaskResultReceived');
    const foreign = b.expectNoEvent('TaskResultReceived', 500);
    await a.invoke('ReportTaskResult', ['task-' + run, 'Completed', 'tenant A result']);
    const event = await own;
    check(await foreign, 'TaskResultReceived crossed tenants');
    check(event.arguments?.[0]?.result === 'tenant A result', 'A result was not received');
    return 'A orchestrator received its result; B with same subject did not';
  } finally { a.close(); b.close(); }
});
await test('HUB-CROSS-WORKFLOW', 'workflow events stay inside the execution tenant group', async () => {
  const definitionId = 'workflow-' + run;
  const definition = { id: definitionId, name: 'Tenant delivery fixture', steps: [{ id: 'approval', name: 'Approval fixture', stepType: 'Approval' }] };
  const saved = await request('/api/workflow/definitions', { token: jwt(alice, tenantA), tenant: tenantA, method: 'POST', body: definition });
  check(saved.status === 200, 'save=' + saved.status);
  const started = await request('/api/workflow/executions/start/' + definitionId, { token: jwt(alice, tenantA), tenant: tenantA, method: 'POST', body: {} });
  check(started.status === 202 && started.data?.executionId, 'start=' + started.status);
  const executionId = started.data.executionId;
  const a = await openHubConnection(jwt(alice, tenantA), '/hubs/workflow');
  const b = await openHubConnection(jwt(alice, tenantB), '/hubs/workflow');
  try {
    await a.invoke('SubscribeToWorkflow', [executionId]);
    await b.invoke('SubscribeToWorkflow', [executionId]);
    const own = a.waitForEvent('ExecutionCancelled');
    const foreign = b.expectNoEvent('ExecutionCancelled', 500);
    const cancelled = await request('/api/workflow/executions/' + executionId + '/cancel?reason=tenant-isolation-test', { token: jwt(alice, tenantA), tenant: tenantA, method: 'POST' });
    check(cancelled.status === 200, 'cancel=' + cancelled.status);
    await own;
    check(await foreign, 'ExecutionCancelled crossed tenants');
    return 'same execution id subscribed in A/B; cancellation event reached A only';
  } finally { a.close(); b.close(); }
});
await test('HUB-CROSS-ONNX', 'ONNX job updates stay inside the tenant group', async () => {
  const a = await openHubConnection(jwt(alice, tenantA), '/hubs/onnx');
  const b = await openHubConnection(jwt(alice, tenantB), '/hubs/onnx');
  let modelId;
  let jobId;
  try {
    await a.invoke('SubscribeToTenant', [tenantA]);
    await b.invoke('SubscribeToTenant', [tenantB]);
    const form = new FormData();
    form.set('file', new Blob([Buffer.from('invalid-onnx-fixture')], { type: 'application/octet-stream' }), 'fixture-' + run + '.onnx');
    form.set('name', 'fixture-' + run);
    form.set('inputNodeName', 'input');
    form.set('outputNodeName', 'output');
    form.set('inputWidth', '1');
    form.set('inputHeight', '1');
    form.set('channels', '3');
    const upload = await request('/api/onnx/models', { method: 'POST', body: form });
    check(upload.status === 201, 'upload=' + upload.status + ' ' + upload.text);
    modelId = upload.data.id;
    const own = a.waitForEvent('JobStatusChanged', 30000, event => event.arguments?.[0]?.status === 'Failed');
    const foreign = b.expectNoEvent('JobStatusChanged', 1500);
    const image = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aHckAAAAASUVORK5CYII=';
    const imageForm = new FormData();
    imageForm.set('imageData', image);
    const job = await request('/api/onnx/models/' + modelId + '/test', { method: 'POST', body: imageForm });
    check(job.status === 202, 'test job=' + job.status + ' ' + job.text);
    jobId = job.data?.jobId;
    const event = await own;
    check(await foreign, 'JobStatusChanged crossed tenants');
    check(event.arguments?.[0]?.tenantId === tenantA, 'ONNX payload tenant=' + event.arguments?.[0]?.tenantId);
    return 'invalid model deliberately fails in worker; job event reached A only';
  } finally {
    a.close(); b.close();
    if (jobId) await request('/api/onnx/models/jobs/' + jobId, { method: 'DELETE' });
    if (modelId) await request('/api/onnx/models/' + modelId, { method: 'DELETE' });
  }
});
await test('RATE-01', 'HTTP rate limit and tenant partition', async () => { const statuses=[];for(let i=0;i<35;i++)statuses.push((await request('/api/chat',{method:'POST',body:{message:''}})).status);const b=await request('/api/chat',{token:jwt(alice,tenantB),method:'POST',body:{message:''}});check(statuses.includes(429)&&b.status===400,'429 present='+statuses.includes(429)+'; tenant B='+b.status);return '429 observed; other tenant 400 validation'; });
const directory=outputDirectory;mkdirSync(directory,{recursive:true});
writeFileSync(resolve(directory,'core-results.json'),JSON.stringify({baseline:sourceCommit,run,services:'real PostgreSQL/pgvector and Ollama; no mock LLM',chatSessionId:session,ragSessionId:ragSession,ragDocumentId,ragRoomId:room,ragPhrase,results},null,2)+'\n');
console.log(JSON.stringify({passed:results.filter(r=>r.result==='passed').length,failed:results.filter(r=>r.result==='failed').length}));
if(results.some(r=>r.result==='failed'))process.exitCode=1;
