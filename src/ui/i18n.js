/* Fills [data-i18n] elements from _locales, and flips the page for RTL UI languages. */
(function () {
  'use strict';
  const api = globalThis.chrome || globalThis.browser;

  function message(key) {
    try {
      return api.i18n.getMessage(key) || '';
    } catch {
      return '';
    }
  }

  function apply(root) {
    for (const el of (root || document).querySelectorAll('[data-i18n]')) {
      const text = message(el.dataset.i18n);
      if (text) el.textContent = text;
    }
    for (const el of (root || document).querySelectorAll('[data-i18n-placeholder]')) {
      const text = message(el.dataset.i18nPlaceholder);
      if (text) el.placeholder = text;
    }
  }

  let uiLang = 'en';
  try {
    uiLang = api.i18n.getUILanguage() || 'en';
  } catch { /* keep the default */ }

  const rtl = /^(ar|fa|he|ur|ps|sd|ug|yi)\b/i.test(uiLang);
  document.documentElement.lang = uiLang;
  document.documentElement.dir = rtl ? 'rtl' : 'ltr';

  document.addEventListener('DOMContentLoaded', () => apply(document));
  globalThis.KLFi18n = { apply, message };
})();
