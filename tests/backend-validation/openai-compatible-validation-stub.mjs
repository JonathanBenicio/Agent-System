import { createServer } from 'node:http';
import { appendFile } from 'node:fs/promises';

const output = process.env.BACKEND_VALIDATION_OUTPUT_DIR;
if (!output || !output.toLowerCase().includes('chat-session-settings-123'))
  throw new Error('Set BACKEND_VALIDATION_OUTPUT_DIR to the chat-session-settings-123 validation directory.');

const auditPath = `${output}/openai-compatible-stub.jsonl`;
const audit = [];
const server = createServer(async (request, response) => {
  const chunks = [];
  for await (const chunk of request) chunks.push(chunk);
  const raw = Buffer.concat(chunks).toString('utf8');
  const body = raw ? JSON.parse(raw) : null;
  if (request.url === '/_audit' && request.method === 'GET') {
    response.writeHead(200, { 'Content-Type': 'application/json' });
    response.end(JSON.stringify(audit));
    return;
  }
  if (request.url === '/v1/models' && request.method === 'GET') {
    const item = { path: request.url, authorization: request.headers.authorization };
    audit.push(item);
    await appendFile(auditPath, JSON.stringify(item) + '\n');
    response.writeHead(200, { 'Content-Type': 'application/json' });
    response.end(JSON.stringify({ data: [{ id: 'validation-model' }] }));
    return;
  }
  if (request.url === '/v1/chat/completions' && request.method === 'POST') {
    const item = { path: request.url, authorization: request.headers.authorization, model: body?.model, messages: body?.messages ?? [] };
    audit.push(item);
    await appendFile(auditPath, JSON.stringify(item) + '\n');
    response.writeHead(200, { 'Content-Type': 'application/json' });
    const lastUserMessage = body?.messages?.filter(message => message.role === 'user').at(-1)?.content ?? '';
    response.end(JSON.stringify({
      id: 'validation-chat', object: 'chat.completion', created: Math.floor(Date.now() / 1000),
      model: body?.model ?? 'validation-model',
      choices: [{ index: 0, message: { role: 'assistant', content: `Resposta validada pelo provider local: ${lastUserMessage}` }, finish_reason: 'stop' }],
      usage: { prompt_tokens: 7, completion_tokens: 6, total_tokens: 13 }
    }));
    return;
  }
  response.writeHead(404, { 'Content-Type': 'application/json' });
  response.end(JSON.stringify({ error: 'not_found' }));
});

server.listen(5190, '127.0.0.1', () => process.stdout.write('listening 127.0.0.1:5190'));
