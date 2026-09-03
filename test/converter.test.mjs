import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const layouts = require('../src/core/layouts.js');
const { convert, detectLayout } = require('../src/core/converter.js');

const ar = { primaryLayout: 'ar', enabledLayouts: ['ar'] };

test('English keystrokes become the Arabic the user meant', () => {
  assert.equal(convert('hgsghl', ar).text, 'السلام');
  assert.equal(convert('hgslhx', ar).text, 'السماء');
  assert.equal(convert('lvpfh', ar).text, 'مرحبا');
  assert.equal(convert(';dt phg;', ar).text, 'كيف حالك');
});

test('Arabic keystrokes become the English the user meant', () => {
  assert.equal(convert('اثممخ', ar).text, 'hello');
  assert.equal(convert('صخقمي', ar).text, 'world');
});

test('round trip is stable for the letter rows', () => {
  const source = 'the quick brown fox jumps over the lazy dog';
  const arabic = convert(source, { ...ar, mode: 'toLayout' }).text;
  assert.notEqual(arabic, source);
  assert.equal(convert(arabic, { ...ar, mode: 'toLatin' }).text, source);
});

test('the "لا" ligature survives a round trip', () => {
  const arabic = convert('b', { ...ar, mode: 'toLayout' }).text;
  assert.equal(arabic, 'لا');
  assert.equal(convert(arabic, { ...ar, mode: 'toLatin' }).text, 'b');
});

test('multi-character keys win over their first character', () => {
  // "لأ" must map back to "G", not to "ل" followed by a stray "أ".
  assert.equal(convert('لأ', { ...ar, mode: 'toLatin' }).text, 'G');
  assert.equal(convert('لآ', { ...ar, mode: 'toLatin' }).text, 'B');
  assert.equal(convert('لإ', { ...ar, mode: 'toLatin' }).text, 'T');
});

test('a capital the user never typed does not become punctuation', () => {
  // Word capitalises the first letter of a sentence, so "lpl,]" arrives as
  // "Lpl,]" — and Shift+L on the Arabic layout is "/", not a letter.
  assert.equal(convert('lpl,]', ar).text, 'محمود');
  assert.equal(convert('Lpl,]', ar).text, 'محمود');
});

test('Caps Lock does not turn a word into punctuation either', () => {
  assert.equal(convert('LPL,]', ar).text, 'محمود');
});

test('a capital that yields a real letter is left alone', () => {
  // Shift+H is how you type أ; that capital is deliberate.
  assert.equal(convert('Hpl]', ar).text, 'أحمد');
  assert.equal(convert('HPL]', ar).text, 'أحمد');
});

test('punctuation typed with Shift still works', () => {
  // "،" is Shift+K, always at the end of a word — never with a word glued to
  // its right, which is exactly what separates it from an accidental capital.
  assert.equal(convert('hgslhxK rvdfh', { ...ar, mode: 'toLayout' }).text, 'السماء، قريبا');
  assert.equal(convert('K', { ...ar, mode: 'toLayout' }).text, '،');
  assert.equal(convert('P', { ...ar, mode: 'toLayout' }).text, '؛');
  assert.equal(convert('L', { ...ar, mode: 'toLayout' }).text, '/');
});

test('layouts whose shift layer is their own upper case are untouched', () => {
  const ru = { primaryLayout: 'ru', enabledLayouts: ['ru'] };
  const el = { primaryLayout: 'el', enabledLayouts: ['el'] };
  assert.equal(convert('CASE', { ...ru, mode: 'toLayout' }).text, 'СФЫУ');
  assert.equal(convert('Ghbdtn', ru).text, 'Привет');
  assert.equal(convert('CASE', { ...el, mode: 'toLayout' }).text, 'ΨΑΣΕ');
});

test('auto direction follows the dominant script', () => {
  assert.equal(convert('hgsghl', ar).direction, 'toLayout');
  assert.equal(convert('اثممخ', ar).direction, 'toLatin');
});

test('digits, spaces and unmapped characters pass through', () => {
  const out = convert('abc 123 @', { ...ar, mode: 'toLayout' }).text;
  assert.ok(out.includes(' 123 '));
  assert.ok(out.endsWith('@'));
});

test('empty and whitespace input report no change', () => {
  assert.equal(convert('', ar).changed, false);
  assert.equal(convert('   ', ar).changed, false);
});

test('Russian: the classic ghbdtn', () => {
  const ru = { primaryLayout: 'ru', enabledLayouts: ['ru'] };
  assert.equal(convert('ghbdtn', ru).text, 'привет');
  assert.equal(convert('привет', ru).text, 'ghbdtn');
  assert.equal(convert('Ghbdtn', ru).text, 'Привет');
});

test('Hebrew folds upper case onto the base layer', () => {
  const he = { primaryLayout: 'he', enabledLayouts: ['he'] };
  assert.equal(convert('shalom', he).text, convert('SHALOM', he).text);
});

test('Greek keeps its own case distinction', () => {
  const el = { primaryLayout: 'el', enabledLayouts: ['el'] };
  assert.equal(convert('kala', el).text, 'καλα');
  assert.equal(convert('Kala', el).text, 'Καλα');
});

