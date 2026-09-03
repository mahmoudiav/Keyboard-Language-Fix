Keyboard Language Fix
=====================

Typed in the wrong keyboard language? Select the text, press Ctrl+Shift+Space,
and it is re-typed in the language you meant. No deleting, no retyping.

Idea and implementation: Mahmoud SATALEH
Contact: mahmoudiav@icloud.com

Free software - free to use and free to share.
Released under the MIT License; see LICENSE.txt.


HOW TO USE IT
-------------

The app has no window. After it starts, its icon sits in the notification area
beside the clock - click the ^ arrow if you do not see it.

  1. Select some text in any program.
  2. Press Ctrl+Shift+Space.
  3. The text is replaced.

It works in both directions and decides which way round from the text itself:

  hgsghl ugd;l   becomes   the Arabic that was meant
  ghbdtn         becomes   the Russian that was meant

Ctrl+Z undoes the change like any other edit.

Double-click the tray icon to open Settings, where you can change the shortcut,
choose your second keyboard layout, and turn "start with Windows" on or off.

Built-in layouts: Arabic, Persian, Russian, Hebrew, Greek, Spanish (Spain).

Spanish is the one that also fixes accents. Its letters sit exactly where
English keeps them, so what comes out wrong is only the punctuation: a
semicolon where you wanted n-with-tilde, and an apostrophe followed by a vowel
where you wanted an accented one. Both are put right, and so is the other way
round.


A WHOLE FILE AT ONCE
--------------------

Right-click a text file in File Explorer and choose "Fix keyboard language".
The file opens with its text before and after the conversion, and nothing is
written until you press Save - the version you had is kept beside it, with .bak
on the end.

On Windows 11 the entry is under "Show more options", where Windows puts every
entry that does not come from a Store app. It can be turned off in Settings.

Selected text inside a program is what the shortcut is for. No program can add
a command to the menu another program shows on selected text; Windows has no
mechanism for it.


WHAT IT DOES NOT DO
-------------------

  * It does not work in windows running as administrator, such as Task Manager.
    Windows does not allow a normal program to send keystrokes to those.

  * It cannot read a selection from the few programs that ignore Ctrl+C.

  * If your clipboard held a picture or files when you pressed the shortcut,
    those are not restored afterwards. Only text is. You can switch to
    "Type it out" in Settings to leave the clipboard untouched entirely.

  * The right-click entry opens plain text files saved as UTF-8 or UTF-16,
    which is what Notepad and every editor on a current Windows write. A file
    in an older Windows code page is refused rather than guessed at, because a
    wrong guess would destroy the file it was asked to fix. Open such a file in
    Notepad, save it again as UTF-8, and it will work.


PRIVACY
-------

Nothing you type or select is sent anywhere. There is no network connection, no
account, and no telemetry of any kind. Your settings are a single small file in
%LOCALAPPDATA%\KeyboardLanguageFix.

The app uses one registered keyboard shortcut. It is not able to see anything
else you type, by design.

The right-click entry is one registry key under HKEY_CURRENT_USER, naming the
command Explorer should run. It is written to your own account only, and the
uninstaller removes it.


REMOVING IT
-----------

Settings > Apps > Installed apps > Keyboard Language Fix > Uninstall.

Your preferences are left in place so a reinstall keeps them. To remove those
too, delete the folder %LOCALAPPDATA%\KeyboardLanguageFix.
