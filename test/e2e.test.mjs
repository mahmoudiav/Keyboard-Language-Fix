/*
 * End-to-end tests: load the unpacked extension into a real Chromium and drive
 * the shortcut against test/manual.html.
 *
 * Playwright is optional. When it is not installed these tests skip rather
 * than fail, so `npm test` still works on a bare checkout:
 *
 *   npm install -D playwright && npx playwright install chromium
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const PAGE = 'file://' + path.join(root, 'test', 'manual.html');
const SHORTCUT = 'Control+Shift+Space';

async function loadPlaywright() {
  for (const specifier of ['playwright', 'playwright-core']) {
    try {
      const mod = await import(specifier);
      return mod.chromium || mod.default?.chromium || null;
    } catch { /* try the next one */ }
  }
  return null;
}

const chromium = await loadPlaywright();
const skip = chromium ? false : 'playwright is not installed';

let ctx = null;
let userDataDir = null;

async function browser() {
  if (ctx) return ctx;
  userDataDir = fs.mkdtempSync(path.join(os.tmpdir(), 'klf-e2e-'));
  ctx = await chromium.launchPersistentContext(userDataDir, {
    channel: 'chromium',
    args: [`--disable-extensions-except=${root}`, `--load-extension=${root}`]
  });
  // The extension is ready once its service worker is up.
  if (!ctx.serviceWorkers().length) {
    await ctx.waitForEvent('serviceworker', { timeout: 30000 });
  }
  return ctx;
}

/** Open the fixture page and wait for the content script to settle. */
async function openPage() {
  const page = await (await browser()).newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(String(e.message)));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  await page.goto(PAGE);
  await page.waitForTimeout(900);
  page.klfErrors = errors;
  return page;
}

async function selectAllAndConvert(page, selector) {
  await page.click(selector);
  await page.keyboard.press('Control+A');
  await page.keyboard.press(SHORTCUT);
  await page.waitForTimeout(400);
}

test.after(async () => {
  if (ctx) await ctx.close();
  if (userDataDir) fs.rmSync(userDataDir, { recursive: true, force: true });
});

test('converts a selection inside a text input', { skip }, async () => {
  const page = await openPage();
  await selectAllAndConvert(page, 'section:nth-of-type(1) input');
  assert.equal(await page.inputValue('section:nth-of-type(1) input'), 'السلام عليكم');
  assert.deepEqual(page.klfErrors, []);
  await page.close();
});

test('with no selection, converts only the word before the caret', { skip }, async () => {
  const page = await openPage();
  const selector = 'section:nth-of-type(3) input';
  await page.click(selector);
  await page.keyboard.press('End');
  await page.keyboard.press(SHORTCUT);
  await page.waitForTimeout(400);
  assert.equal(await page.inputValue(selector), 'hello مرحبا');
  await page.close();
});

test('Ctrl+Z undoes the conversion', { skip }, async () => {
  const page = await openPage();
  const selector = 'section:nth-of-type(3) input';
  await page.click(selector);
  await page.keyboard.press('End');
  await page.keyboard.press(SHORTCUT);
  await page.waitForTimeout(400);
  await page.keyboard.press('Control+Z');
  await page.waitForTimeout(250);
  assert.equal(await page.inputValue(selector), 'hello lvpfh');
  await page.close();
});

test('converts inside a contenteditable', { skip }, async () => {
  const page = await openPage();
  await selectAllAndConvert(page, 'section:nth-of-type(4) .editable');
  assert.equal(
    (await page.textContent('section:nth-of-type(4) .editable')).trim(),
    'السلام عليكم'
  );
  await page.close();
});

test('converts Arabic keystrokes back to English', { skip }, async () => {
  const page = await openPage();
  await selectAllAndConvert(page, 'section:nth-of-type(6) input');
  assert.equal(await page.inputValue('section:nth-of-type(6) input'), 'hello world');
  await page.close();
});

test('only the focused frame converts', { skip }, async () => {
  const page = await openPage();
  const frame = page.frames().find((f) => f !== page.mainFrame());
  assert.ok(frame, 'the fixture should contain an iframe');
  await frame.click('input');
  await page.keyboard.press('Control+A');
  await page.keyboard.press(SHORTCUT);
  await page.waitForTimeout(400);
  assert.equal(await frame.inputValue('input'), 'مرحبا');
  assert.equal(
    await page.inputValue('section:nth-of-type(2) textarea'),
    'lvpfh fpl\n;dt phg;',
    'the parent frame must be left alone'
  );
  await page.close();
});

test('number inputs are left alone', { skip }, async () => {
  const page = await openPage();
  await selectAllAndConvert(page, 'section:nth-of-type(8) input');
  assert.equal(await page.inputValue('section:nth-of-type(8) input'), '12345');
  await page.close();
});

test('a confirmation toast is shown', { skip }, async () => {
  const page = await openPage();
  await selectAllAndConvert(page, 'section:nth-of-type(1) input');
  const toast = await page.evaluate(() => {
    const host = document.querySelector('[data-klf-toast]');
    return host && host.shadowRoot.querySelector('.klf-toast').textContent;
  });
  assert.equal(toast, 'Converted');
  await page.close();
});

test('the popup and options pages load without console errors', { skip }, async () => {
  const context = await browser();
  const id = new URL(context.serviceWorkers()[0].url()).host;
  for (const ui of ['src/ui/popup.html', 'src/ui/options.html']) {
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', (e) => errors.push(String(e.message)));
    page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
    await page.goto(`chrome-extension://${id}/${ui}`);
    await page.waitForTimeout(700);
    assert.deepEqual(errors, [], `${ui} logged errors`);
    assert.ok((await page.title()).length > 0);
    await page.close();
  }
});

test('the popup converts pasted text in both directions', { skip }, async () => {
  const context = await browser();
  const id = new URL(context.serviceWorkers()[0].url()).host;
  const page = await context.newPage();
  await page.goto(`chrome-extension://${id}/src/ui/popup.html`);
  await page.waitForTimeout(400);

  await page.fill('#input', 'hgsghl');
  await page.waitForTimeout(250);
  assert.equal(await page.inputValue('#output'), 'السلام');

  await page.selectOption('#layout', 'ru');
  await page.fill('#input', 'ghbdtn');
  await page.waitForTimeout(250);
  assert.equal(await page.inputValue('#output'), 'привет');

  await page.click('#swap');
  await page.waitForTimeout(250);
  assert.equal(await page.inputValue('#output'), 'ghbdtn');
  await page.close();
});

test('settings saved in the options page change page behaviour', { skip }, async () => {
  const context = await browser();
  const id = new URL(context.serviceWorkers()[0].url()).host;
  const options = await context.newPage();
  await options.goto(`chrome-extension://${id}/src/ui/options.html`);
  await options.waitForTimeout(400);

  // "do nothing" must leave a collapsed caret untouched.
  await options.selectOption('#noSelectionAction', 'nothing');
  await options.click('#save');
  await options.waitForTimeout(600);

  const page = await openPage();
  const selector = 'section:nth-of-type(3) input';
  await page.click(selector);
  await page.keyboard.press('End');
  await page.keyboard.press(SHORTCUT);
  await page.waitForTimeout(400);
  assert.equal(await page.inputValue(selector), 'hello lvpfh');

  // ...while an explicit selection still converts.
  await selectAllAndConvert(page, selector);
  assert.equal(await page.inputValue(selector), 'اثممخ مرحبا');

  await page.close();
  await options.close();
});
