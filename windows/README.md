# Keyboard Language Fix for Windows

The same idea as the browser extension, but system-wide: select text in **any**
Windows app, press one shortcut, and it is re-typed in the language you meant.

Default shortcut: <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Space</kbd>.

The app has no window of its own. It sits in the notification area; double-click
the tray icon for settings.

## Getting an .exe

### The short way

1. Install the .NET 8 SDK once — `winget install Microsoft.DotNet.SDK.8`, or from
   <https://dotnet.microsoft.com/download/dotnet/8.0>.
2. Double-click **`windows\build\build-exe.cmd`**.
3. It leaves `KeyboardLanguageFix.exe` in `windows\dist\exe` and opens the folder.

Double-click the exe. Nothing appears to happen — that is correct, the app has no
window. Its icon goes into the notification area beside the clock; click the `^`
arrow if you do not see it. Now select some text anywhere and press
<kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Space</kbd>.

The file is about 70 MB because it carries its own copy of .NET, so it runs on
any Windows 10 or 11 machine with nothing else installed. Copy it wherever you
like — it needs no other file next to it, and there is nothing to install.

The first time you run it, **Windows SmartScreen will warn you**, because the
file is not code-signed. Choose *More info* → *Run anyway*. That warning appears
for any unsigned program; it goes away if you sign the exe with a code-signing
certificate, or if you install through the Store package instead.

### Without installing anything

Push the repository to GitHub and the included workflow
(`.github/workflows/build.yml`) builds the exe for you. Open the **Actions** tab,
click the newest run, and download `KeyboardLanguageFix-x64` from **Artifacts**.
There is an `arm64` build there too, for Windows on ARM.

### Options

```powershell
.\build-exe.ps1                              # one .exe (default)
.\build-exe.ps1 -Mode Folder                 # .exe plus DLLs; starts faster
.\build-exe.ps1 -Architecture arm64          # Windows on ARM
.\build-exe.ps1 -Output C:\Tools\KLF         # somewhere else
```

To have it start with Windows, turn on *Start with Windows* in the app's
settings.

## How it works

Windows gives no application a way to read another app's selection directly.
So the app does what every tool of this kind does, in one motion you never see:

1. Releases the modifier keys you are still holding — otherwise the copy it
   sends would arrive as <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>C</kbd>.
2. Sends <kbd>Ctrl</kbd>+<kbd>C</kbd> and waits for the clipboard to change
   (it watches the clipboard sequence number rather than guessing with a sleep).
3. Converts the text through the layout table.
4. Pastes the result — or types it out, if you prefer to leave the clipboard
   untouched.
5. Puts your clipboard back.

The conversion tables are **generated from the browser extension's**
`src/core/layouts.js`, and 1028 recorded cases are replayed against the C#
engine in the test suite, so the two platforms cannot drift apart.

### Why there is no keyboard hook

The shortcut is registered with `RegisterHotKey`. The app is therefore only ever
told about the one combination it registered — it cannot see anything else you
type. A low-level keyboard hook would have been easier to write and would have
made the app indistinguishable from a keylogger. This matters both for you and
for Store certification.

## Known limits

- **Elevated windows.** Windows does not let a normal app send keystrokes to a
  program running as administrator (Task Manager, an elevated terminal). The
  Store forbids apps from requesting elevation, so this cannot be worked around
  in the Store build.
- **Apps that ignore Ctrl+C.** A few apps do not put a selection on the
  clipboard. Nothing is read, so nothing is changed.
- **Clipboard restore is text-only.** If your clipboard held an image or files
  when you pressed the shortcut, they are not restored. Switch the replacement
  method to "Type it out" to leave the clipboard alone entirely.
- **Slow apps.** Remote desktops and some Electron apps answer the copy late.
  Raise the timeout in Settings if conversions come back empty.

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download). The library
and its tests build and run on any platform; the app builds anywhere and runs on
Windows.

```powershell
dotnet test  windows\KeyboardLanguageFix.sln      # conversion + parity tests
dotnet build windows\KeyboardLanguageFix.sln -c Release
dotnet run   --project windows\src\KeyboardLanguageFix.App   # on Windows
```

For a portable build, prefer `build-exe.ps1` above; the equivalent raw command is:

