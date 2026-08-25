/*
 * The Windows app is driven by files generated from the browser extension's
 * layout tables. These tests fail if someone edits src/core/layouts.js (or the
 * converter) without regenerating them, which would let the two platforms
 * silently disagree about what a key produces.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const require = createRequire(import.meta.url);

/** Runs a generator in --check mode. Returns null when it is up to date. */
function runGenerator(script) {
  try {
    execFileSync(process.execPath, [path.join(root, 'scripts', script), '--check'],
      { cwd: root, stdio: 'pipe' });
    return null;
  } catch (error) {
    return (error.stderr?.toString() || error.stdout?.toString() || String(error)).trim();
  }
}

test('Layouts.g.cs is up to date with src/core/layouts.js', () => {
  assert.equal(runGenerator('generate-cs-layouts.mjs'), null);
});

test('the C# layout table records the digest of the JavaScript it came from', () => {
  const generated = fs.readFileSync(
    path.join(root, 'windows/src/KeyboardLanguageFix.Core/Layouts.g.cs'), 'utf8');
  const expected = crypto.createHash('sha256')
    .update(fs.readFileSync(path.join(root, 'src/core/layouts.js')))
    .digest('hex').slice(0, 16);

  assert.match(generated, new RegExp(`SourceDigest = "${expected}"`));
});

test('parity-fixture.json matches what the JavaScript engine produces today', () => {
  assert.equal(runGenerator('generate-parity-fixture.mjs'), null);
});

test('the parity fixture covers every key of every layout', async () => {
  const { buildCases } = await import('../scripts/generate-parity-fixture.mjs');
  const layouts = require(path.join(root, 'src/core/layouts.js'));

  const covered = new Set(buildCases().map((c) => `${c.primaryLayout} ${c.input}`));

  for (const layout of layouts.LAYOUTS) {
    for (const table of [layout.base, layout.shift]) {
      for (const [key, value] of Object.entries(table)) {
        assert.ok(covered.has(`${layout.id} ${key}`), `${layout.id}: key "${key}" not covered`);
        assert.ok(covered.has(`${layout.id} ${value}`), `${layout.id}: output "${value}" not covered`);
      }
    }
  }
});
