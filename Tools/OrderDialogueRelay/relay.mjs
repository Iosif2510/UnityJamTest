import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { createInterface } from 'node:readline/promises';
import { Writable } from 'node:stream';
import { pathToFileURL } from 'node:url';

const host = '127.0.0.1';
const port = 8765;
const schemaVersion = 1;
const model = 'gpt-5.4-mini-2026-03-17';
const reactionKeys = [
  'rcv_ok', 'rcv_bottle', 'rcv_ink', 'rcv_both',
  'aft_cured', 'aft_cured_ink', 'aft_odd', 'aft_slime',
];
const keys = ['symptom', ...reactionKeys];
const prompt = readFileSync(new URL('./order-dialogue.ko.txt', import.meta.url), 'utf8').trim();
const format = {
  type: 'json_schema',
  name: 'order_dialogue',
  strict: true,
  schema: {
    type: 'object',
    properties: Object.fromEntries(keys.map(key => [key, { type: 'string' }])),
    required: keys,
    additionalProperties: false,
  },
};
const promptRevision = createHash('sha256').update(prompt).update(JSON.stringify(format)).digest('hex').slice(0, 16);

export function openAiRequest(order) {
  const { character, disease, examples, symptomTargetLength } = order;
  return {
    model,
    store: false,
    max_output_tokens: 1600,
    prompt_cache_key: `potion-order-v1-${promptRevision}`,
    input: [
      { role: 'developer', content: prompt },
      { role: 'user', content: JSON.stringify({ character, disease, examples, symptomTargetLength }) },
    ],
    text: { format },
  };
}

class RelayError extends Error {
  constructor(status, reason) {
    super(reason);
    this.status = status;
  }
}

function isObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function text(value, limit = 300) {
  return typeof value === 'string' && value.length <= limit;
}

function textList(value, maxItems, maxLength) {
  return Array.isArray(value) && value.length <= maxItems && value.every(item => text(item, maxLength));
}

function hasOnlyKeys(value, expected) {
  return isObject(value) && Object.keys(value).length === expected.length &&
    expected.every(key => Object.hasOwn(value, key));
}

function validOrder(order) {
  if (!hasOnlyKeys(order, ['requestId', 'schemaVersion', 'dataRevision', 'character', 'disease', 'examples', 'symptomTargetLength']))
    return false;
  const { requestId, dataRevision, character, disease, examples, symptomTargetLength } = order;
  return text(requestId, 32) && /^[0-9a-f]{32}$/.test(requestId) && order.schemaVersion === schemaVersion &&
    text(dataRevision, 64) && /^[A-Za-z0-9._-]+$/.test(dataRevision) &&
    Number.isInteger(symptomTargetLength) && symptomTargetLength > 0 && symptomTargetLength <= 300 &&
    hasOnlyKeys(character, ['code', 'name', 'job', 'speechStyle', 'personality', 'chatTopics']) &&
    text(character.code, 32) && text(character.name, 40) && text(character.job, 80) &&
    text(character.speechStyle) && text(character.personality) && textList(character.chatTopics, 3, 120) &&
    hasOnlyKeys(disease, ['id', 'name', 'symptomMeanings', 'lineExamples', 'bannedWords']) &&
    text(disease.id, 32) && text(disease.name, 80) &&
    textList(disease.symptomMeanings, 3, 180) && disease.symptomMeanings.length === 3 &&
    textList(disease.lineExamples, 3, 180) && disease.lineExamples.length === 3 &&
    textList(disease.bannedWords, 32, 80) &&
    hasOnlyKeys(examples, ['symptom', 'reactions']) && text(examples.symptom, 1200) &&
    hasOnlyKeys(examples.reactions, reactionKeys) && Object.values(examples.reactions).every(line => text(line, 300));
}

async function readOrder(req) {
  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > 16 * 1024)
      throw new RelayError(413, 'request_too_large');
    chunks.push(chunk);
  }
  try {
    const order = JSON.parse(Buffer.concat(chunks).toString('utf8'));
    if (!validOrder(order))
      throw new RelayError(400, 'invalid_request');
    return order;
  } catch (error) {
    if (error instanceof SyntaxError)
      throw new RelayError(400, 'invalid_json');
    throw error;
  }
}

