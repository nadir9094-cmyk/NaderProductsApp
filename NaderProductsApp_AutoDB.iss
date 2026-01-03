#define AppName "NaderPOS (Cashier)"
#define AppExe  "NaderProductsApp.exe"
#define AppVersion "1.0.0"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={pf}\NaderProductsApp
DefaultGroupName=NaderProductsApp
OutputBaseFilename=NaderProductsApp_Setup
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
Name: "{commondesktop}\تشغيل الكاشير (Nader)"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\app.ico"

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\postinstall.ps1"""; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExe}"; Flags: nowait postinstall skipifsilent
Filename: "powershell.exe"; Parameters: "-NoProfile -WindowStyle Hidden -Command ""Start-Sleep 2; Start-Process 'http://127.0.0.1:5050'"""; Flags: nowait postinstall skipifsilent
