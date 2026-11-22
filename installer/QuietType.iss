#define AppVersion "1.0.0"
[Setup]
AppId={{8DA6E2C9-4D24-4B4D-A1AE-6485CDBBE663}
AppName=QuietType
AppVersion={#AppVersion}
AppPublisher=QuietType contributors
AppPublisherURL=https://github.com/Archetype444/QuietType
AppSupportURL=https://github.com/Archetype444/QuietType/issues
DefaultDirName={localappdata}\Programs\QuietType
DefaultGroupName=QuietType
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=..\artifacts
OutputBaseFilename=QuietType-1.0.0-Setup-x64
SetupIconFile=..\assets\QuietType.ico
UninstallDisplayIcon={app}\QuietType.exe
Compression=lzma2
SolidCompression=yes
LZMANumBlockThreads=2
WizardStyle=modern
LicenseFile=..\LICENSE
InfoAfterFile=..\docs\GETTING-STARTED.txt
CloseApplications=yes
CloseApplicationsFilter=QuietType.exe
RestartApplications=no
AppMutex=Local\QuietType.Tray.v1
[Files]
Source: "..\artifacts\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\QuietType"; Filename: "{app}\QuietType.exe"; Parameters: "--settings"
Name: "{group}\Uninstall QuietType"; Filename: "{uninstallexe}"
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "QuietType"; ValueData: """{app}\QuietType.exe"" --background"; Flags: uninsdeletevalue
[Run]
Filename: "{app}\QuietType.exe"; Description: "Start QuietType"; Flags: nowait postinstall skipifsilent