test('Persian maps its own letters, not the Arabic ones', () => {
  const fa = { primaryLayout: 'fa', enabledLayouts: ['fa'] };
  assert.equal(convert('d', fa).text, 'ی');   // Persian yeh
  assert.equal(convert(';', fa).text, 'ک');   // Persian keheh
  assert.equal(convert(']', fa).text, 'چ');
});

test('Spanish: the ñ key and the accents a US keyboard cannot reach', () => {
  const es = { primaryLayout: 'es', enabledLayouts: ['es'] };
  assert.equal(convert('Espa;a', es).text, 'España');
  assert.equal(convert('Ma;ana', es).text, 'Mañana');
  assert.equal(convert('a;o', es).text, 'año');
});

test('Spanish dead keys compose with the vowel that follows', () => {
  const es = { primaryLayout: 'es', enabledLayouts: ['es'], mode: 'toLayout' };
  // Two keystrokes, one letter: the acute key, then the vowel.
  assert.equal(convert("est'a", es).text, 'está');
  assert.equal(convert("'Angel", es).text, 'Ángel');
  assert.equal(convert('ping"uino', es).text, 'pingüino');
  // A capital reached through a dead key is deliberate, so it stays a capital.
  assert.equal(convert("'ANGEL", es).text, 'ÁNGEL');
  // The dead key on its own is still the accent character.
  assert.equal(convert("'", es).text, '´');
});

test('Spanish letters are identical, so only the punctuation moves', () => {
  const es = { primaryLayout: 'es', enabledLayouts: ['es'], mode: 'toLayout' };
  // a-z sits on the same keys in both layouts: nothing to fix, so nothing is
  // touched — and the caller is told so rather than being handed a copy.
  assert.equal(convert('hello world', es).changed, false);
  assert.equal(convert('HOLA', es).text, 'HOLA');
});

test('Spanish direction turns on what a US keyboard cannot type', () => {
  const es = { primaryLayout: 'es', enabledLayouts: ['es'] };
  // Both alphabets are Latin, so counting letters decides nothing. One "ñ" is
  // proof the text has already been through the Spanish layout.
  assert.equal(convert('Espa;a', es).direction, 'toLayout');
  assert.equal(convert('España', es).direction, 'toLatin');
  assert.equal(convert('España', es).text, 'Espa;a');
  // The everyday complaint of anyone writing code on a Spanish keyboard.
  assert.equal(convert('console.log)x=ñ', es).text, 'console.log(x);');
});

test('Spanish stays out of the way of the other layouts', () => {
  const both = { primaryLayout: 'ar', enabledLayouts: ['ar', 'es'] };
  assert.equal(convert('مرحبا', both).text, 'lvpfh');
  assert.equal(detectLayout('mañana', ['ar', 'es', 'ru']).id, 'es');
  assert.equal(detectLayout('مرحبا', ['ar', 'es']).id, 'ar');
  assert.equal(detectLayout('hello world', ['ar', 'es']), null);
});

test('detectLayout picks the script actually present', () => {
  assert.equal(detectLayout('مرحبا', ['ar', 'ru', 'he']).id, 'ar');
  assert.equal(detectLayout('привет', ['ar', 'ru', 'he']).id, 'ru');
  assert.equal(detectLayout('שלום', ['ar', 'ru', 'he']).id, 'he');
  assert.equal(detectLayout('hello', ['ar', 'ru', 'he']), null);
});

test('custom mappings override the built-in table both ways', () => {
  const opts = { ...ar, customMap: { ar: { q: 'ﻻ' } } };
  assert.equal(convert('q', { ...opts, mode: 'toLayout' }).text, 'ﻻ');
  assert.equal(convert('ﻻ', { ...opts, mode: 'toLatin' }).text, 'q');
});

test('forced modes ignore the detected script', () => {
  assert.equal(convert('hello', { ...ar, mode: 'toLatin' }).text, 'hello');
  assert.equal(convert('مرحبا', { ...ar, mode: 'toLayout' }).text, 'مرحبا');
});

test('every layout maps each of its own characters back to a key', () => {
  for (const layout of layouts.LAYOUTS) {
    const opts = { primaryLayout: layout.id, enabledLayouts: [layout.id] };
    for (const [key, value] of Object.entries(layout.base)) {
      const back = convert(value, { ...opts, mode: 'toLatin' }).text;
      assert.equal(back, key, `${layout.id}: ${value} should map back to ${key}`);
    }
  }
});

test('no layout produces the same character from two keys by accident', () => {
  for (const layout of layouts.LAYOUTS) {
    const allowed = new Set(layout.ambiguous || []);
    const seen = new Map();
    for (const [layer, table] of [['base', layout.base], ['shift', layout.shift]]) {
      for (const [key, value] of Object.entries(table)) {
        if (seen.has(value) && allowed.has(value)) continue;
        assert.ok(
          !seen.has(value),
          `${layout.id}: "${value}" produced by both ${seen.get(value)} and ${layer}.${key}`
        );
        seen.set(value, `${layer}.${key}`);
      }
    }
  }
});
