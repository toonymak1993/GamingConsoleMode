#define MyAppName "Game Console Mode"
#ifndef AppVersion
  #define AppVersion "2.6.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\gcmloader-publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{1A48C041-7B5D-47F0-BC8F-0F3D17D64B8F}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppVerName={#MyAppName} {#AppVersion}
AppPublisher=Luis
AppPublisherURL=https://github.com/toonymak1993/GameConsoleMode
DefaultDirName={autopf}\Game Console Mode
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\gcmloader.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
DisableProgramGroupPage=yes
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir={#OutputDir}
OutputBaseFilename=GCMSetup_{#AppVersion}
SetupIconFile=..\gcmloader\logo.ico
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\gcmloader.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\gcmloader.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\gcmloader.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