async function generateWithOpenAi(order, signal) {
  const key = process.env.OPENAI_API_KEY?.trim();
  if (!key)
    throw new RelayError(503, 'key_missing');

  const response = await fetch('https://api.openai.com/v1/responses', {
    method: 'POST',
    headers: { authorization: `Bearer ${key}`, 'content-type': 'application/json' },
    body: JSON.stringify(openAiRequest(order)),
    redirect: 'error',
    signal,
  });
  if (!response.ok) {
    await response.body?.cancel();
    throw new RelayError(502, `openai_http_${response.status}`);
  }

  const result = await response.json();
  const content = result.output?.filter(item => item.type === 'message').flatMap(item => item.content) ?? [];
  if (content.some(item => item.type === 'refusal'))
    throw new RelayError(502, 'openai_refusal');
  const output = content.filter(item => item.type === 'output_text');
  if (result.status !== 'completed' || output.length !== 1)
    throw new RelayError(502, 'openai_incomplete');
  let lines;
  try {
    lines = JSON.parse(output[0].text);
  } catch {
    throw new RelayError(502, 'openai_invalid_json');
  }
  const usage = result.usage;
  return {
    lines,
    inputTokens: usage?.input_tokens,
    cachedTokens: usage?.input_tokens_details?.cached_tokens,
    outputTokens: usage?.output_tokens,
  };
}

function send(res, status, body) {
  if (!res.destroyed)
    res.writeHead(status, { 'content-type': 'application/json', 'cache-control': 'no-store' }).end(JSON.stringify(body));
}

export function createRelay(generate = generateWithOpenAi) {
  const seen = new Map();
  let generating = false;

  return createServer({ requestTimeout: 10000 }, async (req, res) => {
    if (req.method !== 'POST' || req.url !== '/v1/order-dialogue' || req.headers.host !== `${host}:${port}`)
      return send(res, 404, { reason: 'route_not_found' });
    if (req.headers['content-type']?.split(';')[0].trim() !== 'application/json')
      return send(res, 415, { reason: 'unsupported_content_type' });

    const started = performance.now();
    let order;
    let ownsRequest = false;
    try {
      order = await readOrder(req);
      for (const [id, expires] of seen) {
        if (expires > started) break;
        seen.delete(id);
      }
      if (seen.has(order.requestId))
        throw new RelayError(409, 'duplicate_request');
      if (generating || seen.size >= 512)
        throw new RelayError(429, 'relay_busy');
      seen.set(order.requestId, performance.now() + 300_000);
      generating = ownsRequest = true;

      const disconnected = new AbortController();
      res.once('close', () => disconnected.abort());
      const signal = AbortSignal.any([disconnected.signal, AbortSignal.timeout(9000)]);
      const { lines, inputTokens, cachedTokens, outputTokens } = await generate(order, signal);
      if (!hasOnlyKeys(lines, keys) || Object.values(lines).some(line => !text(line, 1200) || !line.trim()))
        throw new RelayError(502, 'openai_invalid_lines');
      console.info(JSON.stringify({ requestId: order.requestId, dataRevision: order.dataRevision,
        model, promptRevision, elapsedMs: Math.round(performance.now() - started), inputTokens, cachedTokens, outputTokens }));
      send(res, 200, { requestId: order.requestId, schemaVersion, lines });
    } catch (error) {
      const status = error instanceof RelayError ? error.status : error.name === 'TimeoutError' ? 504 : 502;
      const reason = error instanceof RelayError ? error.message : error.name === 'TimeoutError' ? 'openai_timeout' : 'openai_transport';
      console.warn(JSON.stringify({ requestId: order?.requestId, model, promptRevision,
        elapsedMs: Math.round(performance.now() - started), reason }));
      send(res, status, { reason });
    } finally {
      if (ownsRequest)
        generating = false;
    }
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  if (!process.env.OPENAI_API_KEY?.trim() && process.stdin.isTTY) {
    const hidden = new Writable({ write(_chunk, _encoding, done) { done(); } });
    const terminal = createInterface({ input: process.stdin, output: hidden, terminal: true });
    terminal.once('SIGINT', () => process.exit(130));
    process.stdout.write('Paste the OpenAI API key (input hidden): ');
    try {
      process.env.OPENAI_API_KEY = (await terminal.question('')).trim();
    } finally {
      terminal.close();
      process.stdout.write('\n');
    }
  }

  if (!/^sk-[A-Za-z0-9_-]{20,}$/.test(process.env.OPENAI_API_KEY?.trim() ?? '')) {
    console.error('Enter a valid OpenAI API key or set OPENAI_API_KEY.');
    process.exitCode = 1;
  } else {
    createRelay().listen(port, host, () => console.info(`Order dialogue relay listening on ${host}:${port}`));
  }
}
