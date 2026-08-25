/*
 * Builds a fixture of (input, options) -> output from the JavaScript engine.
 *
 * Both engines assert against this file: the C# tests replay it, and the
 * JavaScript tests regenerate it and compare. Neither can drift without one of
 * the two suites going red.
 *
 *   node scripts/generate-parity-fixture.mjs           # write the file
 *   node scripts/generate-parity-fixture.mjs --check   # fail if it is stale
 */
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const require = createRequire(import.meta.url);
const layouts = require(path.join(root, 'src', 'core', 'layouts.js'));
const { convert } = require(path.join(root, 'src', 'core', 'converter.js'));

export const FIXTURE_PATH = path.join(
  root, 'windows', 'tests', 'KeyboardLanguageFix.Core.Tests', 'parity-fixture.json');

const PHRASES = [
  'hgsghl ugd;l', 'lvpfh fpl', ';dt phg;', 'hello world', 'ghbdtn',
  'the quick brown fox jumps over the lazy dog', 'Mixed CASE and 123 numbers!',
  'trailing spaces   ', '   leading spaces', 'punctuation: ,./;\'[]\\-=`',
  'shifted punctuation: <>?:"{}|_+~', 'اثممخ صخقمي', 'привет мир',
  'a', 'A', '', '   ', '123456789', 'user@example.com', 'https://example.com/path?q=1',
  // Capitals the user did not type: sentence auto-capitalisation and Caps Lock.
  'lpl,]', 'Lpl,]', 'LPL,]', 'Hpl]', 'HPL]', 'Ok', 'OK', 'CASE', 'McDonald',
  // Deliberate shifted punctuation, which must survive all of the above.
  'hgslhxK rvdfh', 'K', 'P', 'L', 'I', 'K rvdfh', 'A4', 'L]'
];

/** Every case the fixture covers, in a stable order. */
export function buildCases() {
  const cases = [];

  const add = (input, options) => {
    const result = convert(input, options);
    cases.push({
      input,
      primaryLayout: options.primaryLayout,
      enabledLayouts: options.enabledLayouts,
      mode: options.mode || 'auto',
      expected: result.text,
      changed: result.changed,
      direction: result.changed ? result.direction : null,
      layoutId: result.layoutId
    });
  };

  for (const layout of layouts.LAYOUTS) {
    const enabled = [layout.id];

    // Every key in both layers, in both directions.
    for (const table of [layout.base, layout.shift]) {
      for (const [key, value] of Object.entries(table)) {
        add(key, { primaryLayout: layout.id, enabledLayouts: enabled, mode: 'toLayout' });
        add(value, { primaryLayout: layout.id, enabledLayouts: enabled, mode: 'toLatin' });
      }
    }

    // Realistic text through all three modes.
    for (const phrase of PHRASES) {
      for (const mode of ['auto', 'toLayout', 'toLatin']) {
        add(phrase, { primaryLayout: layout.id, enabledLayouts: enabled, mode });
      }
    }
  }

  // Multi-layout detection: everything enabled at once.
  const all = layouts.LAYOUTS.map((l) => l.id);
  for (const phrase of PHRASES) {
    for (const primary of all) {
      add(phrase, { primaryLayout: primary, enabledLayouts: all, mode: 'auto' });
    }
  }

  return cases;
}

export function render() {
  return JSON.stringify(
    { generatedFrom: 'src/core/converter.js', cases: buildCases() }, null, 2) + '\n';
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const content = render();
  const check = process.argv.includes('--check');
  const current = fs.existsSync(FIXTURE_PATH) ? fs.readFileSync(FIXTURE_PATH, 'utf8') : null;

  if (check) {
    if (current !== content) {
      console.error('parity-fixture.json is stale — run: node scripts/generate-parity-fixture.mjs');
      process.exit(1);
    }
    console.log('parity-fixture.json is up to date');
  } else {
    fs.mkdirSync(path.dirname(FIXTURE_PATH), { recursive: true });
    fs.writeFileSync(FIXTURE_PATH, content);
    console.log(`wrote ${path.relative(root, FIXTURE_PATH)} (${JSON.parse(content).cases.length} cases)`);
  }
}
