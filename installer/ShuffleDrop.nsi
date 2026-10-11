Unicode True
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

!define APP_VERSION "0.3.0"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShuffleDrop"
Name "ShuffleDrop"
OutFile "..\releases\ShuffleDrop-Setup-0.3.0.exe"
InstallDir "$LOCALAPPDATA\Programs\ShuffleDrop"
InstallDirRegKey HKCU "${UNINSTALL_KEY}" "InstallLocation"
RequestExecutionLevel user
SetCompressor /SOLID zlib
SetDatablockOptimize on
ShowInstDetails show
ShowUninstDetails show
VIProductVersion "0.3.0.0"
VIAddVersionKey "ProductName" "ShuffleDrop"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "FileDescription" "ShuffleDrop diegimas"
VIAddVersionKey "LegalCopyright" "ShuffleDrop contributors (MIT)"

!define MUI_ICON "..\assets\ShuffleDrop.ico"
!define MUI_UNICON "..\assets\ShuffleDrop.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "ShuffleDrop ${APP_VERSION}"
!define MUI_WELCOMEPAGE_TEXT "Muzikos tvarkyklė iPod Shuffle grotuvui.$\r$\n$\r$\nPridėk MP3, peržiūrėk esamas dainas ir klausykis jų kompiuteryje be iTunes.$\r$\n$\r$\nTai bandomoji versija. Baterijos procento rodymas ir naujų VoiceOver įrašų kūrimas nepalaikomi.$\r$\n$\r$\nDiegiama tik tavo Windows paskyrai."
!define MUI_FINISHPAGE_RUN "$INSTDIR\ShuffleDrop.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Paleisti ShuffleDrop"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "Lithuanian"

Var NoIcons

Function .onInit
  SetShellVarContext current
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/NOICONS" $1
  ${IfNot} ${Errors}
    StrCpy $NoIcons "1"
  ${EndIf}
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 < 461808
    MessageBox MB_ICONSTOP|MB_OK "Reikalinga .NET Framework 4.7.2 arba naujesnė versija. Įdiek ją iš Microsoft svetainės ir paleisk diegiklį dar kartą." /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
FunctionEnd

Section "ShuffleDrop (būtina)" MainSection
  SectionIn RO
  SetOutPath "$INSTDIR"
  File "..\dist\ShuffleDrop.exe"
  File "..\dist\ShuffleDrop.exe.config"
  File "..\dist\ShuffleDrop.ico"
  File "..\dist\TagLibSharp.dll"
  File "..\dist\Krypton.Toolkit.dll"
  File "..\dist\NAudio.Core.dll"
  File "..\dist\NAudio.Wasapi.dll"
  File "..\dist\LICENSE"
  File "..\README.md"
  File "..\SOURCES.md"
  File /oname=PRADĖK-ČIA.txt "Pradek-cia.txt"
  SetOutPath "$INSTDIR\third-party"
  File "..\third-party\*.*"
  SetOutPath "$INSTDIR\docs"
  File "..\docs\README.lt.md"
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "ShuffleDrop"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "ShuffleDrop"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\ShuffleDrop.exe,0"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "Shortcuts" 0
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" $0
  ${If} $NoIcons != "1"
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "Shortcuts" 1
    CreateDirectory "$SMPROGRAMS\ShuffleDrop"
    CreateShortcut "$SMPROGRAMS\ShuffleDrop\ShuffleDrop.lnk" "$INSTDIR\ShuffleDrop.exe"
    CreateShortcut "$SMPROGRAMS\ShuffleDrop\Pagalba.lnk" "$INSTDIR\PRADĖK-ČIA.txt"
  ${EndIf}
SectionEnd

Section "Nuoroda darbalaukyje" DesktopSection
  ${If} $NoIcons != "1"
    CreateShortcut "$DESKTOP\ShuffleDrop.lnk" "$INSTDIR\ShuffleDrop.exe"
  ${EndIf}
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
!insertmacro MUI_DESCRIPTION_TEXT ${MainSection} "Programa, visos reikalingos bibliotekos ir pašalinimo įrankis."
!insertmacro MUI_DESCRIPTION_TEXT ${DesktopSection} "Sukurti ShuffleDrop nuorodą darbalaukyje."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  SetShellVarContext current
  ; Remove this installation's registered shortcuts and known package files only.
  ReadRegStr $0 HKCU "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $0 == $INSTDIR
    ReadRegDWORD $1 HKCU "${UNINSTALL_KEY}" "Shortcuts"
    ${If} $1 == 1
      Delete "$DESKTOP\ShuffleDrop.lnk"
      Delete "$SMPROGRAMS\ShuffleDrop\ShuffleDrop.lnk"
      Delete "$SMPROGRAMS\ShuffleDrop\Pagalba.lnk"
      RMDir "$SMPROGRAMS\ShuffleDrop"
    ${EndIf}
  ${EndIf}
  Delete "$INSTDIR\ShuffleDrop.exe"
  Delete "$INSTDIR\ShuffleDrop.exe.config"
  Delete "$INSTDIR\ShuffleDrop.ico"
  Delete "$INSTDIR\TagLibSharp.dll"
  Delete "$INSTDIR\Krypton.Toolkit.dll"
  Delete "$INSTDIR\NAudio.Core.dll"
  Delete "$INSTDIR\NAudio.Wasapi.dll"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\README.md"
  Delete "$INSTDIR\SOURCES.md"
  Delete "$INSTDIR\PRADĖK-ČIA.txt"
  Delete "$INSTDIR\third-party\Krypton-LICENSE.txt"
  Delete "$INSTDIR\third-party\NAudio-LICENSE.txt"
  Delete "$INSTDIR\third-party\TagLibSharp-LICENSE.txt"
  Delete "$INSTDIR\third-party\taglib-sharp-2.3.0-source.zip"
  Delete "$INSTDIR\third-party\NOTICE.txt"
  Delete "$INSTDIR\third-party\NSIS-LICENSE.txt"
  RMDir "$INSTDIR\third-party"
  Delete "$INSTDIR\docs\README.lt.md"
  RMDir "$INSTDIR\docs"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  ReadRegStr $0 HKCU "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $0 == $INSTDIR
    DeleteRegKey HKCU "${UNINSTALL_KEY}"
  ${EndIf}
  ; User settings and music recovery backups in LOCALAPPDATA\ShuffleDrop stay intact.
SectionEnd
