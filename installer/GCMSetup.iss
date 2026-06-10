#define MyAppName "Game Console Mode"
#define MyAppExeName "gcmloader.exe"
#define MyServiceName "GCMPrivilegedService"
#define MyAppShortcutIcon "gcm-logo.ico"
#ifndef AppVersion
  #define AppVersion "2.6.8"
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
AppPublisherURL=https://github.com/toonymak1993/GamingConsoleMode
DefaultDirName={autopf}\Game Console Mode
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppShortcutIcon}
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
CloseApplicationsFilter={#MyAppExeName},GAMINGCONSOLEMODE.exe,GameConsoleMode.exe,GamingConsoleMode.exe
RestartApplications=no
UsePreviousAppDir=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "..\gcmloader\logo.ico"; DestDir: "{app}"; DestName: "{#MyAppShortcutIcon}"; Flags: ignoreversion

[Registry]
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"; ValueType: string; ValueName: "Shell"; ValueData: """{app}\{#MyAppExeName}"""
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"; ValueName: "Shell"; Flags: deletevalue

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppShortcutIcon}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppShortcutIcon}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C reg add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"" /v Shell /t REG_SZ /d explorer.exe /f >nul 2>&1"; Flags: runhidden waituntilterminated; RunOnceId: "RestoreWinlogonShell"
Filename: "{cmd}"; Parameters: "/C reg delete ""HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"" /v Shell /f >nul 2>&1"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveUserWinlogonShell"
Filename: "{cmd}"; Parameters: "/C sc stop ""{#MyServiceName}"" >nul 2>&1"; Flags: runhidden waituntilterminated; RunOnceId: "StopGcmPrivilegedService"
Filename: "{cmd}"; Parameters: "/C sc delete ""{#MyServiceName}"" >nul 2>&1"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteGcmPrivilegedService"

[Code]
var
  ProcessedUninstallCommands: string;
  GcmShellRestoredOnUninstall: Boolean;

function HasProcessedCommand(const Command: string): Boolean;
begin
  Result := Pos('|' + LowerCase(Trim(Command)) + '|', ProcessedUninstallCommands) > 0;
end;

procedure MarkProcessedCommand(const Command: string);
begin
  ProcessedUninstallCommands := ProcessedUninstallCommands + '|' + LowerCase(Trim(Command)) + '|';
end;

procedure SplitCommand(const CommandLine: string; var FileName: string; var Parameters: string);
var
  Work: string;
  ClosingQuotePos: Integer;
  SpacePos: Integer;
begin
  Work := Trim(CommandLine);
  FileName := '';
  Parameters := '';

  if Work = '' then
    exit;

  if Work[1] = '"' then
  begin
    Delete(Work, 1, 1);
    ClosingQuotePos := Pos('"', Work);
    if ClosingQuotePos > 0 then
    begin
      FileName := Copy(Work, 1, ClosingQuotePos - 1);
      Parameters := Trim(Copy(Work, ClosingQuotePos + 1, MaxInt));
    end
    else
      FileName := Work;
  end
  else
  begin
    SpacePos := Pos(' ', Work);
    if SpacePos > 0 then
    begin
      FileName := Copy(Work, 1, SpacePos - 1);
      Parameters := Trim(Copy(Work, SpacePos + 1, MaxInt));
    end
    else
      FileName := Work;
  end;
end;

function BuildSilentUninstallCommand(const QuietCommand: string; const UninstallCommand: string): string;
var
  LowerCommand: string;
begin
  if Trim(QuietCommand) <> '' then
    Result := Trim(QuietCommand)
  else
    Result := Trim(UninstallCommand);

  LowerCommand := LowerCase(Result);

  if Pos('msiexec', LowerCommand) > 0 then
  begin
    StringChangeEx(Result, '/I{', '/X{', True);
    StringChangeEx(Result, '/i{', '/X{', True);
    StringChangeEx(Result, '/I ', '/X ', True);
    StringChangeEx(Result, '/i ', '/X ', True);
    if Pos('/qn', LowerCase(Result)) = 0 then
      Result := Result + ' /qn /norestart';
  end
  else if Pos('unins', LowerCommand) > 0 then
  begin
    if Pos('/verysilent', LowerCase(Result)) = 0 then
      Result := Result + ' /VERYSILENT /SUPPRESSMSGBOXES /NORESTART';
  end
  else if Pos('/silent', LowerCommand) = 0 then
    Result := Result + ' /SILENT /NORESTART';
end;

function IsLegacyGcmEntry(const DisplayName: string; const UninstallCommand: string): Boolean;
var
  LowerName: string;
  LowerCommand: string;