```powershell
dotnet publish windows\src\KeyboardLanguageFix.App -c Release -r win-x64 `
  --self-contained true -o out
```

## Packaging for the Microsoft Store

`windows\build\build-msix.ps1` publishes the app and packs an MSIX. It needs
**Windows** with the .NET 8 SDK and the Windows SDK (for `makeappx.exe`).

### 1. Reserve the name

In [Partner Center](https://partner.microsoft.com/dashboard), create the app and
reserve its name. Under **Product identity** you get three values:

| Partner Center field | Goes into |
| --- | --- |
| Package/Identity/Name | `-Identity` |
| Package/Identity/Publisher | `-Publisher` |
| Package/Properties/PublisherDisplayName | `-PublisherDisplayName` |

### 2. Build the package

```powershell
cd windows\build
.\build-msix.ps1 -Identity "12345Contoso.KeyboardLanguageFix" `
                 -Publisher "CN=ABCD1234-1234-1234-1234-1234567890AB" `
                 -PublisherDisplayName "Contoso Ltd" `
                 -Version "1.0.0.0" `
                 -Architectures both
```

`-Architectures both` produces a `.msixbundle` covering x64 and ARM64.

The package is **unsigned on purpose** — Partner Center signs it with your
publisher certificate when you upload. Do not sign a submission yourself.

### 3. Test it locally first

```powershell
.\build-msix.ps1 -SignWithSelfSigned
```

This signs with a throwaway certificate so you can install the package on your
own machine. The script prints the one command needed to trust it.

### 4. Upload

Upload the `.msixbundle` (or `.msix`) to your submission and fill in the listing.

### What the Store will ask about

- **`runFullTrust`.** The package declares this restricted capability. Every
  Win32 desktop app in the Store does; it is what `Windows.FullTrustApplication`
  requires. If the submission form asks you to justify it: the app is a Win32
  desktop application, and it uses the capability for `RegisterHotKey` and
  `SendInput`.
- **Privacy policy.** Required if your listing declares any data collection.
  This app collects nothing, stores nothing off the device, and makes no network
  requests. Settings are a JSON file in `%LOCALAPPDATA%\KeyboardLanguageFix`.
- **Age rating.** The questionnaire will come out at the lowest rating: no ads,
  no user content, no network.
- **Version numbering.** The Store requires the fourth part of the version to be
  `0`; the script validates this before it builds.

### Checklist

- [ ] Identity, Publisher and PublisherDisplayName match Partner Center exactly
- [ ] Version's last part is `0`, and is higher than the last submission
- [ ] Package is unsigned
- [ ] `windows\build\test-build-msix.ps1` passes
- [ ] Installed and used the self-signed build on a real machine
- [ ] Screenshots taken (the Store wants at least one, 1366×768 or larger)

## Layout

```
windows/
  KeyboardLanguageFix.sln
  src/KeyboardLanguageFix.Core/     conversion engine — no Windows API surface
    Layouts.g.cs                    GENERATED from src/core/layouts.js
    Converter.cs                    mirrors src/core/converter.js
    AppSettings.cs                  settings model, JSON, self-repairing
  src/KeyboardLanguageFix.App/      WPF tray app
    Interop/NativeMethods.cs        the Win32 surface, and nothing more
    Interop/InputSimulator.cs       synthetic keystrokes
    Interop/HotkeyListener.cs       RegisterHotKey on a message-only window
    TextSwapper.cs                  the copy-convert-replace cycle
    SettingsWindow.xaml             settings UI with a live preview
  tests/KeyboardLanguageFix.Core.Tests/
    ParityTests.cs                  replays the JavaScript engine's output
    parity-fixture.json             GENERATED — 1028 recorded cases
  packaging/AppxManifest.xml        MSIX manifest
  packaging/Images/                 Store logos, generated
  build/build-exe.ps1               builds a plain KeyboardLanguageFix.exe
  build/build-exe.cmd               double-click wrapper for the above
  build/build-msix.ps1              publish + pack + optional test signing
  build/test-build-msix.ps1         packaging checks, runs anywhere
```

## Regenerating the shared tables

Editing `src/core/layouts.js` at the repository root changes both platforms.
After any edit:

```bash
node scripts/generate-cs-layouts.mjs
node scripts/generate-parity-fixture.mjs
```

`npm test` fails if you forget.
