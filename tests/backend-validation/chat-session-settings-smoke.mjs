import { createHmac } from 'node:crypto';
import { readFile } from 'node:fs/promises';

const baseUrl = 'http://127.0.0.1:5188';
const secret = 'documentation-validation-jwt-secret-local-only-2026';
const tenantId = 'admin';
const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');

function token(userId) {
  const unsigned = `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({
    iss: 'AgenticSystem', aud: 'AgenticSystem', sub: userId, tenant_id: tenantId,
    exp: Math.floor(Date.now() / 1000) + 600
  })}`;
  return `${unsigned}.${createHmac('sha256', secret).update(unsigned).digest('base64url')}`;
}

async function request(userId, method, path, body, expectedStatus) {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token(userId)}`,
      'X-Tenant-Id': tenantId,
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' })
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(180_000)
  });
  const raw = response.status === 204 ? '' : await response.text();
  let value = null;
  try { value = raw ? JSON.parse(raw) : null; }
  catch { throw new Error(`${method} ${path} returned invalid JSON: ${raw.slice(0, 300)}`); }
  if (expectedStatus ? response.status !== expectedStatus : !response.ok)
    throw new Error(`${method} ${path} returned ${response.status}: ${JSON.stringify(value)}`);
  process.stderr.write(`${method} ${path} -> ${response.status}\n`);
  return value;
}

const owner = 'chat-123-owner';
const viewer = 'chat-123-viewer';
let sessionId;
let secondSessionId;
let keyId;
let activeKey = 'synthetic-chat-123-secret';
let skillId;

try {
  const apiKeyLogin = await fetch(`${baseUrl}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ apiKey: 'documentation-validation-bootstrap-only' }),
    signal: AbortSignal.timeout(10_000)
  });
  const apiKeyIdentity = await apiKeyLogin.json();
  if (!apiKeyLogin.ok || !apiKeyIdentity.userId || apiKeyIdentity.tenantId !== tenantId)
    throw new Error(`API key login did not return the principal and tenant: ${JSON.stringify(apiKeyIdentity)}`);

  const catalog = await request(owner, 'GET', '/api/chat/configuration');
  const provider = catalog.providers.find(item => item.name.toLowerCase() === 'openai');
  if (!provider) throw new Error('The OpenAI-compatible validation provider is unavailable.');

  const key = await request(owner, 'POST', `/api/admin/llm/providers/${encodeURIComponent(provider.name)}/keys`,
    { name: 'chat-123-validation-key', apiKey: 'synthetic-chat-123-secret', isDefault: false });
  keyId = key.id;
  if (JSON.stringify(key).includes('synthetic-chat-123-secret')) throw new Error('The saved key was returned in plaintext.');
  const keyValidation = await request(owner, 'POST', `/api/admin/llm/providers/${encodeURIComponent(provider.name)}/keys/${encodeURIComponent(keyId)}/test`);
  if (!keyValidation.success) throw new Error(`The provider rejected the key: ${JSON.stringify(keyValidation)}`);
  const discovered = await request(owner, 'POST', `/api/admin/llm/providers/${encodeURIComponent(provider.name)}/keys/${encodeURIComponent(keyId)}/discover-models`);
  if (!discovered.success || !discovered.discoveredModels.includes('validation-model'))
    throw new Error(`The provider models were not discovered from the tenant key: ${JSON.stringify(discovered)}`);
  activeKey = 'synthetic-chat-123-updated';
  const updatedKey = await request(owner, 'PUT', `/api/admin/llm/providers/${encodeURIComponent(provider.name)}/keys/${encodeURIComponent(keyId)}`,
    { apiKey: activeKey });
  if (JSON.stringify(updatedKey).includes(activeKey)) throw new Error('The updated key was returned in plaintext.');
  const updatedValidation = await request(owner, 'POST', `/api/admin/llm/providers/${encodeURIComponent(provider.name)}/keys/${encodeURIComponent(keyId)}/test`);
  if (!updatedValidation.success) throw new Error(`The updated provider key did not validate: ${JSON.stringify(updatedValidation)}`);
  const updatedModels = await request(owner, 'POST', `/api/admin/llm/providers/${encodeURIComponent(provider.name)}/keys/${encodeURIComponent(keyId)}/discover-models`);
  if (!updatedModels.success || !updatedModels.discoveredModels.includes('validation-model'))
    throw new Error('The updated key did not retain provider model discovery.');
  await request(owner, 'POST', `/api/admin/llm/providers/${encodeURIComponent(provider.name)}/keys/${encodeURIComponent(keyId)}/default`);
  const model = 'validation-model';
  await request(owner, 'PUT', '/api/chat/configuration', { provider: provider.name, model });
  const saved = await request(owner, 'GET', '/api/chat/configuration');
  if (saved.preferredProvider !== provider.name || saved.preferredModel !== model)
    throw new Error('The preference was not persisted.');

  const created = await request(owner, 'POST', '/api/session');
  sessionId = created.id;
  if (!sessionId) throw new Error('The server did not create a session ID.');
  await request(viewer, 'GET', `/api/session/${encodeURIComponent(sessionId)}`, undefined, 404);

  const skillMarker = 'CHAT_123_ACTIVE_SKILL_MARKER';
  const skill = await request(owner, 'POST', '/api/agent/skills', {
    id: `chat-123-skill-${Date.now()}`,
    name: 'Chat 123 Runtime Validation Skill',
    domain: 'general',
    type: 'Instruction',
    systemPromptFragment: skillMarker,
  });
  skillId = skill.id;

  const userPrompts = ['chat-123-marker-primeiro-turno', 'chat-123-marker-segundo-turno'];
  for (let index = 0; index < userPrompts.length; index++) {
    const chat = await request(owner, 'POST', '/api/chat', {
      message: userPrompts[index],
      sessionId
    });
    if (!chat.success || chat.sessionId !== sessionId || !chat.content)
      throw new Error(`Chat did not use the persisted session: ${JSON.stringify(chat)}`);
  }
  const stubAudit = (await readFile(`${process.env.BACKEND_VALIDATION_OUTPUT_DIR}/openai-compatible-stub.jsonl`, 'utf8'))
    .trim().split(/\r?\n/).filter(Boolean).map(line => JSON.parse(line));
  const effectiveCalls = stubAudit.filter(item => item.path === '/v1/chat/completions');
  if (effectiveCalls.length < 2 || effectiveCalls.slice(-2).some(item =>
    item.authorization !== `Bearer ${activeKey}` || item.model !== model))
    throw new Error(`Saved BYOK/model did not reach the chat provider: ${JSON.stringify(effectiveCalls.slice(-2))}`);
  const resumedContext = JSON.stringify(effectiveCalls.at(-1)?.messages ?? []);
  if (!resumedContext.includes(userPrompts[0]))
    throw new Error('The resumed chat did not include the previous turn in the provider request.');
  if (!JSON.stringify(effectiveCalls.slice(-12)).includes(skillMarker))
    throw new Error('The active tenant skill was not present in the MAF chat requests.');
  const messages = await request(owner, 'GET', `/api/session/${encodeURIComponent(sessionId)}/messages`);
  if (messages.length < userPrompts.length * 2) throw new Error(`Expected two persisted turns, found ${messages.length} messages.`);
  const detail = await request(owner, 'GET', `/api/session/${encodeURIComponent(sessionId)}`);
  if (detail.provider !== provider.name || detail.model !== model)
    throw new Error('The applied model was not recorded in the session.');

  await request(viewer, 'POST', `/api/admin/llm/providers/${encodeURIComponent(provider.name)}/keys`,
    { name: 'forbidden', apiKey: 'forbidden' }, 403);
  await request(viewer, 'PUT', '/api/admin/llm/providers/' + encodeURIComponent(provider.name) + '/keys/' + encodeURIComponent(keyId),
    { apiKey: 'forbidden-update' }, 403);
  const keys = await request(owner, 'GET', `/api/admin/llm/providers/${encodeURIComponent(provider.name)}/keys`);
  if (!keys.some(item => item.id === keyId) || JSON.stringify(keys).includes(activeKey) || JSON.stringify(keys).includes('synthetic-chat-123-secret'))
    throw new Error('The key list is missing the saved key or exposed its secret.');

  await request(owner, 'PUT', `/api/agent/skills/${encodeURIComponent(skillId)}/enabled`, { enabled: false });
  const disabled = await request(owner, 'GET', '/api/agent/skills/all');
  if (disabled.find(item => item.id === skillId)?.isEnabled !== false)
    throw new Error('Skill activation state was not persisted.');
  await request(viewer, 'PUT', `/api/agent/skills/${encodeURIComponent(skillId)}/enabled`,
    { enabled: true }, 403);

  const disabledSession = await request(owner, 'POST', '/api/session');
  secondSessionId = disabledSession.id;
  const beforeDisabledChat = effectiveCalls.length;
  const afterDisable = await request(owner, 'POST', '/api/chat', {
    message: 'chat-123-marker-skill-disabled', sessionId: secondSessionId
  });
  if (!afterDisable.success) throw new Error(`Chat after skill disable failed: ${JSON.stringify(afterDisable)}`);
  const updatedAudit = (await readFile(`${process.env.BACKEND_VALIDATION_OUTPUT_DIR}/openai-compatible-stub.jsonl`, 'utf8'))
    .trim().split(/\r?\n/).filter(Boolean).map(line => JSON.parse(line))
    .filter(item => item.path === '/v1/chat/completions');
  if (JSON.stringify(updatedAudit.slice(beforeDisabledChat)).includes(skillMarker))
    throw new Error('A disabled tenant skill still reached the new MAF chat session.');

  const ended = await request(owner, 'POST', `/api/session/${encodeURIComponent(sessionId)}/end`);
  if (!ended.endedAt) throw new Error('The session was not ended.');
  await request(owner, 'POST', '/api/chat', { message: 'Não responder', sessionId }, 404);
  const archived = await request(owner, 'GET', `/api/session/${encodeURIComponent(sessionId)}/messages`);
  if (archived.length < userPrompts.length * 2) throw new Error('Ending the session removed its history.');
  await request(owner, 'POST', `/api/session/${encodeURIComponent(secondSessionId)}/end`);

  process.stdout.write(JSON.stringify({ sessionId, model, messages: archived.length,
    credentialSecretExposed: false, skillActivationAffectedPrompt: true, viewerDenied: true,
    endedHistoryPreserved: true, providerCredentialAndModelUsed: true }));
} finally {
  if (skillId) await request(owner, 'DELETE', `/api/agent/skills/${encodeURIComponent(skillId)}`);
  if (keyId) await request(owner, 'DELETE', `/api/admin/llm/providers/OpenAI/keys/${encodeURIComponent(keyId)}`);
  if (sessionId) await request(owner, 'DELETE', `/api/session/${encodeURIComponent(sessionId)}`);
  if (secondSessionId) await request(owner, 'DELETE', `/api/session/${encodeURIComponent(secondSessionId)}`);
}
