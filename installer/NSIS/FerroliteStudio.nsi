; FerroLite Studio - NSIS installer (NSIS 3.x, Unicode)
; =====================================================
; Installs:
;   1) The APP    - self-contained win-x64 publish output (installer\bin)
;   2) DRIVERS    - OPTIONAL virtual-gamepad drivers (ViGEmBus / HidHide / vJoy),
;                   DOWNLOADED from the vendors' official GitHub releases
;                   during install and installed silently. Skipped when
;                   already present. Silent installs (/S) skip them unless
;                   FERROLITE_INSTALL_DRIVERS=1 is set.
;   3) README     - REQUIRED_DRIVERS.txt explaining the driver step
;   4) SOURCE     - the full source tree, ALL COMMENTS REMOVED (src-clean)
;
; Prerequisites (run installer\build-staging.ps1 first):
;   installer\bin\          - dotnet publish output with Ferrolite.exe
;   installer\src-clean\    - comment-free source tree
;   installer\deps\REQUIRED_DRIVERS.txt
;   ..\..\LICENSE.txt
;
; Build:
;   powershell -ExecutionPolicy Bypass -File installer\build-staging.ps1
;   "%ProgramFiles(x86)%\NSIS\makensis.exe" installer\NSIS\FerroLiteStudio.nsi

Unicode true

!include "MUI2.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"

; ---------------- Metadata ----------------
!define APP_NAME       "FerroLite Studio"
!define APP_VERSION    "1.0.0"          ; internal only - not shown to users
!define APP_PUBLISHER  "FerroLite"
!define APP_EXE        "Ferrolite.exe"
!define APP_PROCNAME   "Ferrolite"        ; image name without .exe, for tasklist/taskkill
!define REG_UNINST     "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
!define REG_COMPANY    "Software\FerroLite\${APP_NAME}"

; ---------------- Driver downloads (official vendor GitHub releases) ----------------
!define VIGEMBUS_URL  "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe"
!define VIGEMBUS_VER  "1.22.0"
!define HIDHIDE_URL   "https://github.com/nefarius/HidHide/releases/download/v1.5.230.0/HidHide_1.5.230_x64.exe"
!define HIDHIDE_VER   "1.5.230"
!define VJOY_URL      "https://github.com/jshafer817/vJoy/releases/download/v2.1.9.1/vJoySetup.exe"
!define VJOY_VER      "2.1.9.1"

; Installer icon (staged next to this script by build-staging or present in repo)
!define ICON_SRC "..\..\Ferrolite.UI\Resources\ferrolite.ico"

; License page input. The canonical license is at the repo ROOT; build-staging.ps1
; copies it to installer\LICENSE.txt (one level up from this script). The old
; "..\..\Documents" path no longer exists, so makensis failed on a missing file.
!define LICENSE_SRC "..\LICENSE.txt"

Name "${APP_NAME}"
OutFile "FerroLiteStudio_win-x64.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "${REG_COMPANY}" "InstallDir"
RequestExecutionLevel admin
ShowInstDetails show
ShowUnInstDetails show
SetCompressor /SOLID lzma

; ---------------- UI ----------------
!define MUI_ICON   "${ICON_SRC}"
!define MUI_UNICON "${ICON_SRC}"

; Tell users up front that the optional drivers are downloaded, not bundled.
; NOTE: NSIS needs $\r$\n for real newlines in this text - literal "\r\n" prints as text.
!define MUI_WELCOMEPAGE_TEXT "This wizard will install ${APP_NAME} on your computer.$\r$\n$\r$\nOptional virtual-gamepad drivers (ViGEmBus / HidHide / vJoy) are not bundled: if you select them on the Components page, setup will DOWNLOAD them from the vendors' official GitHub releases and install them silently. Internet access is required for that step.$\r$\n$\r$\nClick Next to continue."

