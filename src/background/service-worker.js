/*
 * Keyboard Language Fix — background service worker.
 *
 * Deliberately thin: it owns the browser-level shortcut and the context menu,
 * and forwards a single message to the content scripts. All of the real work
 * happens in the frame that holds focus.
 */
'use strict';

const api = globalThis.chrome || globalThis.browser;

const CONVERT_COMMAND = 'convert-selection';
const MENU_ID = 'klf-convert';

function fallbackMenuTitle() {
  try {
    const msg = api.i18n.getMessage('contextMenuConvert');
    if (msg) return msg;
  } catch { /* fall through */ }
  return 'Fix keyboard language';
}

/** Ask the tab's frames to convert; the focused one answers, the rest opt out. */
function requestConversion(tabId) {
  if (tabId == null || tabId < 0) return;
  try {
    api.tabs.sendMessage(tabId, { type: 'klf:convert' }, () => {
      // A missing receiver just means the content script is not on this page
      // (chrome:// pages, the web store, PDFs). Swallow the error.
      void api.runtime.lastError;
    });
  } catch { /* nothing sensible to do from here */ }
}

api.commands.onCommand.addListener((command, tab) => {
  if (command !== CONVERT_COMMAND) return;
  if (tab && tab.id != null) {
    requestConversion(tab.id);
    return;
  }
  api.tabs.query({ active: true, currentWindow: true }, (tabs) => {
    if (tabs && tabs[0]) requestConversion(tabs[0].id);
  });
});

function createMenu() {
  if (!api.contextMenus) return;
  api.contextMenus.removeAll(() => {
    api.contextMenus.create({
      id: MENU_ID,
      title: fallbackMenuTitle(),
      contexts: ['selection', 'editable']
    }, () => void api.runtime.lastError);
  });
}

api.runtime.onInstalled.addListener((details) => {
  createMenu();
  // Pages that were already open have no content script yet; inject into them
  // so the shortcut works without a reload.
  if (api.scripting && api.scripting.executeScript) {
    api.tabs.query({ url: ['http://*/*', 'https://*/*'] }, (tabs) => {
      for (const tab of tabs || []) {
        if (tab.id == null) continue;
        api.scripting.executeScript({
          target: { tabId: tab.id, allFrames: true },
          files: [
            'src/core/layouts.js',
            'src/core/converter.js',
            'src/core/settings.js',
            'src/content/content.js'
          ]
        }, () => void api.runtime.lastError);
      }
    });
  }
  if (details && details.reason === 'install' && api.runtime.openOptionsPage) {
    api.runtime.openOptionsPage();
  }
});

if (api.runtime.onStartup) api.runtime.onStartup.addListener(createMenu);

if (api.contextMenus) {
  api.contextMenus.onClicked.addListener((info, tab) => {
    if (info.menuItemId !== MENU_ID || !tab || tab.id == null) return;
    requestConversion(tab.id);
  });
}
