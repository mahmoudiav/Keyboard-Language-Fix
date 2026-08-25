/* Cheap structural checks: every file a manifest names must actually exist,
   and both manifests must stay in step with each other. */
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const read = (p) => JSON.parse(fs.readFileSync(path.join(root, p), 'utf8'));

const chrome = read('manifest.json');
const firefox = read('manifest.firefox.json');

function referencedFiles(manifest) {
  const files = new Set(Object.values(manifest.icons || {}));
  for (const icon of Object.values(manifest.action?.default_icon || {})) files.add(icon);
  if (manifest.action?.default_popup) files.add(manifest.action.default_popup);
  if (manifest.options_ui?.page) files.add(manifest.options_ui.page);
  if (manifest.background?.service_worker) files.add(manifest.background.service_worker);
  for (const script of manifest.background?.scripts || []) files.add(script);
  for (const cs of manifest.content_scripts || []) {
    for (const js of cs.js || []) files.add(js);
    for (const css of cs.css || []) files.add(css);
  }
  return [...files];
}

for (const [name, manifest] of [['chrome', chrome], ['firefox', firefox]]) {
  test(`${name} manifest references only files that exist`, () => {
    for (const file of referencedFiles(manifest)) {
      assert.ok(fs.existsSync(path.join(root, file)), `missing: ${file}`);
    }
  });
}

test('both manifests ship the same version and content scripts', () => {
  assert.equal(chrome.version, firefox.version);
  assert.deepEqual(chrome.content_scripts, firefox.content_scripts);
  assert.deepEqual(chrome.commands, firefox.commands);
});

test('package.json version matches the manifest', () => {
  assert.equal(read('package.json').version, chrome.version);
});

test('every __MSG_*__ placeholder has an entry in each locale', () => {
  const placeholders = new Set();
  const scan = (value) => {
    if (typeof value === 'string') {
      const match = value.match(/^__MSG_(.+)__$/);
      if (match) placeholders.add(match[1]);
    } else if (value && typeof value === 'object') {
      Object.values(value).forEach(scan);
    }
  };
  scan(chrome);
  assert.ok(placeholders.size > 0);

  for (const locale of fs.readdirSync(path.join(root, '_locales'))) {
    const messages = read(path.join('_locales', locale, 'messages.json'));
    for (const key of placeholders) {
      assert.ok(messages[key], `${locale} is missing "${key}"`);
    }
  }
});

test('every locale defines the same keys as the default locale', () => {
  const base = read(path.join('_locales', chrome.default_locale, 'messages.json'));
  for (const locale of fs.readdirSync(path.join(root, '_locales'))) {
    const messages = read(path.join('_locales', locale, 'messages.json'));
    assert.deepEqual(
      Object.keys(messages).sort(),
      Object.keys(base).sort(),
      `${locale} does not match ${chrome.default_locale}`
    );
  }
});

test('every data-i18n key used in the UI is translated', () => {
  const base = read(path.join('_locales', chrome.default_locale, 'messages.json'));
  const uiDir = path.join(root, 'src', 'ui');
  for (const file of fs.readdirSync(uiDir).filter((f) => f.endsWith('.html'))) {
    const html = fs.readFileSync(path.join(uiDir, file), 'utf8');
    for (const match of html.matchAll(/data-i18n(?:-placeholder)?="([^"]+)"/g)) {
      assert.ok(base[match[1]], `${file} uses untranslated key "${match[1]}"`);
    }
  }
});
