/* Options page. */
(function () {
  'use strict';

  const api = globalThis.chrome || globalThis.browser;
  const { converter, layouts, settings } = globalThis.KLF;
  const { message } = globalThis.KLFi18n;

  const $ = (id) => document.getElementById(id);

  let config = null;
  let hotkey = null;
  let recording = false;

  /* ---------------- hotkey helpers ---------------- */

  const IS_MAC = /Mac|iPhone|iPad/i.test(navigator.platform || navigator.userAgent);

  /** Human label for a key `code`, e.g. "KeyK" -> "K", "Space" -> "Space". */
  function labelForCode(code) {
    if (!code) return '?';
    if (code.startsWith('Key')) return code.slice(3);
    if (code.startsWith('Digit')) return code.slice(5);
    if (code.startsWith('Numpad')) return 'Numpad ' + code.slice(6);
    return code;
  }

  function describeHotkey(hk) {
    if (!hk || !hk.code) return message('popupShortcutUnset') || 'not set';
    const parts = [];
    if (hk.ctrl) parts.push(IS_MAC ? 'Control' : 'Ctrl');
    if (hk.alt) parts.push(IS_MAC ? 'Option' : 'Alt');
    if (hk.shift) parts.push('Shift');
    if (hk.meta) parts.push(IS_MAC ? 'Command' : 'Win');
    parts.push(labelForCode(hk.code));
    return parts.join(' + ');
  }

  const MODIFIER_CODES = /^(Control|Alt|Shift|Meta)(Left|Right)$/;

  function startRecording() {
    recording = true;
    $('record').textContent = message('optRecording') || 'Press the keys…';
    $('record').classList.add('primary');
  }

  function stopRecording() {
    recording = false;
    $('record').textContent = message('optRecord') || 'Record';
    $('record').classList.remove('primary');
    $('inPageHotkey').textContent = describeHotkey(hotkey);
  }

  window.addEventListener('keydown', (event) => {
    if (!recording) return;
    event.preventDefault();
    event.stopPropagation();
    if (event.key === 'Escape') { stopRecording(); return; }
    if (MODIFIER_CODES.test(event.code)) return; // wait for a real key
    hotkey = {
      code: event.code,
      ctrl: event.ctrlKey,
      shift: event.shiftKey,
      alt: event.altKey,
      meta: event.metaKey
    };
    stopRecording();
  }, true);

  /* ---------------- custom map serialisation ---------------- */

  function customMapToText(map, layoutId) {
    const entries = Object.entries((map && map[layoutId]) || {});
    return entries.map(([k, v]) => `${k}=${v}`).join('\n');
  }

  /** Parse "q=ض" lines. Returns the map plus a count of lines we could not use. */
  function parseCustomMap(text) {
    const map = {};
    let invalid = 0;
    for (const rawLine of String(text || '').split('\n')) {
      const line = rawLine.trim();
      if (!line || line.startsWith('#')) continue;
      const eq = line.indexOf('=');
      if (eq <= 0 || eq === line.length - 1) { invalid += 1; continue; }
      const key = line.slice(0, eq).trim();
      const value = line.slice(eq + 1).trim();
      if (!key || !value) { invalid += 1; continue; }
      map[key] = value;
    }
    return { map, invalid };
  }

  /* ---------------- preview ---------------- */

  function preview() {
    const source = $('tryInput').value;
    if (!source) { $('tryOutput').value = ''; return; }
    const { map } = parseCustomMap($('customMap').value);
    const layoutId = $('primaryLayout').value;
    converter.invalidate();
    const result = converter.convert(source, {
      primaryLayout: layoutId,
      // Mirror what save() stores: the primary layout is always recognisable.
      enabledLayouts: [...new Set([layoutId, ...readEnabledLayouts()])],
      mode: $('mode').value,
      customMap: { ...(config.customMap || {}), [layoutId]: map }
    });
    $('tryOutput').value = result.text;
  }

  /* ---------------- form <-> settings ---------------- */

  function readEnabledLayouts() {
    return [...document.querySelectorAll('#enabledLayouts input:checked')]
      .map((input) => input.value);
  }

  function buildLayoutControls() {
    const primary = $('primaryLayout');
    const list = $('enabledLayouts');
    for (const layout of layouts.listLayouts()) {
      const option = document.createElement('option');
      option.value = layout.id;
      option.textContent = `${layout.nameLocal} — ${layout.name}`;
      primary.appendChild(option);

      const label = document.createElement('label');
      label.className = 'check';
      const input = document.createElement('input');
      input.type = 'checkbox';
      input.value = layout.id;
      const span = document.createElement('span');
      span.textContent = `${layout.nameLocal} — ${layout.name}`;
      label.append(input, span);
      list.appendChild(label);
    }
  }

  function fillForm() {
    $('primaryLayout').value = config.primaryLayout;
    $('mode').value = config.mode;
    $('noSelectionAction').value = config.noSelectionAction;
    $('showToast').checked = !!config.showToast;
    $('copyWhenNotEditable').checked = !!config.copyWhenNotEditable;
    $('inPageHotkeyEnabled').checked = !!config.inPageHotkeyEnabled;
    $('disabledSites').value = (config.disabledSites || []).join('\n');
    $('customMap').value = customMapToText(config.customMap, config.primaryLayout);
    hotkey = { ...config.inPageHotkey };
    $('inPageHotkey').textContent = describeHotkey(hotkey);

    for (const input of document.querySelectorAll('#enabledLayouts input')) {
      input.checked = (config.enabledLayouts || []).includes(input.value);
    }
  }

  async function save() {
    const layoutId = $('primaryLayout').value;
    const { map, invalid } = parseCustomMap($('customMap').value);
    $('customWarning').hidden = invalid === 0;

    const enabled = readEnabledLayouts();
    // The primary layout must be recognisable, otherwise "auto" can never
    // convert its script back to English.
    if (!enabled.includes(layoutId)) enabled.push(layoutId);

    const customMap = { ...(config.customMap || {}) };
    if (Object.keys(map).length) customMap[layoutId] = map;
    else delete customMap[layoutId];

    const patch = {
      primaryLayout: layoutId,
      enabledLayouts: enabled,
      mode: $('mode').value,
      noSelectionAction: $('noSelectionAction').value,
      showToast: $('showToast').checked,
      copyWhenNotEditable: $('copyWhenNotEditable').checked,
      inPageHotkeyEnabled: $('inPageHotkeyEnabled').checked,
      inPageHotkey: hotkey || settings.DEFAULTS.inPageHotkey,
      disabledSites: $('disabledSites').value
        .split('\n').map((s) => s.trim()).filter(Boolean),
      customMap
    };

    await settings.set(patch);
    config = { ...config, ...patch };
    converter.invalidate();
    fillForm();

    $('status').textContent = message('optSaved') || 'Saved';
    setTimeout(() => { $('status').textContent = ''; }, 1600);
  }

  async function showBrowserShortcut() {
    if (!api.commands || !api.commands.getAll) return;
    try {
      const commands = await new Promise((resolve) => api.commands.getAll(resolve));
      const command = (commands || []).find((c) => c.name === 'convert-selection');
      $('browserShortcut').textContent =
        (command && command.shortcut) || message('popupShortcutUnset') || 'not set';
    } catch { /* leave the placeholder */ }
  }

  async function init() {
    buildLayoutControls();
    config = await settings.get();
    fillForm();
    showBrowserShortcut();

    $('save').addEventListener('click', save);
    $('record').addEventListener('click', () => (recording ? stopRecording() : startRecording()));

    $('reset').addEventListener('click', async () => {
      if (!confirm(message('optResetConfirm') || 'Reset every setting to its default?')) return;
      await settings.reset();
      config = await settings.get();
      converter.invalidate();
      fillForm();
      $('status').textContent = message('optSaved') || 'Saved';
      setTimeout(() => { $('status').textContent = ''; }, 1600);
    });

    $('primaryLayout').addEventListener('change', () => {
      $('customMap').value = customMapToText(config.customMap, $('primaryLayout').value);
      preview();
    });

    $('editShortcut').addEventListener('click', (event) => {
      event.preventDefault();
      const url = navigator.userAgent.includes('Firefox')
        ? 'about:addons'
        : 'chrome://extensions/shortcuts';
      api.tabs.create({ url });
    });

    for (const id of ['tryInput', 'customMap', 'mode']) {
      $(id).addEventListener('input', preview);
      $(id).addEventListener('change', preview);
    }
    $('enabledLayouts').addEventListener('change', preview);
  }

  document.addEventListener('DOMContentLoaded', init);
})();
