; Inno Setup Script für GCM
; Finale Version - Mit Registry-Cleanup für alte, fehlerhafte Installationen.

; Define version as a preprocessor variable that can be updated by CI
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

[Setup]
; WICHTIG: Eine feste AppId ist der Standard für saubere Upgrades.
; Ich habe eine für dich generiert. Behalte diese für alle zukünftigen Versionen bei!
AppId={{5E8D8A7F-7201-4A57-A686-352B3C2A5393}}
AppName=GCM Game Console Mode
AppVersion={#MyAppVersion}
AppVerName=GCM Game Console Mode {#MyAppVersion}
WizardStyle=modern
DefaultDirName={pf32}\GCM
DefaultGroupName=GCM
UninstallDisplayIcon={app}\gcmloader\gcmloader.exe
Compression=lzma2
SolidCompression=yes
OutputDir=..\output
OutputBaseFilename=GCM-Setup-{#MyAppVersion}
PrivilegesRequired=admin
SetupIconFile=..\installer-files\gcmloader\logo.ico
ArchitecturesAllowed=x86 x64
ArchitecturesInstallIn64BitMode=x64
DisableDirPage=yes
DirExistsWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Files]
Source: "..\installer-files\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
Name: "{group}\GCM Settings"; Filename: "{app}\GAMINGCONSOLEMODE.exe"; WorkingDir: "{app}"
Name: "{group}\GCM Mode"; Filename: "{app}\gcmloader\gcmloader.exe"; IconFilename: "{app}\gcmloader\logo.ico"; WorkingDir: "{app}\gcmloader"
Name: "{commondesktop}\GCM Mode"; Filename: "{app}\gcmloader\gcmloader.exe"; IconFilename: "{app}\gcmloader\logo.ico"; Tasks: desktopicon; WorkingDir: "{app}\gcmloader"

[Run]
Filename: "{app}\GAMINGCONSOLEMODE.exe"; Description: "Launch GCM Settings"; Flags: nowait postinstall skipifsilent unchecked

[Code]
var
  OldUninstallKey: string;

// NEU: Diese Funktion wird als Allererstes ausgeführt, noch vor dem Wizard.
// Sie bereinigt die Registry von alten, fehlerhaften Einträgen.
function InitializeSetup(): Boolean;
begin
  // Der Deinstallations-Schlüssel, den Inno Setup standardmäßig anlegt.
  OldUninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + ExpandConstant('{#SetupSetting("AppName")}') + '_is1';

  // Wir versuchen, den Schlüssel sowohl für 64-Bit als auch für 32-Bit Systeme zu löschen.
  // Das stellt sicher, dass alle Reste entfernt werden.
  if RegDeleteKeyIncludingSubkeys(HKEY_LOCAL_MACHINE_64, OldUninstallKey) then
    Log(Format('Removed old 64-bit registry key: %s', [OldUninstallKey]))
  else
    Log(Format('No old 64-bit registry key found to remove: %s', [OldUninstallKey]));

  if RegDeleteKeyIncludingSubkeys(HKEY_LOCAL_MACHINE_32, OldUninstallKey) then
    Log(Format('Removed old 32-bit registry key: %s', [OldUninstallKey]))
  else
    Log(Format('No old 32-bit registry key found to remove: %s', [OldUninstallKey]));
    
  Result := True;
end;

procedure KillProcess(const exeName: string);
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /IM ' + exeName + '.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure DeleteDirectory(const dirPath: string);
begin
  if DirExists(dirPath) then
  begin
    Log(Format('Deleting old directory: %s', [dirPath]));
    DelTree(dirPath, True, True, True);
  end;
end;

procedure InitializeWizard;
var
  processList: array of string;
  dirsToDelete: array of string;
  i: Integer;
begin
  // 1. Prozesse beenden
  processList := ['wingamepad', 'gcmloader', 'GAMINGCONSOLEMODE', 'Overlaywindow', 'flowlauncher'];
  for i := 0 to GetArrayLength(processList) - 1 do
    KillProcess(processList[i]);

  // 2. Alte Ordner zwangsweise löschen (doppelt hält besser)
  dirsToDelete := [
    ExpandConstant('{pf32}\GCMcrew'), // Sehr alter Ordner
    ExpandConstant('{pf32}\GCM\GCM'), // Der fehlerhafte, verschachtelte Ordner
    ExpandConstant('{pf32}\GCM')      // Der Hauptordner, um sicherzugehen
  ];
  for i := 0 to GetArrayLength(dirsToDelete) - 1 do
    DeleteDirectory(dirsToDelete[i]);
end;
