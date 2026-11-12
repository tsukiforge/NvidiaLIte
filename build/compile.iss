#ifndef AppVersion
  #define AppVersion "3.0.0"
#endif

[Setup]
AppId={{D3B3E1A2-8F9B-4D8E-BB2D-F67389D9D2A1}
AppName=NVIDIA LITE
AppVersion={#AppVersion}
AppPublisher=Frieren
DefaultDirName={autopf}\NvidiaLite
DefaultGroupName=NVIDIA LITE
SetupIconFile=..\Assets\icon.ico
UninstallDisplayIcon={app}\Assets\icon.ico
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=NvidiaLite_Setup_v{#AppVersion}
ArchitecturesInstallIn64BitMode=x64
; Optimasi Installation & Uninstallation
CloseApplications=yes
AppMutex=NvidiaCiMutex
PrivilegesRequired=admin
SignedUninstaller=no

[UninstallDelete]
; Memastikan folder terhapus bersih termasuk file yang dibuat saat runtime
Type: filesandordirs; Name: "{app}\*"
Type: filesandordirs; Name: "{app}"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\NVIDIA LITE"; Filename: "{app}\NvidiaCi.exe"
Name: "{autodesktop}\NVIDIA LITE"; Filename: "{app}\NvidiaCi.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NvidiaCi.exe"; Description: "{cm:LaunchProgram,NVIDIA LITE}"; Flags: nowait postinstall skipifsilent