!insertmacro MUI_PAGE_WELCOME
; Staged next to this script by installer\build-staging.ps1 (the license lives
; at the REPO ROOT; the old path pointed at a "Documents\" copy that no longer
; exists, which made makensis fail on a missing file).
!insertmacro MUI_PAGE_LICENSE "${LICENSE_SRC}"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${APP_NAME}"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_RESERVEFILE_LANGDLL

; ================= Section: App =================

; DownloadFile <url> <dest-file> - downloads via PowerShell (present on every
; Windows 10+ machine; NSISdl cannot do HTTPS against modern servers and no
; third-party download plugin is required).
!macro DownloadFile URL DEST
    DetailPrint "Downloading: ${URL}"
    nsExec::ExecToLog "powershell -NoProfile -ExecutionPolicy Bypass -Command $\"[Net.ServicePointManager]::SecurityProtocol='Tls12'; (New-Object System.Net.WebClient).DownloadFile('${URL}','${DEST}')$\""
    Pop $0
!macroend

; Returns in $0: 0 = Authenticode signature Valid AND signer matches EXPECTED_CN,
;                 non-zero otherwise. Deletes nothing - caller decides.
!macro VerifySignature FILE EXPECTED_CN
    nsExec::ExecToLog "powershell -NoProfile -ExecutionPolicy Bypass -Command $\"$$s=(Get-AuthenticodeSignature '${FILE}'); if($$s.Status -eq 'Valid' -and $$s.SignerCertificate.Subject -like '*${EXPECTED_CN}*'){exit 0}else{exit 1}$\""
    Pop $0
!macroend

; Returns in $0: 1 = driver already installed, 0 = not found
!macro DriverInstalled SERVICEKEY UNINSTALLKEY UNINSTALLKEY_WOW64
    StrCpy $0 "0"
    ClearErrors
    ReadRegStr $1 HKLM "SYSTEM\CurrentControlSet\Services\${SERVICEKEY}" "ImagePath"
    ${IfNot} ${Errors}
        StrCpy $0 "1"
    ${EndIf}
    ClearErrors
    ReadRegStr $1 HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${UNINSTALLKEY}" "DisplayName"
    ${IfNot} ${Errors}
        StrCpy $0 "1"
    ${EndIf}
    ClearErrors
    ReadRegStr $1 HKLM "SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\${UNINSTALLKEY_WOW64}" "DisplayName"
    ${IfNot} ${Errors}
        StrCpy $0 "1"
    ${EndIf}
!macroend

; Same as DriverInstalled but also checks the HID-CR/Dependencies key used by
; HidHide's Wix installer (HKCR\Installer\Dependencies\NSS.Drivers.HidHide.x64).
; Returns in $0: 1 = already installed, 0 = not found
!macro DriverInstalledDep SERVICEKEY DEPKEY UNINSTALLKEY
    !insertmacro DriverInstalled "${SERVICEKEY}" "${UNINSTALLKEY}" "${UNINSTALLKEY}"
    ${If} $0 == "0"
        ClearErrors
        ReadRegStr $1 HKCR "Installer\Dependencies\${DEPKEY}\Version" ""
        ${IfNot} ${Errors}
            StrCpy $0 "1"
        ${EndIf}
    ${EndIf}
!macroend

; ---------------- Helpers ----------------
; ================= Section: App =================
Function un.RemoveLocalData
    ; Deliberately opt-in: recordings and settings are USER DATA. Never delete
    ; them as a side effect of uninstalling the program.
    ${IfNot} ${Silent}
        ; NOTE: breaks use $\r$\n (each escape terminated by another $), and the
        ; data path is literal %LOCALAPPDATA% text. Two NSIS traps are avoided:
        ; "$nFollowedByLetters" parses as ONE variable name (warning 6000), and
        ; a $ variable directly followed by a backslash lets that backslash
        ; escape the next letter.
        MessageBox MB_YESNO|MB_ICONQUESTION "Also delete your FerroLite data?$\r$\n$\r$\nThis removes settings, saved recordings, and the device-recovery state files in:%LOCALAPPDATA%\Ferrolite$\r$\n$\r$\nChoose No to keep them (recommended)." IDYES remove IDNO keep
        Goto keep
        remove:
            RMDir /r "$LOCALAPPDATA\Ferrolite"
            DetailPrint "Local data removed: $LOCALAPPDATA\Ferrolite"
            Goto keep
        keep:
    ${EndIf}
