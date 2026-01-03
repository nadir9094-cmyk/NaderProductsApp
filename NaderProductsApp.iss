#define AppName "NaderProductsApp"
#define AppVersion "1.0.0"
#define AppPublisher "Nader"
#define AppExeName "NaderProductsApp.exe"

[Setup]
AppId={{8B7B9F0C-8E66-4A3D-AF5F-7B6D2A4A77A1}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
OutputDir=C:\sami
OutputBaseFilename=NaderProductsApp_Setup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Files]
Source: "C:\sami\installer\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"

[Run]
Filename: "{cmd}"; Parameters: "/C powershell -NoProfile -ExecutionPolicy Bypass -File ""{app}\postinstall.ps1"""; Flags: runhidden
Filename: "{app}\{#AppExeName}"; Description: "تشغيل البرنامج الآن"; Flags: nowait postinstall skipifsilent
