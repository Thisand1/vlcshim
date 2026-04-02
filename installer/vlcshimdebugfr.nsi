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
LicenseForceSelection checkbox

!insertmacro MUI_PAGE_WELCOME
!define MUI_PAGE_HEADER_TEXT "Read the README"
!define MUI_PAGE_HEADER_SUBTEXT "Review what this shim does and how it is expected to be used."
!define MUI_LICENSEPAGE_TEXT_TOP "You must read the project README before continuing."
!define MUI_LICENSEPAGE_CHECKBOX "I have read the README"
!insertmacro MUI_PAGE_LICENSE "..\README.md"

!define MUI_PAGE_HEADER_TEXT "Read the Code of Conduct"
!define MUI_PAGE_HEADER_SUBTEXT "Review the community rules before installing or interacting with the project."
!define MUI_LICENSEPAGE_TEXT_TOP "You must read the Code of Conduct before continuing."
!define MUI_LICENSEPAGE_CHECKBOX "I have read the Code of Conduct"
!insertmacro MUI_PAGE_LICENSE "..\CODEOFCONDUCT.md"

!define MUI_PAGE_HEADER_TEXT "Read the Contributing Guide"
!define MUI_PAGE_HEADER_SUBTEXT "Review the repository rules and contribution expectations."
!define MUI_LICENSEPAGE_TEXT_TOP "You must read the Contributing guide before continuing."
!define MUI_LICENSEPAGE_CHECKBOX "I have read the Contributing guide"
!insertmacro MUI_PAGE_LICENSE "..\CONTRIBUTING.md"

!define MUI_PAGE_HEADER_TEXT "Read the AGPLv3 License"
!define MUI_PAGE_HEADER_SUBTEXT "Review the license terms that apply to this software."
!define MUI_LICENSEPAGE_TEXT_TOP "You must read the GNU Affero General Public License v3 before continuing."
!define MUI_LICENSEPAGE_CHECKBOX "I have read the AGPLv3 license"
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
  SetOutPath "$INSTDIR\docs"
  File "..\README.md"
  File "..\CODEOFCONDUCT.md"
  File "..\CONTRIBUTING.md"
  File "..\LICENSE"
  SetOutPath "$INSTDIR"

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
