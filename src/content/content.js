/*
 * Keyboard Language Fix — content script.
 *
 * Runs in every frame. Resolves whatever the user has selected (or the word
 * they just typed), converts it, and writes it back in a way the host page
 * notices — including React-style controlled inputs and rich-text editors.
 */
(function () {
  'use strict';

  const api = globalThis.chrome || globalThis.browser;
  const { converter, settings } = globalThis.KLF;

  // `number` is deliberately absent: it rejects text that is not a number,
  // so a conversion there would silently blank the field. `email` is in, but
  // it exposes no selection API — see `inputRange`.
  const EDITABLE_INPUT_TYPES = new Set([
    'text', 'search', 'url', 'tel', 'password', 'email', ''
  ]);

  // Settings are cached eagerly rather than fetched on demand: the keydown
  // handler has to call preventDefault() synchronously, so it cannot await.
  let cachedSettings = null;
  let settingsPromise = null;

  function primeSettings() {
    settingsPromise = settings.get().then((loaded) => {
      cachedSettings = loaded;
      return loaded;
    });
    return settingsPromise;
  }

  function getSettings() {
    if (cachedSettings) return Promise.resolve(cachedSettings);
    return settingsPromise || primeSettings();
  }

  settings.onChange(() => {
    cachedSettings = null;
    converter.invalidate();
    primeSettings();
  });

  primeSettings();

  /* ------------------------------------------------------------------ *
   * Target resolution
   * ------------------------------------------------------------------ */

  /** activeElement, following open shadow roots down to the real focus. */
  function deepActiveElement() {
    let el = document.activeElement;
    while (el && el.shadowRoot && el.shadowRoot.activeElement) {
      el = el.shadowRoot.activeElement;
    }
    return el;
  }

  function isTextInput(el) {
    if (!el) return false;
    const tag = el.tagName;
    if (tag === 'TEXTAREA') return !el.disabled && !el.readOnly;
    if (tag === 'INPUT') {
      return !el.disabled && !el.readOnly &&
        EDITABLE_INPUT_TYPES.has((el.type || '').toLowerCase());
    }
    return false;
  }

  function isRichEditable(el) {
    return !!(el && el.isContentEditable);
  }

  /* ------------------------------------------------------------------ *
   * Reading and writing <input> / <textarea>
   * ------------------------------------------------------------------ */

  const WORD_BOUNDARY = /\s/;

  /** Walk backwards from `caret` over everything that is not whitespace. */
  function wordStartBefore(value, caret) {
    let start = caret;
    while (start > 0 && !WORD_BOUNDARY.test(value[start - 1])) start -= 1;
    return start;
  }

  function inputRange(el, noSelectionAction) {
    let start;
    let end;
    try {
      start = el.selectionStart;
      end = el.selectionEnd;
    } catch {
      start = end = null;
    }

    // `email` inputs (and a few others) expose no selection API at all, so the
    // whole value is the only range we can address.
    if (start == null || end == null) {
      if (noSelectionAction === 'nothing') return null;
      return { start: 0, end: el.value.length, wholeValue: true };
    }

    if (start !== end) return { start, end };

    if (noSelectionAction === 'wholeField') return { start: 0, end: el.value.length };
    if (noSelectionAction === 'lastWord') {
      const wordStart = wordStartBefore(el.value, start);
      if (wordStart === start) return null; // caret sits after whitespace
      return { start: wordStart, end: start };
    }
    return null;
  }

  /** Native value setter, so frameworks that patch `value` still see the change. */
  function nativeSetValue(el, value) {
    const proto = el.tagName === 'TEXTAREA'
      ? HTMLTextAreaElement.prototype
      : HTMLInputElement.prototype;
    const desc = Object.getOwnPropertyDescriptor(proto, 'value');
    if (desc && desc.set) desc.set.call(el, value);
    else el.value = value;
  }

  function replaceInInput(el, range, replacement) {
    el.focus();

    // execCommand keeps the browser's own undo stack intact (Ctrl+Z works),
    // but it needs a real selection to replace.
    let ok = false;
    if (!range.wholeValue) {
      try {
        el.setSelectionRange(range.start, range.end);
        ok = document.execCommand('insertText', false, replacement);
      } catch {
        ok = false;
      }
    }

    if (!ok) {
      const before = el.value.slice(0, range.start);
      const after = el.value.slice(range.end);
      nativeSetValue(el, before + replacement + after);
      el.dispatchEvent(new InputEvent('input', {
        bubbles: true, cancelable: false, inputType: 'insertText', data: replacement
      }));
      el.dispatchEvent(new Event('change', { bubbles: true }));
      const caret = range.start + replacement.length;
      try {
        el.setSelectionRange(caret, caret);
      } catch { /* the field has no selection API; nothing to restore */ }
    }
    return true;
  }

  /* ------------------------------------------------------------------ *
   * Reading and writing contenteditable
   * ------------------------------------------------------------------ */

  function activeSelection(el) {
    const root = el && el.getRootNode ? el.getRootNode() : document;
    if (root && typeof root.getSelection === 'function') return root.getSelection();
    return window.getSelection();
  }

  /**
   * Grow a collapsed caret backwards over the word just typed.
   * `Selection.modify` is non-standard but present in every engine we target;
   * the manual fallback handles the plain-text-node case.
   */
  function extendToLastWord(selection) {
    try {
      selection.modify('extend', 'backward', 'word');
      if (selection.toString().length) return true;
    } catch { /* fall through */ }

    const node = selection.focusNode;
    if (!node || node.nodeType !== Node.TEXT_NODE) return false;
    const offset = selection.focusOffset;
    const start = wordStartBefore(node.data, offset);
    if (start === offset) return false;
    const range = document.createRange();
    range.setStart(node, start);
    range.setEnd(node, offset);
    selection.removeAllRanges();
    selection.addRange(range);
    return true;
  }

  function replaceInEditable(replacement) {
    try {
      if (document.execCommand('insertText', false, replacement)) return true;
    } catch { /* fall through */ }

    const selection = window.getSelection();
    if (!selection || !selection.rangeCount) return false;
    const range = selection.getRangeAt(0);
    range.deleteContents();
    const node = document.createTextNode(replacement);
    range.insertNode(node);
    range.setStartAfter(node);
    range.collapse(true);
    selection.removeAllRanges();
    selection.addRange(range);
    const host = deepActiveElement();
    if (host) {
      host.dispatchEvent(new InputEvent('input', {
        bubbles: true, cancelable: false, inputType: 'insertText', data: replacement
      }));
    }
    return true;
  }

  /* ------------------------------------------------------------------ *
   * Clipboard fallback for text the page will not let us edit
   * ------------------------------------------------------------------ */

  async function copyToClipboard(text) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch { /* fall through to the legacy path */ }

    if (!document.body) return false;

    // The legacy path has to steal the selection, so put the user's back
    // afterwards — they are looking at the text they highlighted.
    const selection = window.getSelection();
    const saved = [];
    if (selection) {
      for (let i = 0; i < selection.rangeCount; i += 1) saved.push(selection.getRangeAt(i));
    }

    const helper = document.createElement('textarea');
    helper.value = text;
    helper.setAttribute('aria-hidden', 'true');
    helper.style.cssText = 'position:fixed;top:-1000px;left:-1000px;opacity:0;';
    document.body.appendChild(helper);
    helper.select();
    let ok = false;
    try {
      ok = document.execCommand('copy');
    } catch {
      ok = false;
    }
    helper.remove();

    if (selection && saved.length) {
      selection.removeAllRanges();
      for (const range of saved) selection.addRange(range);
    }
    return ok;
  }

  /* ------------------------------------------------------------------ *
   * Toast
   * ------------------------------------------------------------------ */

  let toastHost = null;
  let toastTimer = null;

  function showToast(message, tone) {
    if (!document.body && !document.documentElement) return;

    if (toastHost && !toastHost.isConnected) toastHost = null;

    if (!toastHost) {
      toastHost = document.createElement('div');
      toastHost.setAttribute('data-klf-toast', '');
      toastHost.style.cssText = 'all:initial;position:fixed;z-index:2147483647;';
      const shadow = toastHost.attachShadow({ mode: 'open' });
      shadow.innerHTML = `
        <style>
          .klf-toast {
            position: fixed;
            bottom: 24px;
            left: 50%;
            transform: translateX(-50%) translateY(8px);
            max-width: min(80vw, 520px);
            box-sizing: border-box;
            padding: 10px 16px;
            border-radius: 10px;
            background: #1f2430;
            color: #f2f4f8;
            font: 500 13px/1.5 -apple-system, "Segoe UI", Tahoma, "Noto Sans Arabic", sans-serif;
            box-shadow: 0 8px 28px rgba(0,0,0,.32);
            opacity: 0;
            transition: opacity .16s ease, transform .16s ease;
            pointer-events: none;
            white-space: pre-wrap;
            word-break: break-word;
            text-align: center;
          }
          .klf-toast.visible { opacity: 1; transform: translateX(-50%) translateY(0); }
          .klf-toast.warn { background: #6b3b12; color: #ffe9d2; }
        </style>
        <div class="klf-toast" part="toast"></div>`;
      (document.body || document.documentElement).appendChild(toastHost);
    }

    const bubble = toastHost.shadowRoot.querySelector('.klf-toast');
    bubble.textContent = message;
    bubble.classList.toggle('warn', tone === 'warn');
    requestAnimationFrame(() => bubble.classList.add('visible'));

    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => bubble.classList.remove('visible'), 2000);
  }

  function t(key, fallback, substitutions) {
    try {
      const msg = api.i18n.getMessage(key, substitutions);
      if (msg) return msg;
    } catch { /* fall through */ }
    return fallback;
  }

  /* ------------------------------------------------------------------ *
   * The actual command
   * ------------------------------------------------------------------ */

  function convertOptions(config) {
    return {
      primaryLayout: config.primaryLayout,
      enabledLayouts: config.enabledLayouts,
      mode: config.mode,
      customMap: config.customMap
    };
  }

  // The browser-level command and the in-page hotkey can, on some platforms,
  // both reach us for a single press. Converting twice would undo the fix, so
  // collapse anything that arrives within one keystroke's worth of time.
  const DOUBLE_FIRE_MS = 250;
  let lastRunAt = 0;

  async function runConversion() {
    const now = Date.now();
    if (now - lastRunAt < DOUBLE_FIRE_MS) return { handled: false, debounced: true };
    lastRunAt = now;

    const config = await getSettings();
    if (settings.isDisabledFor(config, location.hostname)) return { handled: false };

    const el = deepActiveElement();

    // `document.hasFocus()` is true in the top document whenever *any* of its
    // frames has focus, so the top frame must step aside for the child that
    // actually holds the caret.
    if (el && (el.tagName === 'IFRAME' || el.tagName === 'FRAME')) {
      return { handled: false };
    }

    // 1. Plain text fields — the common case.
    if (isTextInput(el)) {
      const range = inputRange(el, config.noSelectionAction);
      if (!range || range.start === range.end) return { handled: false };
      const source = el.value.slice(range.start, range.end);
      const result = converter.convert(source, convertOptions(config));
      if (!result.changed) return { handled: false };
      replaceInInput(el, range, result.text);
      if (config.showToast) {
        showToast(t('toastConverted', 'Converted'));
      }
      return { handled: true, direction: result.direction, layoutId: result.layoutId };
    }

    // 2. Rich text editors (Gmail, chat apps, comment boxes).
    if (isRichEditable(el)) {
      const selection = activeSelection(el);
      if (!selection) return { handled: false };
      if (selection.isCollapsed) {
        if (config.noSelectionAction === 'nothing') return { handled: false };
        if (!extendToLastWord(selection)) return { handled: false };
      }
      const source = selection.toString();
      if (!source) return { handled: false };
      const result = converter.convert(source, convertOptions(config));
      if (!result.changed) return { handled: false };
      replaceInEditable(result.text);
      if (config.showToast) {
        showToast(t('toastConverted', 'Converted'));
      }
      return { handled: true, direction: result.direction, layoutId: result.layoutId };
    }

    // 3. Read-only text: convert and hand it back through the clipboard.
    const selection = window.getSelection();
    const source = selection ? selection.toString() : '';
    if (!source.trim()) return { handled: false };
    const result = converter.convert(source, convertOptions(config));
    if (!result.changed) return { handled: false };
    if (config.copyWhenNotEditable) {
      const copied = await copyToClipboard(result.text);
      if (config.showToast) {
        showToast(
          copied
            ? t('toastCopied', 'Converted and copied — press Ctrl+V to paste')
            : t('toastCopyFailed', 'Could not copy the result'),
          copied ? '' : 'warn'
        );
      }
      return { handled: copied, direction: result.direction, layoutId: result.layoutId };
    }
    return { handled: false };
  }

  /* ------------------------------------------------------------------ *
   * Triggers
   * ------------------------------------------------------------------ */

  function matchesHotkey(event, hotkey) {
    if (!hotkey) return false;
    if (event.ctrlKey !== !!hotkey.ctrl) return false;
    if (event.shiftKey !== !!hotkey.shift) return false;
    if (event.altKey !== !!hotkey.alt) return false;
    if (event.metaKey !== !!hotkey.meta) return false;
    // `code` is layout-independent, which matters a lot for this extension:
    // the user's keyboard is, by definition, in the "wrong" language.
    return event.code === hotkey.code;
  }

  document.addEventListener('keydown', (event) => {
    if (event.defaultPrevented || !event.isTrusted) return;
    const config = cachedSettings;
    if (!config || !config.inPageHotkeyEnabled) return;
    if (!matchesHotkey(event, config.inPageHotkey)) return;
    // Synchronous, so the page never sees the keystroke.
    event.preventDefault();
    event.stopPropagation();
    runConversion();
  }, true);

  api.runtime.onMessage.addListener((message, _sender, sendResponse) => {
    if (!message || message.type !== 'klf:convert') return undefined;
    // Only the frame that actually holds focus should act.
    if (!document.hasFocus()) {
      sendResponse({ handled: false, focused: false });
      return true;
    }
    runConversion().then(
      (result) => sendResponse({ ...result, focused: true }),
      () => sendResponse({ handled: false, focused: true })
    );
    return true; // keep the message channel open for the async reply
  });
})();
