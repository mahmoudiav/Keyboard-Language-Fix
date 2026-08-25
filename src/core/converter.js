/*
 * Keyboard Language Fix — conversion engine.
 *
 * Turns text that was typed on the wrong keyboard layout into the text the
 * user actually meant, by re-mapping every character through the physical key
 * that produced it.
 */
(function (root) {
  'use strict';

  const layoutsApi = (root.KLF && root.KLF.layouts) ||
    (typeof require === 'function' ? require('./layouts.js') : null);

  const LATIN_RE = /[A-Za-z]/;

  /** Cache of compiled maps, keyed by layout id + a signature of the overrides. */
  const cache = new Map();

  function compile(layout, overrides) {
    const toLayout = new Map();
    const toLatin = new Map();

    const put = (key, value) => {
      if (value === '' || value == null) return;
      toLayout.set(key, value);
      // First writer wins, so the un-shifted layer is preferred when reversing.
      if (!toLatin.has(value)) toLatin.set(value, key);
    };

    for (const [k, v] of Object.entries(layout.base)) put(k, v);
    for (const [k, v] of Object.entries(layout.shift)) put(k, v);

    if (layout.shiftFallback) {
      // No case distinction in the target script: fold A-Z onto a-z.
      for (const [k, v] of Object.entries(layout.base)) {
        const upper = k.toUpperCase();
        if (upper !== k && !toLayout.has(upper)) toLayout.set(upper, v);
      }
    }

    if (overrides) {
      for (const [k, v] of Object.entries(overrides)) {
        if (typeof k !== 'string' || typeof v !== 'string' || !k) continue;
        toLayout.set(k, v);
        toLatin.set(v, k); // user overrides win in both directions
      }
    }

    return {
      toLayout,
      toLatin,
      toLayoutWidths: multiWidths(toLayout.keys()),
      toLatinWidths: multiWidths(toLatin.keys())
    };
  }

  /** Descending list of key lengths > 1, so multi-character keys match first. */
  function multiWidths(keys) {
    const widths = new Set();
    for (const k of keys) if (k.length > 1) widths.add(k.length);
    return [...widths].sort((a, b) => b - a);
  }

  function getMaps(layout, overrides) {
    const sig = layout.id + '|' + (overrides ? JSON.stringify(overrides) : '');
    let maps = cache.get(sig);
    if (!maps) {
      maps = compile(layout, overrides);
      cache.set(sig, maps);
    }
    return maps;
  }

  /** Clear the compiled-map cache (call after the user edits custom mappings). */
  function invalidate() {
    cache.clear();
  }

  function applyMap(text, map, widths) {
    let out = '';
    let i = 0;
    while (i < text.length) {
      let hit = null;
      for (const w of widths) {
        const chunk = text.substr(i, w);
        if (chunk.length === w && map.has(chunk)) {
          hit = { value: map.get(chunk), width: w };
          break;
        }
      }
      if (hit) {
        out += hit.value;
        i += hit.width;
        continue;
      }
      const ch = text[i];
      out += map.has(ch) ? map.get(ch) : ch;
      i += 1;
    }
    return out;
  }

  /**
   * Score how strongly `text` looks like Latin vs. like a given layout's script.
   * Characters that belong to neither (digits, spaces, punctuation) are ignored.
   */
  function score(text, layout) {
    let latin = 0;
    let target = 0;
    for (const ch of text) {
      if (LATIN_RE.test(ch)) latin += 1;
      else if (layout.script.test(ch)) target += 1;
    }
    return { latin, target };
  }

  /**
   * Pick the layout a piece of non-Latin text was most likely typed in.
   * Returns null when the text carries no recognisable script.
   */
  function detectLayout(text, candidateIds) {
    const ids = (candidateIds && candidateIds.length)
      ? candidateIds
      : layoutsApi.LAYOUTS.map((l) => l.id);

    let best = null;
    for (const id of ids) {
      const layout = layoutsApi.getLayout(id);
      if (!layout) continue;
      let hits = 0;
      for (const ch of text) if (layout.script.test(ch)) hits += 1;
      if (hits > 0 && (!best || hits > best.hits)) best = { layout, hits };
    }
    return best ? best.layout : null;
  }

  /**
   * Convert `text`.
   *
   * options:
   *   primaryLayout {string}  layout used when the text is Latin        (default 'ar')
   *   enabledLayouts {string[]} layouts considered when reversing
   *   mode {'auto'|'toLayout'|'toLatin'}                                (default 'auto')
   *   customMap {object}      { layoutId: { key: value } } overrides
   *
   * Returns { text, changed, direction, layoutId } — `changed` is false when
   * there was nothing sensible to do, so callers can leave the page alone.
   */
  function convert(text, options) {
    const opts = options || {};
    const mode = opts.mode || 'auto';
    const primary = layoutsApi.getLayout(opts.primaryLayout || 'ar');
    if (!text || !primary) {
      return { text: text || '', changed: false, direction: null, layoutId: null };
    }

    const enabled = (opts.enabledLayouts && opts.enabledLayouts.length)
      ? opts.enabledLayouts
      : [primary.id];

    let direction;
    let layout;

    if (mode === 'toLayout') {
      direction = 'toLayout';
      layout = primary;
    } else if (mode === 'toLatin') {
      direction = 'toLatin';
      layout = detectLayout(text, enabled) || primary;
    } else {
      const detected = detectLayout(text, enabled);
      const s = score(text, detected || primary);
      if (detected && s.target >= s.latin) {
        direction = 'toLatin';
        layout = detected;
      } else {
        direction = 'toLayout';
        layout = primary;
      }
    }

    const overrides = opts.customMap ? opts.customMap[layout.id] : null;
    const maps = getMaps(layout, overrides);
    const out = direction === 'toLayout'
      ? applyMap(text, maps.toLayout, maps.toLayoutWidths)
      : applyMap(text, maps.toLatin, maps.toLatinWidths);

    return {
      text: out,
      changed: out !== text,
      direction,
      layoutId: layout.id
    };
  }

  const api = { convert, detectLayout, invalidate, LATIN_RE };

  root.KLF = Object.assign(root.KLF || {}, { converter: api });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof globalThis !== 'undefined' ? globalThis : this);
