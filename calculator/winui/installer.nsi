; Calc Pro — WinUI 3 installer (NSIS)
; Packages the self-contained folder publish (installer-app\) into a setup .exe.

Unicode true

!define APP_NAME    "Calc Pro"
!define APP_VERSION "1.0.0"
!define APP_PUBLISHER "Alexey"
!define APP_EXE     "CalcPro.exe"
!define APP_REGKEY  "Software\Microsoft\Windows\CurrentVersion\Uninstall\CalcPro"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "dist\Calc Pro Setup ${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\CalcPro"
InstallDirRegKey HKCU "Software\CalcPro" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
Icon "Assets\app.ico"
UninstallIcon "Assets\app.ico"

!include "MUI2.nsh"

!define MUI_ICON   "Assets\app.ico"
!define MUI_UNICON "Assets\app.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Запустить ${APP_NAME}"

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Russian"
!insertmacro MUI_LANGUAGE "English"

Section "Install"
  SetOutPath "$INSTDIR"
  File /r "installer-app\*.*"

  ; Shortcuts
  CreateShortCut "$SMPROGRAMS\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortCut "$DESKTOP\${APP_NAME}.lnk"    "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0

  ; Uninstaller
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\CalcPro" "InstallDir" "$INSTDIR"

  ; Add/Remove Programs entry
  WriteRegStr   HKCU "${APP_REGKEY}" "DisplayName"     "${APP_NAME}"
  WriteRegStr   HKCU "${APP_REGKEY}" "DisplayVersion"  "${APP_VERSION}"
  WriteRegStr   HKCU "${APP_REGKEY}" "Publisher"       "${APP_PUBLISHER}"
  WriteRegStr   HKCU "${APP_REGKEY}" "DisplayIcon"     "$INSTDIR\${APP_EXE}"
  WriteRegStr   HKCU "${APP_REGKEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr   HKCU "${APP_REGKEY}" "InstallLocation" "$INSTDIR"
  WriteRegDWORD HKCU "${APP_REGKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${APP_REGKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Delete "$SMPROGRAMS\${APP_NAME}.lnk"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "${APP_REGKEY}"
  DeleteRegKey HKCU "Software\CalcPro"
SectionEnd
