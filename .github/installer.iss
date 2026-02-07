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
AppPublisher=toonymak1993
VersionInfoCompany=toonymak1993
VersionInfoDescription=GCM Game Console Mode Installer
VersionInfoVersion={#MyAppVersion}
AppVerName=GCM Game Console Mode {#MyAppVersion}
WizardStyle=modern
DefaultDirName={pf32}\GCM
DefaultGroupName=GCM
UninstallDisplayIcon={app}\gcmloader\gcmloader.exe
Compression=lzma2
SolidCompression=yes
OutputDir=..\output
OutputBaseFilename=GCM-Setup
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
// Prozedur zum Beenden laufender Prozesse (verhindert Sperrfehler)
procedure KillProcess(const exeName: string);
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /IM ' + exeName + '.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Verzeichnisse löschen (für alte Pfade)
procedure DeleteDirectory(const dirPath: string);
begin
  if DirExists(dirPath) then
  begin
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

  // 2. Alte/falsche Ordnerpfade aufräumen
  dirsToDelete := [
    ExpandConstant('{pf32}\GCMcrew'),
    ExpandConstant('{pf32}\GCM\GCM')
  ];
  for i := 0 to GetArrayLength(dirsToDelete) - 1 do
    DeleteDirectory(dirsToDelete[i]);
end;
