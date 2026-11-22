// Checks dialogue entries against the same rules the game applies to generated lines
// (Assets/Scripts/Core/OpenAiDialogueProvider.cs and FallbackLine in DialogueCatalog.cs).
// Usage: node Tools/DialogueTable/validate.mjs <table.json> [more tables...]
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

export const reactionKeys = [
  'rcv_ok', 'rcv_bottle', 'rcv_ink', 'rcv_both',
  'aft_cured', 'aft_cured_ink', 'aft_odd', 'aft_slime',
];
export const keys = ['symptom', ...reactionKeys];
export const symptomTargetLength = 200;

const symptomTag = /<sym>([^<>]+)<\/sym>/g;
const answerId = /(?<![A-Za-z0-9])(?:DIS_\d{2}|POT_\d{2}|[A-F][1-3])(?![A-Za-z0-9])/i;
const squash = text => text.normalize().replace(/\s+/g, ' ');
// Korean text is counted the way the game counts it: by user-perceived characters.
export const length = text => [...new Intl.Segmenter('ko', { granularity: 'grapheme' }).segment(text.trim())].length;

/** Returns { errors, warnings }. An entry with errors must not go into the game. */
export function validateEntry(entry, disease) {
  const errors = [];
  const warnings = [];
  if (!entry || typeof entry !== 'object')
    return { errors: ['entry is not an object'], warnings };
  const extra = Object.keys(entry).filter(key => !keys.includes(key));
  if (extra.length) errors.push(`unexpected keys: ${extra.join(', ')}`);

  for (const key of keys) {
    const line = entry[key];
    if (typeof line !== 'string' || !line.trim()) { errors.push(`${key}: missing`); continue; }
    if (/[\u0000-\u001f\u007f]/.test(line)) errors.push(`${key}: contains a line break or control character`);
  }
  if (errors.length) return { errors, warnings };

  // Symptom line: exactly three <sym> spans, nothing else in angle brackets, no banned words anywhere.
  const spans = [...entry.symptom.matchAll(symptomTag)];
  const visible = entry.symptom.replace(symptomTag, '$1');
  if (spans.length !== 3) errors.push(`symptom: ${spans.length} symptom spans, expected 3`);
  if (/[<>]/.test(visible)) errors.push('symptom: stray or nested tag');
  const flat = squash(visible);
  for (const word of [disease.name, ...disease.bannedWords])
    if (word && flat.includes(squash(word.trim()))) errors.push(`symptom: contains banned word "${word}"`);
  if (answerId.test(visible)) errors.push('symptom: contains an answer id');
  range('symptom', visible, symptomTargetLength * 0.8, symptomTargetLength * 1.2, 300);

  for (const key of reactionKeys) {
    const line = entry[key];
    if (/[<>]/.test(line)) errors.push(`${key}: contains a tag`);
    if (answerId.test(line) || squash(line).includes(squash(disease.name)))
      errors.push(`${key}: names the disease or an answer id`);
    const receive = key.startsWith('rcv_');
    range(key, line, receive ? 30 : 40, receive ? 60 : 80, 120);
  }
  return { errors, warnings };

  function range(key, text, min, max, limit) {
    const count = length(text);
    if (count > limit) errors.push(`${key}: ${count} characters, hard limit ${limit}`);
    else if (count < min || count > max) warnings.push(`${key}: ${count} characters, target ${min}–${max}`);
  }
}

export function validateTable(table, inputs) {
  const diseases = new Map(inputs.diseases.map(disease => [disease.id, disease]));
  const report = [];
  for (const [id, entry] of Object.entries(table.entries ?? {})) {
    const disease = diseases.get(id.split('|')[1]);
    const result = disease ? validateEntry(entry, disease) : { errors: [`unknown disease in "${id}"`], warnings: [] };
    report.push({ id, ...result });
  }
  return report;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const inputs = JSON.parse(readFileSync(new URL('./inputs.json', import.meta.url), 'utf8'));
  let failed = false;
  for (const file of process.argv.slice(2)) {
    const report = validateTable(JSON.parse(readFileSync(file, 'utf8')), inputs);
    const bad = report.filter(row => row.errors.length);
    const warned = report.filter(row => row.warnings.length);
    console.log(`${file}: ${report.length} entries, ${bad.length} with errors, ${warned.length} with warnings`);
    for (const row of report) {
      for (const error of row.errors) console.log(`  ERROR ${row.id} ${error}`);
      for (const warning of row.warnings) console.log(`  warn  ${row.id} ${warning}`);
    }
    failed ||= bad.length > 0;
  }
  process.exitCode = failed ? 1 : 0;
}
