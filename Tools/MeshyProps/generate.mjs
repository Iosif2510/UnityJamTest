// Generates small props with Meshy text-to-3D in the style the existing workshop props were made in:
// a low-poly mesh (preview), then a textured version (refine), downloaded as FBX with its texture maps.
//
// Usage (MESHY_API_KEY must be set; it is never printed or stored):
//   node Tools/MeshyProps/generate.mjs <outDir> <name> [more names...]
// Names come from props.json next to this file. Each prop lands in <outDir>/<name>/.
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const key = process.env.MESHY_API_KEY?.trim();
if (!key) { console.error('MESHY_API_KEY is not set'); process.exit(2); }
const [outDir, ...names] = process.argv.slice(2);
if (!outDir || !names.length) { console.error('usage: generate.mjs <outDir> <name>...'); process.exit(2); }

const { style, polycount, props } = JSON.parse(readFileSync(new URL('./props.json', import.meta.url), 'utf8'));
const api = 'https://api.meshy.ai/openapi';
const headers = { authorization: `Bearer ${key}`, 'content-type': 'application/json' };

async function call(method, path, body) {
  const response = await fetch(api + path, { method, headers, body: body ? JSON.stringify(body) : undefined });
  const text = await response.text();
  if (!response.ok) throw new Error(`${method} ${path}: HTTP ${response.status} ${text.slice(0, 300)}`);
  return JSON.parse(text);
}

const balance = async () => (await call('GET', '/v1/balance')).balance;

async function finish(id) {
  for (;;) {
    await new Promise(resolve => setTimeout(resolve, 6000));
    const task = await call('GET', `/v2/text-to-3d/${id}`);
    if (task.status === 'SUCCEEDED') return task;
    if (task.status === 'FAILED' || task.status === 'CANCELED')
      throw new Error(`task ${id} ${task.status}: ${task.task_error?.message ?? ''}`);
  }
}

async function download(url, file) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`download ${file}: HTTP ${response.status}`);
  writeFileSync(file, Buffer.from(await response.arrayBuffer()));
}

async function make(name) {
  const prop = props[name];
  if (!prop) return `${name}: not in props.json`;
  const dir = join(outDir, name);
  mkdirSync(dir, { recursive: true });
  const started = Date.now();
  const before = await balance();

  // Smart Topology gives a game-ready low-poly mesh directly, like the hand-reduced props already in the project.
  const preview = (await call('POST', '/v2/text-to-3d', {
    mode: 'preview',
    prompt: `${prop.prompt} ${style}`,
    model_type: 'smart-topology',
    ai_model: 'meshy-t2.5',
    target_polycount: prop.polycount ?? polycount,
    target_formats: ['fbx'],
  })).result;
  await finish(preview);

  const refine = (await call('POST', '/v2/text-to-3d', {
    mode: 'refine',
    preview_task_id: preview,
    enable_pbr: true,
    texture_prompt: `${prop.texture ?? prop.prompt} ${style}`,
    target_formats: ['fbx'],
    auto_size: true,
    origin_at: 'bottom',
  })).result;
  const task = await finish(refine);

  await download(task.model_urls.fbx, join(dir, `${name}.fbx`));
  const maps = task.texture_urls?.[0] ?? {};
  for (const [map, url] of Object.entries(maps))
    if (url) await download(url, join(dir, `${name}_${map}.png`));
  if (task.thumbnail_url) await download(task.thumbnail_url, join(dir, 'thumbnail.png'));
  writeFileSync(join(dir, 'task.json'), JSON.stringify({ preview, refine, prompt: prop.prompt }, null, 2));

  const spent = before - await balance();
  return `${name}: ok, maps [${Object.keys(maps).join(', ')}], ${spent} credits, ${Math.round((Date.now() - started) / 1000)}s`;
}

// One at a time: the balance difference then tells what each prop cost.
for (const name of names) {
  try { console.log(await make(name)); }
  catch (error) { console.log(`${name}: FAILED ${error.message}`); process.exitCode = 1; }
}
console.log(`balance now ${await balance()}`);
