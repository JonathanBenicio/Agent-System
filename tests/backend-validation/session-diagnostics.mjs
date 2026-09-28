import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { createHmac } from 'node:crypto';
const directory = resolve(import.meta.dirname, '../TestResults/backend-documentation');
const core = JSON.parse(readFileSync(resolve(directory, 'core-results.json'), 'utf8'));
const tenant = core.run + '-a', otherTenant = core.run + '-b', alice = core.run + '-alice';
const phase = process.argv.includes('--after-restart') ? 'after-restart' : 'before-restart';
const results = [];
function token(user, tenantId) {
  const b64 = v => Buffer.from(JSON.stringify(v)).toString('base64url');
  const body = b64({ alg: 'HS256', typ: 'JWT' }) + '.' + b64({ iss: 'AgenticSystem', aud: 'AgenticSystem', sub: user, tenant_id: tenantId, role: 'Viewer', exp: Math.floor(Date.now()/1000)+3600 });
  return body + '.' + createHmac('sha256', 'documentation-validation-jwt-secret-local-only-2026').update(body).digest('base64url');
}
async function req(path, user = alice, tenantId = tenant, method = 'GET', body) {
  const r = await fetch('http://127.0.0.1:5188'+path, { method, headers: { Authorization:'Bearer '+token(user,tenantId),'Content-Type':'application/json' }, body:body?JSON.stringify(body):undefined, signal:AbortSignal.timeout(10000) });
  let data;try{data=await r.json();}catch{}return {status:r.status,data};
}
async function test(id, criterion, fn) {
  try { const detail=await fn();results.push({id,criterion,result:'passed',detail}); }
  catch(e){results.push({id,criterion,result:'failed',detail:e.message});}
  console.log(id+': '+results.at(-1).result+' — '+results.at(-1).detail);
}
function check(value,detail){if(!value)throw new Error(detail);}
const listed=await req('/api/session');
const previous=phase==='after-restart'?JSON.parse(readFileSync(resolve(directory,'session-before-restart.json'),'utf8')):null;
const id=previous?.sessionId||listed.data?.[0]?.id;
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
await test('SESSION-MESSAGES-'+phase,'message endpoint enforces ownership',async()=>{
  const own=await req('/api/session/'+id+'/messages'),foreign=await req('/api/session/'+id+'/messages',core.run+'-bob');
  check(own.status===200&&Array.isArray(own.data)&&foreign.status===404,'status='+own.status+' foreign='+foreign.status);
  return 'owner messages=200 other-user=404';
});
writeFileSync(resolve(directory,'session-'+phase+'.json'),JSON.stringify({baseline:core.baseline,sessionId:id,phase,context:'sessions created by failing chat; validates persistence/CRUD, not successful conversation',results},null,2)+'\n');
if(results.some(r=>r.result==='failed'))process.exitCode=1;
