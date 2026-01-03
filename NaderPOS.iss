#define AppName "NADER POS"
#define AppExe  "NaderPOSLauncher.exe"

[Setup]
AppName={#AppName}
AppVersion=1.0.0
DefaultDirName={commonpf}\NaderPOS
DefaultGroupName=NaderPOS
OutputBaseFilename=NaderPOS_Setup
OutputDir=C:\sami
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
SetupIconFile=C:\sami\installer\app.ico
UninstallDisplayIcon={app}\app.ico

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Files]
Source: "C:\sami\installer\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{commondesktop}\NADER POS"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\app.ico"

[Run]
Filename: "{app}\{#AppExe}"; Flags: nowait postinstall