begin
  LowerName := LowerCase(DisplayName);
  LowerCommand := LowerCase(UninstallCommand);

  Result :=
    (Pos('game console mode', LowerName) > 0) or
    (Pos('gamingconsolemode', LowerName) > 0) or
    (Pos('gcm game console mode', LowerName) > 0) or
    (Pos('gcmloader', LowerName) > 0) or
    (Pos('\gmc\', LowerCommand) > 0) or
    (Pos('gcmloader.exe', LowerCommand) > 0);
end;

procedure CloseKnownGcmProcesses();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C sc stop "{#MyServiceName}" >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{cmd}'), '/C sc delete "{#MyServiceName}" >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /T /IM gcmloader.exe >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /T /IM GAMINGCONSOLEMODE.exe >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /T /IM GameConsoleMode.exe >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /T /IM GamingConsoleMode.exe >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure RestoreExplorerShellForUninstall();
begin
  Log('Restoring Windows Explorer as Winlogon shell for uninstall.');

  if RegWriteStringValue(
    HKLM,
    'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon',
    'Shell',
    'explorer.exe') then
  begin
    GcmShellRestoredOnUninstall := True;
    Log('HKLM Winlogon Shell restored to explorer.exe.');
  end
  else
    Log('WARNING: Could not restore HKLM Winlogon Shell to explorer.exe.');

  if RegValueExists(HKCU, 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon', 'Shell') then
  begin
    if RegDeleteValue(HKCU, 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon', 'Shell') then
      Log('Removed HKCU Winlogon Shell override.')
    else
      Log('WARNING: Could not remove HKCU Winlogon Shell override.');
  end;
end;

function UninstallLegacyEntriesInRoot(const RootKey: Integer; const RootPath: string; var FailureMessage: string): Boolean;
var
  SubKeys: TArrayOfString;
  EntryPath: string;
  DisplayName: string;
  UninstallCommand: string;
  QuietUninstallCommand: string;
  SilentCommand: string;
  FileName: string;
  Parameters: string;
  ResultCode: Integer;
  I: Integer;
begin
  Result := True;

  if not RegGetSubkeyNames(RootKey, RootPath, SubKeys) then
    exit;

  for I := 0 to GetArrayLength(SubKeys) - 1 do
  begin
    EntryPath := RootPath + '\' + SubKeys[I];
    DisplayName := '';
    UninstallCommand := '';
    QuietUninstallCommand := '';

    if not RegQueryStringValue(RootKey, EntryPath, 'DisplayName', DisplayName) then
      continue;

    RegQueryStringValue(RootKey, EntryPath, 'UninstallString', UninstallCommand);
    RegQueryStringValue(RootKey, EntryPath, 'QuietUninstallString', QuietUninstallCommand);

    if not IsLegacyGcmEntry(DisplayName, UninstallCommand) then
      continue;

    SilentCommand := BuildSilentUninstallCommand(QuietUninstallCommand, UninstallCommand);
    if (Trim(SilentCommand) = '') or HasProcessedCommand(SilentCommand) then
      continue;

    MarkProcessedCommand(SilentCommand);
    SplitCommand(SilentCommand, FileName, Parameters);

    if Trim(FileName) = '' then
      continue;

    Log(Format('Removing previous installation: %s [%s]', [DisplayName, SilentCommand]));

    if not Exec(FileName, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      FailureMessage := Format(
        'A previous Game Console Mode installation ("%s") could not be removed automatically. Please uninstall it manually and run setup again.', [DisplayName]);
      Result := False;
      exit;
    end;

    if (ResultCode <> 0) and (ResultCode <> 3010) then
    begin
      FailureMessage := Format(
        'The previous Game Console Mode installation ("%s") returned exit code %d during uninstall. Please remove it manually and run setup again.', [DisplayName, ResultCode]);
      Result := False;
      exit;
    end;
  end;
end;

function RemovePreviousGcmInstallations(var FailureMessage: string): Boolean;
begin
  Result := True;
  FailureMessage := '';
  ProcessedUninstallCommands := '';

  if not UninstallLegacyEntriesInRoot(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall', FailureMessage) then
  begin
    Result := False;
    exit;
  end;

  if not UninstallLegacyEntriesInRoot(HKLM, 'Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall', FailureMessage) then
  begin
    Result := False;
    exit;
  end;

  if not UninstallLegacyEntriesInRoot(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall', FailureMessage) then
  begin
    Result := False;
    exit;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  FailureMessage: string;
begin
  Result := '';
  NeedsRestart := False;

  Log('Preparing Game Console Mode upgrade/install.');
  CloseKnownGcmProcesses();

  if not RemovePreviousGcmInstallations(FailureMessage) then
    Result := FailureMessage;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  GcmShellRestoredOnUninstall := False;
  CloseKnownGcmProcesses();
  RestoreExplorerShellForUninstall();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RestoreExplorerShellForUninstall();
end;

function NeedRestart(): Boolean;
begin
  Result := GcmShellRestoredOnUninstall;
end;
