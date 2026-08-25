**Typed in the wrong keyboard language?** Select the text, press <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Space</kbd>, and it is re-typed in the language you meant. No deleting, no retyping. Works in every Windows program.

<div dir="rtl">

**كتبت بلغة الكيبورد الخطأ؟** حدّد النص، اضغط <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Space</kbd>، فيُعاد كتابته باللغة التي قصدتها — دون حذف وإعادة كتابة. يعمل في كل برامج ويندوز.

</div>

---

## Download

| File | For |
| --- | --- |
| **`KeyboardLanguageFix-1.0.0-x64-Setup.exe`** | Almost every PC — start here |
| `KeyboardLanguageFix-1.0.0-arm64-Setup.exe` | Windows on ARM only (Surface Pro X, Snapdragon laptops) |

Download the file, double-click it, click **Install**. It installs for your user account only, so **no administrator rights are needed**.

> **Windows will show a blue warning the first time.** Click **More info**, then **Run anyway**. This appears for any free program without a paid code-signing certificate. If you would rather check the file yourself, the SHA-256 checksums are attached below.

## Using it

The app has **no window**. After it installs, its icon sits in the notification area beside the clock — click the `^` arrow if you do not see it.

1. Select some text in any program
2. Press <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Space</kbd>
3. The text is replaced

<kbd>Ctrl</kbd> + <kbd>Z</kbd> undoes it like any other edit. **Double-click the tray icon** to open Settings, where you can change the shortcut, pick your second keyboard layout, and turn "start with Windows" on or off.

It works in both directions and decides which way round from the text itself — you never have to tell it.

## Keyboard layouts

Arabic (101) · Persian · Russian (ЙЦУКЕН) · Hebrew · Greek

Not quite your layout? Settings lets you override individual keys.

## Requirements

Windows 10 (version 1809) or Windows 11. Nothing else — the .NET runtime is included in the download.

## Also in this repository

A **browser extension** for Chrome, Edge, Brave and Firefox, doing the same job inside web pages. Build it with `npm run build`, or load it unpacked. See the [README](../../#readme).

## Privacy

Nothing you type or select is sent anywhere. There is no network connection, no account, no telemetry. Your settings are one small file in `%LOCALAPPDATA%\KeyboardLanguageFix`.

The app registers a **single keyboard shortcut** with Windows. It is not able to see anything else you type — that is by design, not a promise.

## Known limits

- Does not work in windows running as administrator, such as Task Manager. Windows does not permit it, and the app never asks for elevation.
- Cannot read a selection from the few programs that ignore <kbd>Ctrl</kbd> + <kbd>C</kbd>.
- Only text is restored to your clipboard afterwards — a picture or files are not. Switch to *Type it out* in Settings to leave the clipboard untouched.
- Word capitalises the first letter of a sentence as you type. Most of that is corrected automatically, but six Arabic letters stay ambiguous (`Hgsghl` gives `ألسلام`, not `السلام`). Turn it off at the source: **File → Options → Proofing → AutoCorrect Options → uncheck "Capitalize first letter of sentences"**.

## Removing it

Settings → Apps → Installed apps → Keyboard Language Fix → Uninstall. Your preferences are kept so a reinstall does not lose them.

---

## About

**Idea and implementation: Mahmoud SATALEH**
<mahmoudiav@icloud.com>

**Free software** — free to use and free to share. Released under the MIT License.

Found a problem, or want a keyboard layout that is not in the list? Write to the address above.