FunctionEnd

Function un.RestoreBeforeRemoving
    ; Undo any leftover device hiding BEFORE the app (and the ownership records
    ; recovery depends on) is deleted. Without this, uninstalling while the
    ; controller chain is armed leaves the physical pad hidden from EVERY other
    ; application with no recovery path left on the machine.
    ${If} ${FileExists} "$INSTDIR\${APP_EXE}"
        DetailPrint "Restoring controller visibility before removing files..."
        nsExec::ExecToLog '"$INSTDIR\${APP_EXE}" --recover-hidhide'
        Pop $0
        ${If} $0 != "0"
            DetailPrint "WARNING: recovery reported code $0."
            ${IfNot} ${Silent}
                MessageBox MB_OK|MB_ICONEXCLAMATION "FerroLite could not confirm that your controller is visible again. Reinstalling FerroLite once will also repair it, or run the app with the --recover-hidhide option."
            ${EndIf}
        ${Else}
            DetailPrint "Controller visibility restored."
        ${EndIf}
    ${EndIf}

    ; Stop a running instance: its in-flight state must not be overwritten.
    DetailPrint "Closing FerroLite if it is running..."
    nsExec::Exec 'taskkill /F /IM "${APP_PROCNAME}.exe" /T'
    Pop $0
FunctionEnd

; ================= Section: App =================
Section "App (required)" SecApp
    SectionIn RO

    SetOutPath "$INSTDIR"
    File /r "..\bin\*.*"

    ; Drivers readme (documentation always present)
    File "..\deps\REQUIRED_DRIVERS.txt"

    ; Registry: install dir + version (also read by InstallDirRegKey on upgrade)
    WriteRegStr HKLM "${REG_COMPANY}" "InstallDir" "$INSTDIR"
    WriteRegStr HKLM "${REG_COMPANY}" "Version"    "${APP_VERSION}"

    ; Shortcuts (all-users context)
    SetShellVarContext all
    CreateDirectory "$SMPROGRAMS\${APP_NAME}"
    CreateShortCut  "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
    CreateShortCut  "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"
    CreateShortCut  "$SMPROGRAMS\${APP_NAME}\Required Drivers.lnk" "$INSTDIR\REQUIRED_DRIVERS.txt"
    CreateShortCut  "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"

    ; Add/Remove Programs entry
    WriteRegStr   HKLM "${REG_UNINST}" "DisplayName"    "${APP_NAME}"
    WriteRegStr   HKLM "${REG_UNINST}" "UninstallString" "$INSTDIR\Uninstall.exe"
    WriteRegStr   HKLM "${REG_UNINST}" "QuietUninstallString" "$INSTDIR\Uninstall.exe /S"
    WriteRegStr   HKLM "${REG_UNINST}" "InstallLocation" "$INSTDIR"
    WriteRegStr   HKLM "${REG_UNINST}" "DisplayIcon"    "$INSTDIR\${APP_EXE}"
    WriteRegStr   HKLM "${REG_UNINST}" "DisplayVersion" "${APP_VERSION}"
    WriteRegStr   HKLM "${REG_UNINST}" "Publisher"      "${APP_PUBLISHER}"
    WriteRegDWORD HKLM "${REG_UNINST}" "NoModify" 1
    WriteRegDWORD HKLM "${REG_UNINST}" "NoRepair" 1

    ; Estimate installed size for AR&P (kilobytes)
    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $0 "0x%08X" $0
    WriteRegDWORD HKLM "${REG_UNINST}" "EstimatedSize" $0

    WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

