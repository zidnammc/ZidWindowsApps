; ZidWindowsApps - installer wizard (NSIS 3, Modern UI 2)
; Build (framework-dependent, ringan):
;   makensis -DBIN_DIR=<folder berisi ZidWindowsApps.exe/.dll/.runtimeconfig.json> -DOUT_FILE=<hasil.exe> ZidWindowsApps.nsi
; Build (self-contained, runtime ikut dibundel; hasil dari `dotnet publish -r win-x64 --self-contained`):
;   makensis -DSELF_CONTAINED -DBIN_DIR=<folder publish> -DOUT_FILE=<hasil.exe> ZidWindowsApps.nsi
Unicode true
SetCompressor /SOLID lzma
RequestExecutionLevel admin
ManifestDPIAware true

!ifndef BIN_DIR
  !error "Definisikan BIN_DIR (folder hasil build)."
!endif
!ifndef OUT_FILE
  !define OUT_FILE "ZidWindowsApps-Setup.exe"
!endif
!ifndef ICON_FILE
  !define ICON_FILE "${__FILEDIR__}\..\launcher\app.ico"
!endif

!define APP_NAME      "ZidWindowsApps"
!define APP_VERSION   "1.1.0"
!define APP_PUBLISHER "Zid"
!define APP_EXE       "ZidWindowsApps.exe"
!define UNINST_KEY    "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
!define TASK_NAME     "ZidWindowsApps\Scheduled Safe Cleanup"
!define DOTNET_URL    "https://dotnet.microsoft.com/download/dotnet"

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"
!insertmacro GetFileName
!insertmacro un.GetFileName

Name "${APP_NAME} ${APP_VERSION}"
OutFile "${OUT_FILE}"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "Software\${APP_NAME}" "InstallDir"
BrandingText "${APP_NAME} ${APP_VERSION}"

VIProductVersion "1.1.0.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey "FileDescription" "Installer ${APP_NAME}"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "LegalCopyright" "${APP_PUBLISHER}"

; ------------------------------------------------------------------ tampilan wizard
; The icon file is not shipped in the public repo snapshot; use the default
; NSIS icon when launcher\app.ico is absent.
!if /FileExists "${ICON_FILE}"
!define MUI_ICON   "${ICON_FILE}"
!define MUI_UNICON "${ICON_FILE}"
!endif
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "Selamat datang di ${APP_NAME}"
!define MUI_WELCOMEPAGE_TEXT "Wizard ini akan memasang ${APP_NAME} ${APP_VERSION} di komputer Anda.$\r$\n$\r$\nPusat perawatan Windows: proses, auto-start, uninstaller, pembersihan, spesifikasi, dan troubleshooting.$\r$\n$\r$\nTutup ${APP_NAME} bila sedang berjalan, lalu klik Berikutnya."
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "Jalankan ${APP_NAME}"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApp

!insertmacro MUI_PAGE_WELCOME
!define MUI_PAGE_CUSTOMFUNCTION_LEAVE DirLeave
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "Indonesian"

; Pastikan aplikasi selalu berada di folder bernama "${APP_NAME}" (uninstaller self-contained menghapus folder ini secara rekursif).
Function DirLeave
  ${GetFileName} "$INSTDIR" $R0
  ${If} $R0 != "${APP_NAME}"
    StrCpy $INSTDIR "$INSTDIR\${APP_NAME}"
  ${EndIf}
FunctionEnd

; ------------------------------------------------------------------ pemeriksaan .NET Desktop Runtime >= 8
Var DotNetFound

Function CheckDotNetKey
  ; $0 = nama key arsitektur (x64 / arm64)
  StrCpy $1 0
  ${Do}
    EnumRegValue $2 HKLM "SOFTWARE\dotnet\Setup\InstalledVersions\$0\sharedfx\Microsoft.WindowsDesktop.App" $1
    ${If} $2 == ""
      ${Break}
    ${EndIf}
    IntOp $1 $1 + 1
    StrCpy $3 $2 1 1          ; karakter ke-2: "." berarti versi 1 digit (mis. 8.0.11)
    ${If} $3 == "."
      StrCpy $4 $2 1
    ${Else}
      StrCpy $4 $2 2
    ${EndIf}
    ${If} $4 >= 8
      StrCpy $DotNetFound 1
      ${Break}
    ${EndIf}
  ${Loop}
FunctionEnd

Function .onInit
  SetRegView 64
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "${APP_NAME} membutuhkan Windows 64-bit."
    Abort
  ${EndIf}

!ifdef SELF_CONTAINED
  StrCpy $DotNetFound 1   ; runtime sudah ikut dibundel
