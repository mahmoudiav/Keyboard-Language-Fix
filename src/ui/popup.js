/* Popup: a manual converter for text you cannot fix in place. */
(function () {
  'use strict';

  const api = globalThis.chrome || globalThis.browser;
  const { converter, layouts, settings } = globalThis.KLF;
  const { message } = globalThis.KLFi18n;

  const $ = (id) => document.getElementById(id);

  let config = null;

  function convertNow() {
    const source = $('input').value;
    const layoutId = $('layout').value;
    const result = converter.convert(source, {
      primaryLayout: layoutId,
      // The dropdown *is* the choice here, so it is also the only layout we
      // try to recognise when converting back to Latin. Anything else makes
      // the "use the result as input" button a no-op.
      enabledLayouts: [layoutId],
      mode: config.mode,
      customMap: config.customMap
    });
    const output = $('output');
    output.value = result.text;
    const layout = layouts.getLayout(result.layoutId);
    output.dir = (result.direction === 'toLayout' && layout && layout.rtl) ? 'rtl' : 'ltr';
    $('status').textContent = '';
  }

  function flash(text) {
    $('status').textContent = text;
    setTimeout(() => { $('status').textContent = ''; }, 1600);
  }

  async function showShortcut() {
    if (!api.commands || !api.commands.getAll) return;
    try {
      const commands = await new Promise((resolve) => api.commands.getAll(resolve));
      const command = (commands || []).find((c) => c.name === 'convert-selection');
      $('shortcut').textContent =
        (command && command.shortcut) || message('popupShortcutUnset') || 'not set';
    } catch { /* leave the placeholder */ }
  }

  async function init() {
    config = await settings.get();

    const select = $('layout');
    for (const layout of layouts.listLayouts()) {
      const option = document.createElement('option');
      option.value = layout.id;
      option.textContent = `${layout.nameLocal} — ${layout.name}`;
      select.appendChild(option);
    }
    select.value = config.primaryLayout;

    $('input').dir = 'auto';
    $('convert').addEventListener('click', convertNow);
    select.addEventListener('change', convertNow);
    $('input').addEventListener('input', convertNow);

    $('input').addEventListener('keydown', (event) => {
      if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
        event.preventDefault();
        convertNow();
      }
    });

    $('copy').addEventListener('click', async () => {
      const text = $('output').value;
      if (!text) return;
      try {
        await navigator.clipboard.writeText(text);
        flash(message('popupCopied') || 'Copied');
      } catch {
        $('output').select();
        document.execCommand('copy');
        flash(message('popupCopied') || 'Copied');
      }
    });

    $('swap').addEventListener('click', () => {
      const text = $('output').value;
      if (!text) return;
      $('input').value = text;
      convertNow();
    });

    $('options').addEventListener('click', (event) => {
      event.preventDefault();
      api.runtime.openOptionsPage();
    });

    $('editShortcut').addEventListener('click', (event) => {
      event.preventDefault();
      // Firefox has no chrome://extensions/shortcuts equivalent to link to.
      const url = navigator.userAgent.includes('Firefox')
        ? 'about:addons'
        : 'chrome://extensions/shortcuts';
      api.tabs.create({ url });
    });

    showShortcut();
  }

  document.addEventListener('DOMContentLoaded', init);
})();