; ================= Section: ViGEmBus driver (downloaded) =================
Section "ViGEmBus virtual gamepad driver (recommended)" SecViGEm
    !insertmacro DriverInstalled "ViGEmBus" "ViGEmBus_is1" "ViGEmBus_is1"
    ${If} $0 == "1"
        DetailPrint "ViGEmBus is already installed - skipping download."
        Return
    ${EndIf}

    DetailPrint "ViGEmBus ${VIGEMBUS_VER} will now be DOWNLOADED from Nefarius' official GitHub release."
    !insertmacro DownloadFile "${VIGEMBUS_URL}" "$PLUGINSDIR\ViGEmBusSetup.exe"
    ${If} ${FileExists} "$PLUGINSDIR\ViGEmBusSetup.exe"
        ; Supply-chain guard: refuse to run anything not signed by Nefarius.
        !insertmacro VerifySignature "$PLUGINSDIR\ViGEmBusSetup.exe" "Nefarius Software Solutions"
        ${If} $0 != 0
            DetailPrint "SECURITY: downloaded ViGEmBus setup failed signature verification - NOT running it."
            Delete "$PLUGINSDIR\ViGEmBusSetup.exe"
            DetailPrint "You can install ViGEmBus manually later (see REQUIRED_DRIVERS.txt)."
            Return
        ${EndIf}
        DetailPrint "Signature OK (Nefarius Software Solutions). Installing ViGEmBus ${VIGEMBUS_VER} (silent)..."
        ExecWait '"$PLUGINSDIR\ViGEmBusSetup.exe" /passive /norestart' $0
        ${If} $0 == 0
            DetailPrint "ViGEmBus installed successfully."
        ${ElseIf} $0 == 3010
            DetailPrint "ViGEmBus installed - a REBOOT is required."
            SetRebootFlag true
        ${Else}
            DetailPrint "ViGEmBus setup exited with code $0. You can install it later (see REQUIRED_DRIVERS.txt)."
        ${EndIf}
    ${Else}
        DetailPrint "ERROR: ViGEmBus download failed (no internet?). You can install it later (see REQUIRED_DRIVERS.txt)."
    ${EndIf}
SectionEnd

; ================= Section: HidHide driver (downloaded) =================
; Kernel "device firewall": hides the physical controller from every app
; except FerroLite while a script mediates it through the virtual pad.
Section "HidHide controller isolation driver (recommended)" SecHidHide
    !insertmacro DriverInstalledDep "HidHide" "NSS.Drivers.HidHide.x64" "HidHide_is1"
    ${If} $0 == "1"
        DetailPrint "HidHide is already installed - skipping download."
    ${Else}
        DetailPrint "HidHide ${HIDHIDE_VER} will now be DOWNLOADED from Nefarius' official GitHub release."
        !insertmacro DownloadFile "${HIDHIDE_URL}" "$PLUGINSDIR\HidHideSetup.exe"
        ${If} ${FileExists} "$PLUGINSDIR\HidHideSetup.exe"
            ; Supply-chain guard: refuse to run anything not signed by Nefarius.
            !insertmacro VerifySignature "$PLUGINSDIR\HidHideSetup.exe" "Nefarius Software Solutions"
            ${If} $0 == 0
                DetailPrint "Signature OK (Nefarius Software Solutions). Installing HidHide ${HIDHIDE_VER} (silent)..."
                ExecWait '"$PLUGINSDIR\HidHideSetup.exe" /passive /norestart' $0
                ${If} $0 == 0
                    DetailPrint "HidHide installed successfully."
                ${ElseIf} $0 == 3010
                    DetailPrint "HidHide installed - a REBOOT is required."
                ${Else}
                    DetailPrint "HidHide setup exited with code $0. You can install it later (see REQUIRED_DRIVERS.txt)."
                ${EndIf}
            ${Else}
                DetailPrint "SECURITY: downloaded HidHide setup failed signature verification - NOT running it."
                Delete "$PLUGINSDIR\HidHideSetup.exe"
            ${EndIf}
        ${Else}
            DetailPrint "ERROR: HidHide download failed (no internet?). You can install it later (see REQUIRED_DRIVERS.txt)."
        ${EndIf}
    ${EndIf}
