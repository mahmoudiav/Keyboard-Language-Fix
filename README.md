<div dir="rtl">

# مصحّح لغة الكيبورد — Keyboard Language Fix

إضافة للمتصفح تُنقذك عندما تكتب بلغة الكيبورد الخطأ.

تكتب `hgsghl` وأنت تقصد `السلام`؟ حدّد النص، اضغط <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Space</kbd>، فيُعاد كتابته فوراً باللغة التي قصدتها — دون حذف وإعادة كتابة.

يعمل في الاتجاهين: من الإنجليزية إلى العربية، ومن العربية إلى الإنجليزية، ويكتشف الاتجاه المطلوب من النص نفسه.

## المزايا

- **اختصار واحد** يعمل داخل حقول الإدخال، ومربعات النص، والمحرّرات الغنية (Gmail، وتطبيقات الدردشة، وصناديق التعليقات).
- **بلا تحديد**: إن لم تحدّد شيئاً، تُحوَّل الكلمة التي قبل المؤشّر مباشرة — وهي الحالة الأشيع.
- **نص غير قابل للتعديل**: يُحوَّل النص المحدَّد ويُنسخ إلى الحافظة، فيكفي <kbd>Ctrl</kbd> + <kbd>V</kbd>.
- **التراجع يعمل**: التبديل يمرّ عبر آلية التحرير الخاصة بالمتصفح، فيُلغيه <kbd>Ctrl</kbd> + <kbd>Z</kbd> كأي تعديل آخر.
- **خمسة تخطيطات مدمجة**: العربية (101)، والفارسية، والروسية (ЙЦУКЕН)، والعبرية، واليونانية.
- **قائمة سياق** (زر الفأرة الأيمن) و**نافذة منبثقة** للتحويل اليدوي للنص المنسوخ.
- **تخصيص**: اختصار داخل الصفحة، ومواقع مُستثناة، وتعديل جدول المفاتيح يدوياً.
- **بلا خوادم**: كل شيء يجري داخل المتصفح، ولا يُرسل أي نص إلى أي مكان.

## نسخة ويندوز

يوجد أيضاً تطبيق سطح مكتب لويندوز في مجلد [`windows/`](windows/README.md) يعمل
**خارج المتصفح**: في Word، وتيليجرام، وVS Code، وأي تطبيق آخر. نفس الاختصار،
ونفس جداول التحويل المولَّدة من المصدر ذاته، ومهيّأ للنشر في متجر مايكروسوفت
كحزمة MSIX.

**للمستخدم العادي**: نزّل ملف `Setup.exe` من صفحة
[Releases](../../releases) وانقر عليه نقراً مزدوجاً. لا يحتاج صلاحيات مدير.

**للمطوّر**: `./windows/build/build-setup.sh` يبني ملف التثبيت، أو
`windows\build\build-exe.cmd` لملف تنفيذي مجرّد. التفاصيل في
[`windows/README.md`](windows/README.md).

## التثبيت أثناء التطوير

**Chrome / Edge / Brave**

1. افتح `chrome://extensions`.
2. فعّل **وضع المطوّر** (Developer mode).
3. اضغط **تحميل غير محزوم** (Load unpacked) واختر مجلد المستودع.

**Firefox**

1. افتح `about:debugging#/runtime/this-firefox`.
2. اضغط **تحميل إضافة مؤقتة** (Load Temporary Add-on) واختر `manifest.firefox.json`.

للتحزيم للنشر:

```bash
npm test          # اختبارات التحويل والملفات (بلا متصفح)
npm run test:e2e  # تشغيل الإضافة فعلياً داخل Chromium
npm run test:windows  # اختبارات تطبيق ويندوز
npm run build     # ينتج dist/keyboard-language-fix-{chrome,firefox}-<version>.zip
```

## الاستخدام

| الحالة | ما تفعله | ما يحدث |
| --- | --- | --- |
| كتبت جملة كاملة بلغة خاطئة | حدّدها واضغط الاختصار | تُستبدل بالنص الصحيح |
| انتبهت بعد كلمة واحدة | اضغط الاختصار دون تحديد | تُحوَّل الكلمة التي قبل المؤشّر |
| النص في صفحة غير قابلة للتعديل | حدّده واضغط الاختصار | تُنسخ النتيجة إلى الحافظة |
| النص عندك في الحافظة | افتح أيقونة الإضافة والصقه | تُحوَّل في النافذة المنبثقة |

الاختصار الافتراضي هو <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Space</kbd>، ويمكن تغييره من `chrome://extensions/shortcuts`.

## الإعدادات

افتح صفحة الإعدادات من النافذة المنبثقة، أو من صفحة الإضافات:

- **تخطيط الكيبورد الثاني**: اللغة التي يُحوَّل إليها النص اللاتيني (العربية افتراضياً).
- **الاتجاه**: تلقائي، أو إجبار اتجاه واحد.
- **عند عدم التحديد**: الكلمة السابقة، أو الحقل كاملاً، أو لا شيء.
- **المواقع المُعطَّلة**: أسماء مضيفين تُترك وشأنها (تشمل النطاقات الفرعية).
- **تخصيص المفاتيح**: أسطر بصيغة `q=ض` تحلّ محل الجدول المدمج، لمن يستخدم تخطيطاً غير قياسي.

