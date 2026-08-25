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

Built-in layouts: Arabic, Persian, Russian, Hebrew, Greek.


WHAT IT DOES NOT DO
-------------------

  * It does not work in windows running as administrator, such as Task Manager.
    Windows does not allow a normal program to send keystrokes to those.

  * It cannot read a selection from the few programs that ignore Ctrl+C.

  * If your clipboard held a picture or files when you pressed the shortcut,
    those are not restored afterwards. Only text is. You can switch to
    "Type it out" in Settings to leave the clipboard untouched entirely.


PRIVACY
-------

Nothing you type or select is sent anywhere. There is no network connection, no
account, and no telemetry of any kind. Your settings are a single small file in
%LOCALAPPDATA%\KeyboardLanguageFix.

The app uses one registered keyboard shortcut. It is not able to see anything
else you type, by design.


REMOVING IT
-----------

Settings > Apps > Installed apps > Keyboard Language Fix > Uninstall.

Your preferences are left in place so a reinstall keeps them. To remove those
too, delete the folder %LOCALAPPDATA%\KeyboardLanguageFix.
