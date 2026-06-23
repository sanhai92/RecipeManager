#ifndef MyAppVersion
  #define MyAppVersion "1.0.3"
#endif
#ifndef MyAppPublisher
  #define MyAppPublisher "sande"
#endif

#define MyAppName "Recipe Manager"
#define MyAppExeName "RecipeManager.exe"

[Setup]
AppId={{C2B340D8-8D1D-4D9C-94B7-78E68A98E127}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\RecipeManager
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\artifacts\installer
OutputBaseFilename=RecipeManager-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
CloseApplications=yes
RestartApplications=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\Assets\RecipeManager.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Open {#MyAppName}"; Flags: nowait

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  PreviousVersion: String;
  UpdateFolder: String;
  MarkerJson: String;
begin
  Result := '';
  if RegQueryStringValue(HKCU,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{C2B340D8-8D1D-4D9C-94B7-78E68A98E127}_is1',
    'DisplayVersion', PreviousVersion) and
    (CompareText(PreviousVersion, '{#MyAppVersion}') <> 0) then
  begin
    UpdateFolder := ExpandConstant('{localappdata}\RecipeManager');
    ForceDirectories(UpdateFolder);
    MarkerJson := '{"FromVersion":"' + PreviousVersion + '","ToVersion":"{#MyAppVersion}"}';
    SaveStringToFile(UpdateFolder + '\pending-update.json', MarkerJson, False);
  end;
end;