## كيف يعمل

كل تخطيط يُعرَّف كجدول يربط **المفتاح الفيزيائي على كيبورد US QWERTY** بالحرف الذي يُنتجه ذلك المفتاح في التخطيط الهدف. التحويل هو ببساطة تمرير النص عبر هذا الجدول، أو عبر معكوسه.

الاختصار داخل الصفحة يُقارَن بـ `KeyboardEvent.code` لا بـ `key`، لأن لغة الكيبورد وقت الضغط هي — بحكم المشكلة نفسها — اللغة الخطأ.

## البنية

</div>

```
manifest.json               Chrome / Edge / Brave  (MV3)
manifest.firefox.json       Firefox                (MV3)
src/core/layouts.js         layout tables (ar, fa, ru, he, el)
src/core/converter.js       the conversion engine + script detection
src/core/settings.js        chrome.storage wrapper and defaults
src/content/content.js      selection handling and in-place replacement
src/background/service-worker.js   shortcut + context menu
src/ui/                     popup and options pages
test/converter.test.mjs     unit tests for the engine and the layout tables
test/manifest.test.mjs      manifest / locale consistency checks
test/e2e.test.mjs           drives the loaded extension in Chromium
test/manual.html            fixture page for the browser tests
scripts/make-icons.py       renders icons/*.png, the .ico and the Store logos
scripts/generate-cs-layouts.mjs    layouts.js -> the Windows app's C# tables
scripts/generate-parity-fixture.mjs  records this engine's output for the C# tests
scripts/build.sh            packages the store zips
windows/                    the Windows desktop app (see windows/README.md)
```

---

<div dir="ltr">

## English

A browser extension for people who type on a bilingual physical keyboard and
forget to switch layouts.

Typed `hgsghl` when you meant `السلام`? Select it, press
<kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Space</kbd>, and it is re-typed in the
language you actually meant — no deleting, no retyping. It works in both
directions and figures out which one you need from the text itself.

Highlights:

- Works in `<input>`, `<textarea>`, and rich-text editors.
- With nothing selected it converts the word before the cursor.
- Read-only text is converted to the clipboard instead.
- <kbd>Ctrl</kbd> + <kbd>Z</kbd> undoes the swap like any other edit.
- Arabic (101), Persian, Russian (ЙЦУКЕН), Hebrew and Greek are built in.
- Right-click menu, plus a popup for converting pasted text by hand.
- Everything runs locally; no text ever leaves the browser.

### Windows desktop app

[`windows/`](windows/README.md) holds a Windows tray app that does the same job
**outside** the browser — in Word, Telegram, VS Code, anywhere. It is a .NET 8
WPF app packaged as MSIX for the Microsoft Store.

**To install it**: download `Setup.exe` from the
[Releases page](../../releases) and double-click it. Per-user install, no
administrator rights needed.

**To build it yourself**: `./windows/build/build-setup.sh` makes the installer
(.NET 8 SDK and NSIS, both of which cross-compile), or
`windows\build\build-exe.cmd` makes a plain executable. See
[windows/README.md](windows/README.md).

Both platforms share one source of truth: the C# layout tables are generated
from `src/core/layouts.js`, and the C# test suite replays 1028 cases recorded
from the JavaScript engine, so the two cannot drift apart.

```bash
npm run generate       # regenerate the C# tables after editing layouts.js
npm run test:windows   # dotnet test — runs on Linux and macOS too
npm run test:packaging # MSIX manifest and Store-requirement checks
```

### Development

```bash
npm test          # conversion, manifest, locale and generated-file checks
npm run test:e2e  # drives the real extension in Chromium (needs Playwright)
npm run test:all  # everything, including the Windows and packaging suites
npm run icons     # regenerate icons/*.png
npm run build     # package the store zips into dist/
```

Load unpacked from `chrome://extensions` (Developer mode), or load
`manifest.firefox.json` as a temporary add-on from
`about:debugging#/runtime/this-firefox`.

`test/manual.html` is a fixture page covering every input type the extension
touches — text inputs, textareas, `contenteditable`, read-only text, an iframe
and a `number` field that must stay untouched. `npm run test:e2e` drives that
same page through Playwright; open it by hand to check anything the automated
run cannot, such as the right-click menu. The browser tests skip themselves
when Playwright is not installed:

```bash
npm install -D playwright && npx playwright install chromium
```

### Adding a layout

Add an entry to `LAYOUTS` in `src/core/layouts.js` with:

- `base` — the un-shifted layer, keyed by the US QWERTY character
- `shift` — the shifted layer
- `script` — a regex matching the characters of the target script
- `shiftFallback: true` if the script has no upper case

The test suite checks every table for round-trip integrity and for keys that
accidentally produce the same character twice, so a new layout is covered the
moment it is added.

### About

Keyboard Language Fix — idea and implementation: **Mahmoud SATALEH**
<mahmoudiav@icloud.com>

Free software — free to use and free to share.

### License

MIT — see [LICENSE](LICENSE).

</div>
