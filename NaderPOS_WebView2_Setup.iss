#define AppName "NADER POS"
#define AppExe  "LauncherWeb.exe"
#define AppVer  "1.0.0"

[Setup]
AppId={{7F4DAB52-4A5B-4D8C-9E8A-2E8B9B0A1B21}
AppName={#AppName}
AppVersion={#AppVer}
DefaultDirName={pf}\NaderPOS
DefaultGroupName=NaderPOS
OutputBaseFilename=NaderPOS_Setup
OutputDir=C:\sami
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
SetupIconFile=C:\sami\setup_payload\app.ico
UninstallDisplayIcon={app}\app.ico
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Files]
Source: "C:\sami\setup_payload\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{commondesktop}\NADER POS"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\app.ico"

[Run]
Filename: "{app}\{#AppExe}"; Flags: nowait postinstall skipifsilent
