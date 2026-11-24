// Translates the Korean customer dialogue table into another language with an OpenAI model, entry by entry,
// so every language tells the same small stories and the same three symptoms (the memos are translations too).
//
// Usage (from the repository root; OPENAI_API_KEY must be set, it is never printed or stored):
//   node Tools/DialogueTable/translate.mjs --model <id> --lang ja --out Assets/Resources/Dialogue/DialogueTable.ja.json --all
//   node Tools/DialogueTable/translate.mjs --model <id> --lang zh-Hans --out <file> --pairs HUM_M1|DIS_04,BST_F1|DIS_02
// Re-running with the same --out keeps valid entries and only fills the missing or invalid ones.
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { keys } from './validate.mjs';

const args = process.argv.slice(2);
const option = name => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : undefined; };
const model = option('--model');
const lang = option('--lang');
const outPath = option('--out');
const source = option('--source') ?? 'Assets/GameData/DialogueTable.json';
const attempts = Number(option('--attempts') ?? 3);
const parallel = Number(option('--parallel') ?? 4);
const names = { ja: 'Japanese', 'zh-Hans': 'Simplified Chinese', 'zh-Hant': 'Traditional Chinese', en: 'English' };
if (!model || !outPath || !names[lang]) { console.error('--model, --out and --lang (ja, zh-Hans, zh-Hant, en) are required'); process.exit(2); }
const key = process.env.OPENAI_API_KEY?.trim();
if (!key) { console.error('OPENAI_API_KEY is not set'); process.exit(2); }

const korean = JSON.parse(readFileSync(source, 'utf8'));

const prompt = `You translate the lines of one customer in the cozy fantasy game "The Noisy Potion Workshop" from Korean into ${names[lang]}.
The customer has come to a potion shop. "symptom" is what they tell the shopkeeper; rcv_* are what they say on being handed the potion (ok: bottle and label right; bottle: odd bottle; ink: unreadable label; both: both wrong); aft_* are what they say after drinking (cured; cured_ink: cured but the label was unreadable; odd: a strange side effect; slime: they have turned into a slime).

Rules:
- Translate the meaning, tone and personality faithfully, as natural, idiomatic ${names[lang]} a native speaker would say out loud. Keep the speaker's politeness level and verbal tics. Do not add or drop events or details.
- In "symptom", the Korean marks exactly three symptom phrases with <sym> and </sym>. Keep exactly three <sym>...</sym> pairs, around the phrases that carry the same three symptoms, never nested, with no other angle brackets anywhere.
- Lines are spoken on screen: no line breaks, stage directions, speaker labels, quotation marks around the whole line, markdown or emoji. No Korean letters anywhere.
- Keep each line about as long as the Korean one.${lang === 'ja' ? '\n- Use ordinary Japanese with kanji and kana as a person would speak.' : ''}${lang === 'zh-Hant' ? '\n- Use Traditional Chinese characters and wording natural in Taiwan.' : ''}${lang === 'zh-Hans' ? '\n- Use Simplified Chinese characters.' : ''}`;

const format = {
  type: 'json_schema', name: 'customer_lines', strict: true,
  schema: { type: 'object', properties: Object.fromEntries(keys.map(k => [k, { type: 'string' }])), required: keys, additionalProperties: false },
};

const hangul = /[가-힣ᄀ-ᇿ㄰-㆏]/;
function problems(entry) {
  const errors = [];
  for (const k of keys) {
    const line = entry?.[k];
    if (typeof line !== 'string' || !line.trim()) { errors.push(`${k}: missing`); continue; }
    if (/[\u0000-\u001f\u007f]/.test(line)) errors.push(`${k}: line break`);
    if (hangul.test(line)) errors.push(`${k}: Korean left`);
    if (k !== 'symptom' && /[<>]/.test(line)) errors.push(`${k}: angle brackets`);
  }
  if (typeof entry?.symptom === 'string') {
    const spans = [...entry.symptom.matchAll(/<sym>([^<>]+)<\/sym>/g)];
    if (spans.length !== 3) errors.push(`symptom: ${spans.length} spans`);
    if (/[<>]/.test(entry.symptom.replace(/<sym>([^<>]+)<\/sym>/g, '$1'))) errors.push('symptom: stray tag');
  }
  return errors;
}

const table = existsSync(outPath) ? JSON.parse(readFileSync(outPath, 'utf8'))
  : { schemaVersion: 1, generator: { provider: 'openai', model, method: 'translated from the Korean table', lang }, entries: {} };
const wanted = args.includes('--all') ? Object.keys(korean.entries)
  : (option('--pairs') ?? '').split(',').filter(Boolean);
const todo = wanted.filter(id => korean.entries[id] && problems(table.entries[id]).length > 0);
const usage = { input: 0, output: 0, calls: 0 };
console.log(`${lang}: ${todo.length} entries to translate (${wanted.length - todo.length} already done)`);

async function translate(id) {
  const ko = korean.entries[id];
  for (let attempt = 1; attempt <= attempts; attempt++) {
    const response = await fetch('https://api.openai.com/v1/responses', {
      method: 'POST',
      headers: { authorization: `Bearer ${key}`, 'content-type': 'application/json' },
      body: JSON.stringify({
        model, store: false, max_output_tokens: 6000,
        input: [
          { role: 'developer', content: prompt },
          { role: 'user', content: JSON.stringify(ko) },
        ],
        text: { format },
      }),
    });
    usage.calls++;
    if (!response.ok) { console.warn(`${id}: HTTP ${response.status} (attempt ${attempt})`); continue; }
    const body = await response.json();
    usage.input += body.usage?.input_tokens ?? 0;
    usage.output += body.usage?.output_tokens ?? 0;
    const text = body.output?.flatMap(o => o.content ?? []).find(c => c.type === 'output_text')?.text;
    let entry;
    try { entry = JSON.parse(text); } catch { console.warn(`${id}: not JSON (attempt ${attempt})`); continue; }
    for (const k of keys) if (typeof entry[k] === 'string') entry[k] = entry[k].trim();
    const errors = problems(entry);
    if (errors.length === 0) { table.entries[id] = entry; return true; }
    console.warn(`${id}: ${errors.join('; ')} (attempt ${attempt})`);
  }
  return false;
}

let done = 0, failed = 0;
const queue = [...todo];
await Promise.all(Array.from({ length: parallel }, async () => {
  while (queue.length) {
    const id = queue.shift();
    if (await translate(id)) done++; else failed++;
    writeFileSync(outPath, JSON.stringify(table, null, 2) + '\n');
  }
}));
writeFileSync(outPath, JSON.stringify(table, null, 2) + '\n');
console.log(`${lang}: translated ${done}, failed ${failed}, calls ${usage.calls}, tokens in ${usage.input} out ${usage.output}`);
