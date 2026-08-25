/*
 * Keyboard Language Fix — settings storage.
 *
 * Everything lives in chrome.storage.sync so preferences follow the user
 * between machines; the API degrades to storage.local when sync is missing.
 */
(function (root) {
  'use strict';

  const api = root.chrome || root.browser;

  const DEFAULTS = {
    /** Layout used when the selected text is Latin. */
    primaryLayout: 'ar',
    /** Layouts considered when converting non-Latin text back to Latin. */
    enabledLayouts: ['ar'],
    /** 'auto' | 'toLayout' | 'toLatin' */
    mode: 'auto',
    /** What to convert when nothing is selected: 'lastWord' | 'wholeField' | 'nothing' */
    noSelectionAction: 'lastWord',
    /** Show the small in-page confirmation bubble. */
    showToast: true,
    /** Copy the result to the clipboard when the text cannot be edited in place. */
    copyWhenNotEditable: true,
    /** In-page hotkey, used alongside the browser-level command. */
    inPageHotkeyEnabled: true,
    inPageHotkey: { code: 'Space', ctrl: true, shift: true, alt: false, meta: false },
    /** Hostnames where the extension stays out of the way. */
    disabledSites: [],
    /** Per-layout key overrides: { ar: { q: 'ض' } } */
    customMap: {}
  };

  function storageArea() {
    if (api && api.storage) return api.storage.sync || api.storage.local;
    return null;
  }

  function get() {
    const area = storageArea();
    if (!area) return Promise.resolve({ ...DEFAULTS });
    return new Promise((resolve) => {
      area.get(DEFAULTS, (stored) => {
        if (api.runtime && api.runtime.lastError) resolve({ ...DEFAULTS });
        else resolve({ ...DEFAULTS, ...stored });
      });
    });
  }

  function set(patch) {
    const area = storageArea();
    if (!area) return Promise.resolve();
    return new Promise((resolve) => area.set(patch, () => resolve()));
  }

  function reset() {
    const area = storageArea();
    if (!area) return Promise.resolve();
    return new Promise((resolve) => area.clear(() => resolve()));
  }

  function onChange(cb) {
    if (!api || !api.storage || !api.storage.onChanged) return;
    api.storage.onChanged.addListener((changes, areaName) => {
      if (areaName === 'sync' || areaName === 'local') cb(changes);
    });
  }

  /** True when the extension should stay silent on this hostname. */
  function isDisabledFor(settings, hostname) {
    if (!settings || !Array.isArray(settings.disabledSites)) return false;
    const host = String(hostname || '').toLowerCase();
    return settings.disabledSites.some((raw) => {
      const pattern = String(raw || '').trim().toLowerCase().replace(/^\*\./, '');
      if (!pattern) return false;
      return host === pattern || host.endsWith('.' + pattern);
    });
  }

  const settingsApi = { DEFAULTS, get, set, reset, onChange, isDisabledFor };

  root.KLF = Object.assign(root.KLF || {}, { settings: settingsApi });
  if (typeof module !== 'undefined' && module.exports) module.exports = settingsApi;
})(typeof globalThis !== 'undefined' ? globalThis : this);
