Unicode true
SetCompressor /SOLID lzma

!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"

!ifndef VERSION
!define VERSION "dev"
!endif

!ifndef PUBLISH_DIR
!error "PUBLISH_DIR was not defined. Run installer\\build-installer.bat or pass /DPUBLISH_DIR=..."
!endif

!ifndef OUT_DIR
!define OUT_DIR "."
!endif

!define APP_NAME "VLC Shim"
!define COMPANY_NAME "IsThisThisandFr"
!define INSTALL_DIR "$ProgramFiles64\VLC Shim"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\VLCShim"

Name "${APP_NAME}"
OutFile "${OUT_DIR}\vlcshimdebugfr-${VERSION}-setup.exe"
InstallDir "${INSTALL_DIR}"
InstallDirRegKey HKLM "${UNINSTALL_KEY}" "InstallLocation"
RequestExecutionLevel admin

!define MUI_ABORTWARNING
!define MUI_ICON "..\assets\vlc.ico"
!define MUI_UNICON "..\assets\vlc.ico"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

Section "Install"
  SetRegView 64
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\VLC Shim"
  CreateShortcut "$SMPROGRAMS\VLC Shim\VLC Shim.lnk" "$INSTDIR\vlcshimdebugfr.exe" "" "$INSTDIR\assets\vlc.ico"
  CreateShortcut "$SMPROGRAMS\VLC Shim\Uninstall VLC Shim.lnk" "$INSTDIR\Uninstall.exe"

  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "${COMPANY_NAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\assets\vlc.ico"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "QuietUninstallString" "$INSTDIR\Uninstall.exe /S"
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  SetRegView 64
  Delete "$DESKTOP\VLC Shim.lnk"
  Delete "$SMPROGRAMS\VLC Shim\VLC Shim.lnk"
  Delete "$SMPROGRAMS\VLC Shim\Uninstall VLC Shim.lnk"
  RMDir "$SMPROGRAMS\VLC Shim"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
SectionEnd
