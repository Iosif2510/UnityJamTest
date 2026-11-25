// Writes a side-by-side Markdown comparison of two or more dialogue tables for human review.
// Usage: node Tools/DialogueTable/compare.mjs <out.md> <label>=<table.json> <label>=<table.json> ...
import { readFileSync, writeFileSync } from 'node:fs';
import { keys, length, validateEntry } from './validate.mjs';

const [outPath, ...sources] = process.argv.slice(2);
const inputs = JSON.parse(readFileSync(new URL('./inputs.json', import.meta.url), 'utf8'));
const characters = new Map(inputs.characters.map(character => [character.code, character]));
const diseases = new Map(inputs.diseases.map(disease => [disease.id, disease]));
const tables = sources.map(source => {
  const [label, file] = source.split('=');
  return { label, table: JSON.parse(readFileSync(file, 'utf8')) };
});

const titles = {
  symptom: '상담 (증상 3개)',
  rcv_ok: '받을 때 · 병과 잉크 정답',
  rcv_bottle: '받을 때 · 병만 오답',
  rcv_ink: '받을 때 · 잉크만 오답',
  rcv_both: '받을 때 · 둘 다 오답',
  aft_cured: '마신 뒤 · 완치',
  aft_cured_ink: '마신 뒤 · 완치, 잉크 오답',
  aft_odd: '마신 뒤 · 재료 1개 오답 (몸 색 변화)',
  aft_slime: '마신 뒤 · 재료 2개 이상 오답 (슬라임)',
};
// The game shows symptom spans with an effect; bold stands in for that here.
const show = text => text.replace(/<sym>/g, '**').replace(/<\/sym>/g, '**').replace(/\|/g, '\\|');

const ids = [...new Set(tables.flatMap(({ table }) => Object.keys(table.entries)))];
const out = ['# 손님 대사 샘플 비교', '',
  tables.map(({ label, table }) => `- **${label}**: ${table.generator?.model ?? '?'}`).join('\n'), '',
  '굵은 글씨는 게임에서 증상 효과가 붙는 구간이다. 괄호 안 숫자는 글자 수다.', ''];

for (const id of ids) {
  const [code, diseaseId] = id.split('|');
  const character = characters.get(code);
  const disease = diseases.get(diseaseId);
  out.push(`## ${character.name} (${code}, ${character.job}) × ${disease.name}`, '',
    `- 말투: ${character.speechStyle}`, `- 성격: ${character.personality}`,
    `- 잡담 소재: ${character.chatTopics.join(', ')}`,
    `- 증상의 뜻: ${disease.symptomMeanings.join(' / ')}`, `- 금지어: ${disease.bannedWords.join(', ')}`, '');
  for (const { label, table } of tables) {
    const entry = table.entries[id];
    if (!entry) continue;
    const { errors, warnings } = validateEntry(entry, disease);
    out.push(`- ${label} 검증: ${errors.length ? '오류 ' + errors.join('; ') : '통과'}` +
      (warnings.length ? ` (참고: ${warnings.join('; ')})` : ''));
  }
  out.push('', `| 줄 | ${tables.map(({ label }) => label).join(' | ')} |`, `| --- | ${tables.map(() => '---').join(' | ')} |`);
  for (const key of keys) {
    const cells = tables.map(({ table }) => {
      const line = table.entries[id]?.[key];
      return line ? `${show(line)} (${length(line.replace(/<\/?sym>/g, ''))})` : '없음';
    });
    out.push(`| ${titles[key]} | ${cells.join(' | ')} |`);
  }
  out.push('');
}
writeFileSync(outPath, out.join('\n'));
console.log(`${ids.length} orders compared -> ${outPath}`);
