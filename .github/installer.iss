; Define version as a preprocessor variable that can be updated by CI
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0.0"
#endif

[Setup]
AppName=GCM Game Console Mode
AppVersion={#MyAppVersion}
AppVerName=GCM Game Console Mode {#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
WizardStyle=modern
DefaultDirName={pf32}\GCM\GCM
DefaultGroupName=GCM
UninstallDisplayIcon={app}\gcmloader.exe
Compression=lzma2
SolidCompression=yes
; Output directory relative to the ISS file location
OutputDir=..\output
OutputBaseFilename=GCM-Setup-{#MyAppVersion}
PrivilegesRequired=admin
; Use relative path for icon file - now in the root since everything is in one folder
SetupIconFile=..\installer-files\gcmloader\logo.ico
ArchitecturesAllowed=x86 x64
ArchitecturesInstallIn64BitMode=x64
DisableDirPage=yes
DirExistsWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Files]
; Source files relative to the ISS file location
; The workflow copies all build outputs to a single installer-files directory
Source: "..\installer-files\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
; Startmenü-Verknüpfungen
; Since all executables are now in the same folder, adjust paths accordingly
Name: "{group}\GCM Settings"; Filename: "{app}\GAMINGCONSOLEMODE.exe"; WorkingDir: "{app}"
Name: "{group}\GCM Mode"; Filename: "{app}\gcmloader.exe"; IconFilename: "{app}\gcmloader\logo.ico"; WorkingDir: "{app}"

; Optionaler Desktop Shortcut
Name: "{commondesktop}\GCM Mode"; Filename: "{app}\gcmloader.exe"; IconFilename: "{app}\gcmloader\logo.ico"; Tasks: desktopicon

[Run]
; Starte GCM Settings automatisch nach Setup
Filename: "{app}\GAMINGCONSOLEMODE.exe"; Description: "Launch GCM Settings"; Flags: nowait postinstall skipifsilent unchecked

[Code]
// Prozesse beenden
procedure KillProcessIfRunning(const exeName: string);
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /IM ' + exeName + '.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Alten Uninstaller starten
procedure RunExternalUninstaller(const exePath: string);
var
  ResultCode: Integer;
begin
  if FileExists(exePath) then
  begin
    MsgBox('An old version of GCM was detected. It will now be removed automatically. Please wait...', mbInformation, MB_OK);
    if not Exec(exePath, '/SILENT', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
      MsgBox('Uninstaller failed to run. Please uninstall the previous version manually.', mbCriticalError, MB_OK);
      WizardForm.Close;
    end;
  end;
end;

// Haupt-Vorbereitung beim Start des Setups
procedure InitializeWizard;
var
  OldUninstaller, OldGcmcrewDir, OldStartMenuDir, OldSettingsDir: string;
  processList: array of string;
  i: Integer;
begin
  // 1️⃣ Prozesse beenden
  // Add all possible process names including variants
  processList := ['wingamepad', 'gcmloader', 'GAMINGCONSOLEMODE', 'Overlaywindow', 'flowlauncher'];
  for i := 0 to GetArrayLength(processList) - 1 do
    KillProcessIfRunning(processList[i]);

  // 2️⃣ Alten Uninstaller ausführen (check both old locations)
  OldUninstaller := ExpandConstant('{pf32}\GCMcrew\GCM\Uninstall.exe');
  RunExternalUninstaller(OldUninstaller);
  
  // Also check for uninstaller in the current GCM location
  OldUninstaller := ExpandConstant('{pf32}\GCM\GCM\unins000.exe');
  RunExternalUninstaller(OldUninstaller);

  // 3️⃣ Alten Programmordner löschen (GCMcrew)
  OldGcmcrewDir := ExpandConstant('{pf32}\GCMcrew');
  if DirExists(OldGcmcrewDir) then
    DelTree(OldGcmcrewDir, True, True, True);
    
  // 3️⃣ Alten Programmordner löschen (GCM)
  OldGcmcrewDir := ExpandConstant('{pf32}\GCM');
  if DirExists(OldGcmcrewDir) then
    DelTree(OldGcmcrewDir, True, True, True);

  // 4️⃣ Startmenü-Eintrag von GCMcrew löschen
  OldStartMenuDir := ExpandConstant('{commonprograms}\GCMcrew');
  if DirExists(OldStartMenuDir) then
    DelTree(OldStartMenuDir, True, True, True);
    
  // Also remove old GCM start menu entries
  OldStartMenuDir := ExpandConstant('{commonprograms}\GCM');
  if DirExists(OldStartMenuDir) then
    DelTree(OldStartMenuDir, True, True, True);

  // 5️⃣ Benutzer AppData-Einträge löschen
  OldSettingsDir := ExpandConstant('{userappdata}\gcmsettings');
  if DirExists(OldSettingsDir) then
    DelTree(OldSettingsDir, True, True, True);
end;

// Optional: Check if all required files exist after installation
procedure CurStepChanged(CurStep: TSetupStep);
var
  RequiredFiles: array of string;
  i: Integer;
  MissingFiles: string;
begin
  if CurStep = ssPostInstall then
  begin
    // Check for required executables
    RequiredFiles := ['GAMINGCONSOLEMODE.exe', 'gcmloader.exe'];
    MissingFiles := '';
    
    for i := 0 to GetArrayLength(RequiredFiles) - 1 do
    begin
      if not FileExists(ExpandConstant('{app}\' + RequiredFiles[i])) then
      begin
        if MissingFiles <> '' then
          MissingFiles := MissingFiles + ', ';
        MissingFiles := MissingFiles + RequiredFiles[i];
      end;
    end;
    
    if MissingFiles <> '' then
    begin
      MsgBox('Warning: The following files were not installed: ' + MissingFiles + 
             #13#10#13#10 + 'The application may not work correctly.', 
             mbWarning, MB_OK);
    end;
  end;
end;