!else
  StrCpy $DotNetFound 0
  StrCpy $0 "x64"
  Call CheckDotNetKey
  ${If} $DotNetFound == 0
    StrCpy $0 "arm64"
    Call CheckDotNetKey
  ${EndIf}
!endif

  ${If} $DotNetFound == 0
    IfSilent +4
    MessageBox MB_YESNOCANCEL|MB_ICONEXCLAMATION "${APP_NAME} membutuhkan .NET Desktop Runtime 8 atau lebih baru, yang belum terpasang.$\r$\n$\r$\nYa      = buka halaman unduhan, lalu lanjutkan instalasi$\r$\nTidak   = lanjutkan instalasi saja (pasang runtime nanti)$\r$\nBatal   = keluar" IDYES openDownload IDNO continueInstall
    Abort
    openDownload:
      ExecShell "open" "${DOTNET_URL}"
    continueInstall:
  ${EndIf}
FunctionEnd

; ------------------------------------------------------------------ instalasi
Section "${APP_NAME} (wajib)" SecMain
  SectionIn RO
  SetRegView 64
  SetOutPath "$INSTDIR"

  ; tutup instance lama agar file tidak terkunci (upgrade)
  nsExec::Exec 'taskkill /F /IM "${APP_EXE}"'
  Pop $0
  Sleep 400

!ifdef SELF_CONTAINED
  File /r /x "*.pdb" "${BIN_DIR}\*.*"
!else
  File "${BIN_DIR}\ZidWindowsApps.exe"
  File "${BIN_DIR}\ZidWindowsApps.dll"
  File "${BIN_DIR}\ZidWindowsApps.runtimeconfig.json"
!endif
!if /FileExists "${ICON_FILE}"
  File "${ICON_FILE}"
!endif
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"

  WriteRegStr HKLM "Software\${APP_NAME}" "InstallDir" "$INSTDIR"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr   HKLM "${UNINST_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr   HKLM "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr   HKLM "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr   HKLM "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1
  !ifdef SELF_CONTAINED
  WriteRegDWORD HKLM "${UNINST_KEY}" "EstimatedSize" 180000
!else
  WriteRegDWORD HKLM "${UNINST_KEY}" "EstimatedSize" 700
!endif
SectionEnd

Section "Pintasan di Desktop" SecDesktop
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
SectionEnd

LangString DESC_Main    ${LANG_INDONESIAN} "File program ${APP_NAME} dan pintasan Start Menu."
LangString DESC_Desktop ${LANG_INDONESIAN} "Tambahkan ikon ${APP_NAME} di Desktop."
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} $(DESC_Main)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} $(DESC_Desktop)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; Jalankan tanpa hak admin installer (supaya aplikasi berjalan sebagai pengguna biasa seperti pintasan).
Function LaunchApp
  Exec '"$WINDIR\explorer.exe" "$INSTDIR\${APP_EXE}"'
FunctionEnd

; ------------------------------------------------------------------ uninstall
Section "Uninstall"
  SetRegView 64
  nsExec::Exec 'taskkill /F /IM "${APP_EXE}"'
  Pop $0
  Sleep 400

  ; hapus jadwal pembersihan yang dibuat aplikasi
  nsExec::Exec 'schtasks /Delete /TN "${TASK_NAME}" /F'
  Pop $0

!ifdef SELF_CONTAINED
  ; Ratusan file runtime: hapus rekursif, tetapi hanya bila folder memang bernama "${APP_NAME}".
  ${un.GetFileName} "$INSTDIR" $R0
  ${If} $R0 == "${APP_NAME}"
    SetOutPath "$TEMP"
    RMDir /r "$INSTDIR"
  ${EndIf}
!else
  Delete "$INSTDIR\ZidWindowsApps.exe"
  Delete "$INSTDIR\ZidWindowsApps.dll"
  Delete "$INSTDIR\ZidWindowsApps.runtimeconfig.json"
  Delete "$INSTDIR\app.ico"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
!endif

  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  Delete "$DESKTOP\${APP_NAME}.lnk"

  DeleteRegKey HKLM "${UNINST_KEY}"
  DeleteRegKey HKLM "Software\${APP_NAME}"

  IfSilent skipData
  MessageBox MB_YESNO|MB_ICONQUESTION|MB_DEFBUTTON2 "Hapus juga pengaturan, log, dan cadangan registry ${APP_NAME} milik akun ini?$\r$\n$\r$\n(Pilih Tidak bila Anda mungkin memasang ulang atau masih membutuhkan cadangan .reg.)" IDNO skipData
    RMDir /r "$APPDATA\${APP_NAME}"
    RMDir /r "$LOCALAPPDATA\${APP_NAME}"
  skipData:
SectionEnd
