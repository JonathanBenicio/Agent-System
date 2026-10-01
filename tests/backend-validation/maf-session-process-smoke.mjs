import { createHmac } from 'node:crypto';

const baseUrl = 'http://127.0.0.1:5188';
const secret = 'documentation-validation-jwt-secret-local-only-2026';
const tenantId = 'admin';
const userId = 'gateway-real-validation-user';
const sessionId = process.env.MAF_SMOKE_SESSION_ID || undefined;
const base64url = value => Buffer.from(JSON.stringify(value)).toString('base64url');
const unsigned = `${base64url({ alg: 'HS256', typ: 'JWT' })}.${base64url({
  iss: 'AgenticSystem',
  aud: 'AgenticSystem',
  sub: userId,
  tenant_id: tenantId,
  role: 'Owner',
  exp: Math.floor(Date.now() / 1000) + 600
})}`;
const token = `${unsigned}.${createHmac('sha256', secret).update(unsigned).digest('base64url')}`;

const response = await fetch(`${baseUrl}/api/chat`, {
  method: 'POST',
  headers: {
    Authorization: `Bearer ${token}`,
    'X-Tenant-Id': tenantId,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    Message: 'Responda em português com uma frase curta: o modelo está ativo?',
    Provider: 'Ollama',
    Model: 'qwen2.5:0.5b',
    SessionId: sessionId
  }),
  signal: AbortSignal.timeout(180_000)
});

const body = await response.json();
if (!response.ok || body.success !== true || !body.sessionId || !body.content) {
  throw new Error(`HTTP ${response.status}: ${JSON.stringify(body)}`);
}

process.stdout.write(JSON.stringify({
  success: body.success,
  sessionId: body.sessionId,
  agentName: body.agentName,
  content: body.content
}));
