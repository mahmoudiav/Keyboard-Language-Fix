; Keyboard Language Fix — installer
;
; Produces a single Setup.exe that a person can double-click. Built with NSIS
; (https://nsis.sourceforge.io), which cross-compiles, so windows/build/build-setup.sh
; makes the same installer on Windows, Linux or macOS.
;
; Deliberately kept to a per-user install: no administrator prompt, no UAC
; dialog, nothing written outside the user's own profile. That is the right
; shape for a small free utility, and it means anyone can install it on a
; managed work machine without asking IT.
;
; The payload is the framework-carrying folder build rather than a single-file
; exe: NSIS compresses the loose .NET assemblies far better (49 MB against
; 63 MB), and the app starts faster because nothing has to be unpacked at run
; time.
;
; Required defines (build-setup.sh passes them):
;   APP_VERSION   e.g. 1.0.0
;   SOURCE_DIR    the published folder, holding KeyboardLanguageFix.exe
;   OUTPUT_FILE   path of the Setup.exe to write

!ifndef APP_VERSION
  !error "APP_VERSION must be defined: makensis -DAPP_VERSION=1.0.0 ..."
!endif
!ifndef SOURCE_DIR
  !error "SOURCE_DIR must be defined"
!endif
!ifndef OUTPUT_FILE
  !error "OUTPUT_FILE must be defined"
!endif

!define APP_NAME       "Keyboard Language Fix"
!define APP_EXE        "KeyboardLanguageFix.exe"
!define APP_AUTHOR     "Mahmoud SATALEH"
!define APP_EMAIL      "mahmoudiav@icloud.com"
!define APP_REGKEY     "Software\Microsoft\Windows\CurrentVersion\Uninstall\KeyboardLanguageFix"
!define APP_RUNKEY     "Software\Microsoft\Windows\CurrentVersion\Run"
!define APP_RUNVALUE   "KeyboardLanguageFix"
; Where the app puts its "Fix keyboard language" right-click entry. Kept here
; so the uninstaller can take it away again; see windows/src/.../ShellMenu.cs.
!define APP_SHELLKEY   "Software\Classes\SystemFileAssociations"
!define APP_SHELLVERB  "KeyboardLanguageFix"

Name "${APP_NAME}"
OutFile "${OUTPUT_FILE}"
Unicode true

; Per-user install: HKCU only, and a folder the user always owns.
RequestExecutionLevel user
InstallDir "$LOCALAPPDATA\Programs\KeyboardLanguageFix"
InstallDirRegKey HKCU "Software\KeyboardLanguageFix" "InstallDir"

; LZMA in solid mode: the payload is a self-contained .NET app, so the
; difference between this and the default is tens of megabytes.
SetCompressor /SOLID lzma
SetCompressorDictSize 64

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName"     "${APP_NAME}"
VIAddVersionKey "FileDescription" "${APP_NAME} Setup"
VIAddVersionKey "FileVersion"     "${APP_VERSION}"
VIAddVersionKey "ProductVersion"  "${APP_VERSION}"
VIAddVersionKey "CompanyName"     "${APP_AUTHOR}"
VIAddVersionKey "LegalCopyright"  "Copyright (c) ${APP_AUTHOR}"

!include "MUI2.nsh"
!include "FileFunc.nsh"

!define MUI_ICON   "${SOURCE_DIR}\setup.ico"
!define MUI_UNICON "${SOURCE_DIR}\setup.ico"
!define MUI_ABORTWARNING

; --- Pages -------------------------------------------------------------------
; Only what a person actually needs to decide. No component tree, no install
; location prompt: this is a utility, not a suite.

!define MUI_WELCOMEPAGE_TITLE "${APP_NAME} ${APP_VERSION}"
!define MUI_WELCOMEPAGE_TEXT "Typed in the wrong keyboard language?$\r$\n$\r$\nSelect the text, press Ctrl+Shift+Space, and it is re-typed in the language you meant. Works in any Windows program.$\r$\n$\r$\nIdea and implementation: ${APP_AUTHOR}$\r$\n${APP_EMAIL}$\r$\n$\r$\nFree software — free to use and free to share.$\r$\n$\r$\nClick Install to continue."
!insertmacro MUI_PAGE_WELCOME

!insertmacro MUI_PAGE_INSTFILES

!define MUI_FINISHPAGE_TITLE "${APP_NAME} is installed"
!define MUI_FINISHPAGE_TEXT "The app has no window. Its icon sits in the notification area, beside the clock — click the ^ arrow if you do not see it.$\r$\n$\r$\nSelect some text anywhere and press Ctrl+Shift+Space.$\r$\n$\r$\nDouble-click the tray icon for settings."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Start ${APP_NAME} now"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; --- Install -----------------------------------------------------------------

Function .onInit
  ; A running copy would hold the exe open and the install would fail with a
  ; sharing violation. Close it first, quietly.
  Call CloseRunningApp
FunctionEnd

Function CloseRunningApp
  ; taskkill is present on every supported Windows; /F because the app has no
  ; window to send a close message to.
  nsExec::Exec 'taskkill /F /IM "${APP_EXE}"'
  Pop $0
  Sleep 500
FunctionEnd

Section "Install"
  SetOutPath "$INSTDIR"
  SetOverwrite on

  ; Everything the published folder holds: the exe, the .NET runtime it carries,
  ; plus LICENSE.txt and README.txt, which build-setup.sh stages alongside them.
  File /r "${SOURCE_DIR}\*.*"

  ; Staged only so the installer could use it as its own icon.
  Delete "$INSTDIR\setup.ico"

  WriteRegStr HKCU "Software\KeyboardLanguageFix" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\KeyboardLanguageFix" "Version" "${APP_VERSION}"

  ; Start with Windows. This is a tray utility that is useless when it is not
  ; running, and the settings window has a switch to turn it back off.
  WriteRegStr HKCU "${APP_RUNKEY}" "${APP_RUNVALUE}" '"$INSTDIR\${APP_EXE}"'

  CreateShortcut "$SMPROGRAMS\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; Appear in Settings > Apps, so the app can be removed the normal way.
  WriteRegStr   HKCU "${APP_REGKEY}" "DisplayName"     "${APP_NAME}"
  WriteRegStr   HKCU "${APP_REGKEY}" "DisplayVersion"  "${APP_VERSION}"
  WriteRegStr   HKCU "${APP_REGKEY}" "DisplayIcon"     "$INSTDIR\${APP_EXE}"
  WriteRegStr   HKCU "${APP_REGKEY}" "Publisher"       "${APP_AUTHOR}"
  WriteRegStr   HKCU "${APP_REGKEY}" "Contact"         "${APP_EMAIL}"
  WriteRegStr   HKCU "${APP_REGKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr   HKCU "${APP_REGKEY}" "InstallLocation" "$INSTDIR"
  WriteRegDWORD HKCU "${APP_REGKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${APP_REGKEY}" "NoRepair" 1

  ; Report the installed size in Settings > Apps rather than leaving it blank.
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "${APP_REGKEY}" "EstimatedSize" "$0"
SectionEnd

; --- Uninstall ---------------------------------------------------------------

Section "Uninstall"
  nsExec::Exec 'taskkill /F /IM "${APP_EXE}"'
  Pop $0
  Sleep 500

  ; A recursive delete deserves a guard. Only ever remove a folder that really
  ; does hold this app, so a corrupted registry entry cannot point the
  ; uninstaller at something else.
  IfFileExists "$INSTDIR\${APP_EXE}" proceed skip
  proceed:
    RMDir /r "$INSTDIR"
  skip:

  Delete "$SMPROGRAMS\${APP_NAME}.lnk"

  DeleteRegValue HKCU "${APP_RUNKEY}" "${APP_RUNVALUE}"
  DeleteRegKey HKCU "${APP_REGKEY}"
  DeleteRegKey HKCU "Software\KeyboardLanguageFix"

  ; The right-click entry the app registers for itself on first run. The app
  ; would normally take it away again, but by now it is gone, so the uninstaller
  ; does it. HKCU only: this is the user's own copy of the association and
  ; nobody else's.
  DeleteRegKey HKCU "${APP_SHELLKEY}\text\shell\${APP_SHELLVERB}"
  DeleteRegKey HKCU "${APP_SHELLKEY}\.md\shell\${APP_SHELLVERB}"
  DeleteRegKey HKCU "${APP_SHELLKEY}\.csv\shell\${APP_SHELLVERB}"
  DeleteRegKey HKCU "${APP_SHELLKEY}\.json\shell\${APP_SHELLVERB}"
  DeleteRegKey HKCU "${APP_SHELLKEY}\.srt\shell\${APP_SHELLVERB}"

  ; The user's settings are left alone on purpose: reinstalling should not lose
  ; a customised shortcut or layout. They live in
  ; %LOCALAPPDATA%\KeyboardLanguageFix and are a single small JSON file.
SectionEnd
