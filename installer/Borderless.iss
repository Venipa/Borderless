; Borderless — Inno Setup installer (framework-dependent; needs .NET 9 Desktop Runtime)
; Build: ISCC.exe /DMyAppVersion=1.0.0.0 /DMyAppSourceDir=..\publish installer\Borderless.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0.0"
#endif

#ifndef MyAppSourceDir
  #define MyAppSourceDir "..\publish"
#endif

#define MyAppName "Borderless"
#define MyAppPublisher "Venipa"
#define MyAppExeName "Borderless.exe"
#define MyAppIcon "..\Borderless.App\Resources\app.ico"
#define MyAppURL "https://github.com/Venipa/Borderless"

[Setup]
AppId={{8F3C2A91-6B4E-4D7A-9C1F-2E5B8A0D4F73}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..
OutputBaseFilename=Borderless-{#MyAppVersion}-win-x64-setup
SetupIconFile={#MyAppIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=yes
RestartApplications=no
UsedUserAreasWarning=no
VersionInfoVersion={#MyAppVersion}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppName} Setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"


[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Wipe previous payload so old self-contained runtime DLLs cannot mix with framework-dependent builds.
Type: filesandordirs; Name: "{app}\*"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Launch elevated after install (including silent updates — do not use skipifsilent).
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Verb: runas; Flags: nowait postinstall shellexec

[Code]
// WPF needs Microsoft.WindowsDesktop.App — plain ".NET Runtime" is not enough.
function IsDotNetDesktop9Installed: Boolean;
var
  FindRec: TFindRec;
  SharedDir: String;
begin
  Result := False;
  SharedDir := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(SharedDir) then
    SharedDir := ExpandConstant('{pf}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(SharedDir) then
    Exit;
  if FindFirst(SharedDir + '\9.*', FindRec) then
  begin
    try
      Result := True;
    finally
      FindClose(FindRec);
    end;
  end;
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if IsDotNetDesktop9Installed then
    Exit;

  if MsgBox(
       '{#MyAppName} needs the .NET 9 Desktop Runtime (x64).' + #13#10 +
       'The normal ".NET Runtime" package is not enough for this WPF app.' + #13#10#13#10 +
       'Open the Desktop Runtime download now?',
       mbConfirmation, MB_YESNO) = IDYES then
  begin
    ShellExec(
      'open',
      'https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe',
      '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;

  Result := MsgBox(
    'Install .NET 9 Desktop Runtime (x64), then continue.' + #13#10#13#10 +
    'Continue Borderless setup anyway?',
    mbConfirmation, MB_YESNO) = IDYES;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Belt-and-suspenders: clear {app} before file copy on upgrade.
  if CurStep = ssInstall then
  begin
    if DirExists(ExpandConstant('{app}')) then
      DelTree(ExpandConstant('{app}'), False, True, True);
  end;
end;
