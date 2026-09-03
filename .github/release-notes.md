**Typed in the wrong keyboard language?** Select the text, press <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Space</kbd>, and it is re-typed in the language you meant. No deleting, no retyping. Works in every Windows program.

<div dir="rtl">

**كتبت بلغة الكيبورد الخطأ؟** حدّد النص، اضغط <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Space</kbd>، فيُعاد كتابته باللغة التي قصدتها — دون حذف وإعادة كتابة. يعمل في كل برامج ويندوز.

</div>

---

## New in 1.1.0

**Spanish (Spain).** The first layout that shares the alphabet with English, so
only the punctuation and the accents move — `Espa;a` becomes `España`, `est'a`
becomes `está`, and going the other way `console.log)x=ñ` becomes
`console.log(x);`. The accents are dead keys on a real Spanish keyboard, and
they are treated as such here: `'` and then a vowel is one letter, not two.

**A right-click entry in Windows.** Right-click a text file in File Explorer and
choose **Fix keyboard language** to convert the whole file — no selecting. It
opens showing the text before and after, and writes nothing until you press
Save, keeping what you had beside it as `.bak`. Windows 11 puts it under **Show
more options**; Settings can turn it off.

<div dir="rtl">

**الإسبانية**، وهي أول تخطيط يشارك الإنجليزية أبجديتها، فلا يتغيّر إلا الترقيم
والحركات. و**أمر في قائمة الزر الأيمن** لتحويل ملف نصي كاملاً من مستكشف الملفات،
مع عرض النتيجة قبل الحفظ. في ويندوز 11 تجده تحت «إظهار المزيد من الخيارات».

</div>

---

## Download

| File | For |
| --- | --- |
| **`KeyboardLanguageFix-1.1.0-x64-Setup.exe`** | Almost every PC — start here |
| `KeyboardLanguageFix-1.1.0-arm64-Setup.exe` | Windows on ARM only (Surface Pro X, Snapdragon laptops) |

Download the file, double-click it, click **Install**. It installs for your user account only, so **no administrator rights are needed**.

> **Windows will show a blue warning the first time.** Click **More info**, then **Run anyway**.
>
> Read what it says: SmartScreen *could not verify* the file *because it is not commonly downloaded*. That is about how popular the file is, not about what is in it — Defender found nothing. It says "unknown publisher" because the file is not signed with a code-signing certificate, which costs a few hundred dollars a year and this tool is free.
>
> You can check it yourself instead of trusting it: the source is public, the installer is built by GitHub from that source with a public build log, `SHA256SUMS.txt` is attached below, and each file carries a signed provenance record —
> `gh attestation verify KeyboardLanguageFix-x64-Setup.exe --repo mahmoudiav/Keyboard-Language-Fix`

## Using it

The app has **no window**. After it installs, its icon sits in the notification area beside the clock — click the `^` arrow if you do not see it.

1. Select some text in any program
2. Press <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Space</kbd>
3. The text is replaced

<kbd>Ctrl</kbd> + <kbd>Z</kbd> undoes it like any other edit. **Double-click the tray icon** to open Settings, where you can change the shortcut, pick your second keyboard layout, and turn "start with Windows" on or off.

It works in both directions and decides which way round from the text itself — you never have to tell it.

## Keyboard layouts

Arabic (101) · Persian · Russian (ЙЦУКЕН) · Hebrew · Greek · Spanish (Spain)

Not quite your layout? Settings lets you override individual keys.

## A whole file at once

Right-click a text file in File Explorer → **Fix keyboard language** (Windows 11:
under **Show more options**). The file opens with its text before and after;
nothing is written until you press Save, and the version you had is kept beside
it with `.bak` on the end. Plain text saved as UTF-8 or UTF-16 — an older
Windows code page is refused rather than guessed at.

Selected text inside a program is what the shortcut is for. Windows gives no
program a way to add a command to another program's text menu.

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
- Spanish shares its letters with English, so a selection that is already correct English is left alone rather than guessed at. Pick a direction in Settings to force it either way.
- Word capitalises the first letter of a sentence as you type. Most of that is corrected automatically, but six Arabic letters stay ambiguous (`Hgsghl` gives `ألسلام`, not `السلام`). Turn it off at the source: **File → Options → Proofing → AutoCorrect Options → uncheck "Capitalize first letter of sentences"**.

## Removing it

Settings → Apps → Installed apps → Keyboard Language Fix → Uninstall. Your preferences are kept so a reinstall does not lose them.

---

## About

**Idea and implementation: Mahmoud SATALEH**
<mahmoudiav@icloud.com>

**Free software** — free to use and free to share. Released under the MIT License.

Found a problem, or want a keyboard layout that is not in the list? Write to the address above.
