#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#define MyAppName "SPECTRA"
#define MyAppPublisher "femboy-organization"

[Setup]
AppId={{12D0BDFB-2E06-4A59-9D8D-0E615E1F3D02}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\SPECTRA
DefaultGroupName=SPECTRA
OutputDir=..\..\artifacts
OutputBaseFilename=SPECTRA-{#MyAppVersion}-Setup-win-x64
UninstallDisplayIcon={app}\SPECTRA.exe
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
DisableProgramGroupPage=yes
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SPECTRA"; Filename: "{app}\SPECTRA.exe"
Name: "{autodesktop}\SPECTRA"; Filename: "{app}\SPECTRA.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SPECTRA.exe"; Description: "Launch SPECTRA"; Flags: postinstall nowait skipifsilent