SectionEnd

; ================= Section: vJoy driver (downloaded) =================
Section "vJoy virtual gamepad driver (legacy)" SecVJoy
    !insertmacro DriverInstalled "vjoy" "vJoy_is1" "vJoy_is1"
    ${If} $0 == "1"
        DetailPrint "vJoy is already installed - skipping download."
        Return
    ${EndIf}

    DetailPrint "vJoy ${VJOY_VER} will now be DOWNLOADED from the official vJoy GitHub release."
    !insertmacro DownloadFile "${VJOY_URL}" "$PLUGINSDIR\vJoySetup.exe"
    ${If} ${FileExists} "$PLUGINSDIR\vJoySetup.exe"
        ; Supply-chain guard: refuse to run anything not signed by the vJoy fork maintainer.
        !insertmacro VerifySignature "$PLUGINSDIR\vJoySetup.exe" "On-site Dental Systems"
        ${If} $0 != 0
            DetailPrint "SECURITY: downloaded vJoy setup failed signature verification - NOT running it."
            Delete "$PLUGINSDIR\vJoySetup.exe"
            DetailPrint "You can install vJoy manually later (see REQUIRED_DRIVERS.txt)."
            Return
        ${EndIf}
        DetailPrint "Signature OK (vJoy project). Installing vJoy ${VJOY_VER} (silent)..."
        ExecWait '"$PLUGINSDIR\vJoySetup.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART' $0
        ${If} $0 == 0
            DetailPrint "vJoy installed successfully."
        ${Else}
            DetailPrint "vJoy setup exited with code $0. You can install it later (see REQUIRED_DRIVERS.txt)."
        ${EndIf}
    ${Else}
        DetailPrint "ERROR: vJoy download failed (no internet?). You can install it later (see REQUIRED_DRIVERS.txt)."
    ${EndIf}
SectionEnd

; ================= Section: Source (comment-free) =================
Section "Source code (build it yourself)" SecSource
    ; The COMPLETE source tree, minus comments. Ships the Core, UI and TEST
    ; projects together with FerroliteStudio.slnx, so a user can audit every
    ; line, run `dotnet build FerroliteStudio.slnx` and `dotnet test`, and end
    ; up with their own binary instead of trusting ours. See BUILD.md here.
    SetOutPath "$INSTDIR\src"
    File /r "..\src-clean\*.*"
    SetOutPath "$INSTDIR"
SectionEnd

Section "Documentation" SecDocs
    SetOutPath "$INSTDIR\Documents"
    File /r "..\..\Documents\*.*"
    SetOutPath "$INSTDIR"
    CreateShortcut "$SMPROGRAMS\${APP_NAME}\Documentation.lnk" "$INSTDIR\Documents\DOCUMENTATION.md"
SectionEnd

