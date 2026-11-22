// Pre-generates customer dialogue with an OpenAI model and writes a table the game can ship.
// The prompt and the request shape are the ones the runtime relay used (Tools/OrderDialogueRelay),
// so a table entry is exactly what the game used to receive for one order.
//
// Usage (from the repository root; OPENAI_API_KEY must be set, it is never printed or stored):
//   node Tools/DialogueTable/generate.mjs --model <id> --out <file.json> --pairs HUM_M3:DIS_01,BST_M1:DIS_02
//   node Tools/DialogueTable/generate.mjs --model <id> --out <file.json> --all
// Re-running with the same --out keeps valid entries and only fills the missing or invalid ones.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { keys, symptomTargetLength, validateEntry } from './validate.mjs';

const args = process.argv.slice(2);
const option = name => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : undefined; };
const model = option('--model');
const outPath = option('--out');
const attempts = Number(option('--attempts') ?? 3);
const parallel = Number(option('--parallel') ?? 4);
if (!model || !outPath) { console.error('--model and --out are required'); process.exit(2); }
const key = process.env.OPENAI_API_KEY?.trim();
if (!key) { console.error('OPENAI_API_KEY is not set'); process.exit(2); }

const inputs = JSON.parse(readFileSync(new URL('./inputs.json', import.meta.url), 'utf8'));
const prompt = readFileSync(new URL('../OrderDialogueRelay/order-dialogue.ko.txt', import.meta.url), 'utf8').trim();
const format = {
  type: 'json_schema',
  name: 'order_dialogue',
  strict: true,
  schema: {
    type: 'object',
    properties: Object.fromEntries(keys.map(name => [name, { type: 'string' }])),
    required: keys,
    additionalProperties: false,
  },
};
const promptRevision = createHash('sha256').update(prompt).update(JSON.stringify(format)).digest('hex').slice(0, 16);

const characters = new Map(inputs.characters.map(character => [character.code, character]));
const diseases = new Map(inputs.diseases.map(disease => [disease.id, disease]));
const pairs = args.includes('--all')
  ? inputs.characters.flatMap(character => inputs.diseases.map(disease => [character.code, disease.id]))
  : (option('--pairs') ?? '').split(',').filter(Boolean).map(pair => pair.split(':'));
if (!pairs.length) { console.error('give --pairs CODE:DIS_xx,... or --all'); process.exit(2); }

const table = existsSync(outPath) ? JSON.parse(readFileSync(outPath, 'utf8'))
  : { schemaVersion: 1, generator: { provider: 'openai', model, promptRevision }, entries: {} };
const usage = { inputTokens: 0, outputTokens: 0, calls: 0 };

/** The same user message the runtime relay sent: one customer, one disease, the fallback lines as examples. */
export function orderFor(character, disease) {
  const { exampleSymptom, ...diseaseInput } = disease;
  return {
    character,
    disease: diseaseInput,
    examples: { symptom: exampleSymptom, reactions: inputs.exampleReactions },
    symptomTargetLength,
  };
}

async function generate(character, disease) {
  const response = await fetch('https://api.openai.com/v1/responses', {
    method: 'POST',
    headers: { authorization: `Bearer ${key}`, 'content-type': 'application/json' },
    body: JSON.stringify({
      model,
      store: false,
      max_output_tokens: 6000,
      input: [
        { role: 'developer', content: prompt },
        { role: 'user', content: JSON.stringify(orderFor(character, disease)) },
      ],
      text: { format },
    }),
  });
  if (!response.ok) throw new Error(`HTTP ${response.status} ${(await response.text()).slice(0, 200)}`);
  const result = await response.json();
  usage.calls++;
  usage.inputTokens += result.usage?.input_tokens ?? 0;
  usage.outputTokens += result.usage?.output_tokens ?? 0;
  const text = result.output?.filter(item => item.type === 'message').flatMap(item => item.content)
    .find(item => item.type === 'output_text')?.text;
  if (result.status !== 'completed' || !text) throw new Error(`incomplete response (${result.status})`);
  const lines = JSON.parse(text);
  for (const name of keys) lines[name] = String(lines[name] ?? '').trim().normalize();
  return lines;
}

async function fill([code, diseaseId]) {
  const id = `${code}|${diseaseId}`;
  const character = characters.get(code);
  const disease = diseases.get(diseaseId);
  if (!character || !disease) return `${id}: unknown character or disease`;
  if (table.entries[id] && !validateEntry(table.entries[id], disease).errors.length) return `${id}: kept`;

  let last = '';
  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      const lines = await generate(character, disease);
      const { errors } = validateEntry(lines, disease);
      if (!errors.length) { table.entries[id] = lines; return `${id}: ok (attempt ${attempt})`; }
      last = errors.join('; ');
    } catch (error) { last = error.message; }
  }
  delete table.entries[id];
  return `${id}: FAILED after ${attempts} attempts: ${last}`;
}

const started = Date.now();
for (let i = 0; i < pairs.length; i += parallel) {
  console.log((await Promise.all(pairs.slice(i, i + parallel).map(fill))).join('\n'));
  writeFileSync(outPath, JSON.stringify(table, null, 2) + '\n');
}
console.log(`done: ${Object.keys(table.entries).length} entries in ${outPath}; ${usage.calls} calls, ` +
  `${usage.inputTokens} input + ${usage.outputTokens} output tokens, ${Math.round((Date.now() - started) / 1000)}s`);