; ================= Uninstaller =================
Section "Uninstall"
    ${If} ${RunningX64}
        SetRegView 64
    ${EndIf}
    SetShellVarContext all

    ; Shortcuts
    Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${APP_NAME}\Required Drivers.lnk"
    Delete "$SMPROGRAMS\${APP_NAME}\Documentation.lnk"
    RMDir  "$SMPROGRAMS\${APP_NAME}"
    Delete "$DESKTOP\${APP_NAME}.lnk"

    ; Restore device visibility and stop the app BEFORE deleting anything:
    ; the ownership records under %LOCALAPPDATA% plus this exe are what undo a
    ; leftover hide. Deleting first would strand the controller hidden.
    Call un.RestoreBeforeRemoving

    ; Ask about user data BEFORE any RMDir: /r on $INSTDIR removes this very
    ; uninstaller, and NSIS gives it only a short grace period before the
    ; process is terminated - so no user-facing prompt may come after it.
    Call un.RemoveLocalData

    ; Files/dirs (RMDir /r is safe here: $INSTDIR is ours).
    ; NOTE: installed drivers (ViGEmBus/vJoy) are NOT removed - uninstall them
    ; from Windows' Apps & Features if desired.
    RMDir /r "$INSTDIR"

    ; Registry
    DeleteRegKey HKLM "${REG_UNINST}"
    DeleteRegKey HKLM "${REG_COMPANY}"
SectionEnd

; ---------------- Descriptions (must come after sections: ${Sec*} defined above) ----------------
LangString DESC_App    0 "The FerroLite Studio application (self-contained, no .NET install needed) plus the drivers readme."
LangString DESC_ViGEm  0 "ViGEmBus ${VIGEMBUS_VER} virtual gamepad driver by Nefarius. Makes games see a genuine Xbox 360 controller. Downloaded (~6 MB) from the official GitHub release during setup and installed silently."
LangString DESC_HidHide 0 "HidHide ${HIDHIDE_VER} controller isolation driver by Nefarius. Hides your physical controller from other apps while a script mediates it, so the game sees exactly one controller. Downloaded (~8 MB) from the official GitHub release during setup and installed silently."
LangString DESC_VJoy   0 "vJoy ${VJOY_VER} virtual gamepad driver. Legacy alternative to ViGEmBus. Downloaded (~11 MB) from the official GitHub release during setup and installed silently."
LangString DESC_Source 0 "The complete FerroLite Studio source code."
LangString DESC_Docs   0 "Official documentation: complete language reference (293 functions), user documentation, installation guide, and license."

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecApp}    "$(DESC_App)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecViGEm}  "$(DESC_ViGEm)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecHidHide} "$(DESC_HidHide)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecVJoy}   "$(DESC_VJoy)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecSource} "$(DESC_Source)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDocs}   "$(DESC_Docs)"
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ---------------- Init: silent installs skip driver downloads by default ----------------
Function .onInit
    ; Use the native 64-bit registry view on x64 so entries don't land in WOW6432Node.
    ${If} ${RunningX64}
        SetRegView 64
    ${EndIf}

    ; Refuse to install over a running instance. Overwriting a live FerroLite
    ; can both fail (locked files) and destroy an in-flight armed controller
    ; session, so ask the user to close it first.
    nsExec::Exec 'tasklist /FI "IMAGENAME eq ${APP_PROCNAME}.exe" /NH'
    Pop $0
    ${If} $0 == "0"
        ${IfNot} ${Silent}
            ; $\r$\n (not $\r$n): the second $ terminates the first escape, so the
            ; following word is not swallowed into the variable name.
            MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION "FerroLite Studio is currently running.$\r$\n$\r$\nPlease close it, then choose OK to continue (setup will stop it for you). Choose Cancel to abort the installation." IDOK continue IDCANCEL abort
            Goto continue
            abort:
                Abort "Setup cancelled: please close FerroLite Studio first."
            continue:
        ${EndIf}
        nsExec::Exec 'taskkill /F /IM "${APP_PROCNAME}.exe" /T'
        Pop $0
        Sleep 400
    ${EndIf}
    ${If} ${Silent}
        ClearErrors
        ReadEnvStr $0 FERROLITE_INSTALL_DRIVERS
        ${If} $0 != "1"
            SectionSetFlags ${SecViGEm} 0
            SectionSetFlags ${SecVJoy}  0
            DetailPrint "Silent install: driver components skipped (set FERROLITE_INSTALL_DRIVERS=1 to enable)."
        ${EndIf}
    ${EndIf}
FunctionEnd
