; Instalátor Gasbyn Optimizer (Inno Setup 6).
; Sestavení: iscc /DAppVersion=3.0.0 installer\GasbynOptimizer.iss
; Očekává publikovanou aplikaci ve složce ..\publish (viz .github\workflows\build.yml).

#ifndef AppVersion
  #define AppVersion "3.0.0"
#endif

[Setup]
AppId={{B6E1D0A4-5C2F-4B7A-9D3E-7F1A2C8E4D90}
AppName=Gasbyn Optimizer
AppVersion={#AppVersion}
AppPublisher=Gasbyn
DefaultDirName={autopf}\Gasbyn Optimizer
DefaultGroupName=Gasbyn Optimizer
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=GasbynOptimizer-Setup-{#AppVersion}
SetupIconFile=..\GasbynOptimizer.ico
UninstallDisplayIcon={app}\GasbynOptimizer.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes

[Languages]
#if FileExists(CompilerPath + "Languages\Czech.isl")
Name: "czech"; MessagesFile: "compiler:Languages\Czech.isl"
#endif
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\GasbynOptimizer.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Gasbyn Optimizer"; Filename: "{app}\GasbynOptimizer.exe"
Name: "{autodesktop}\Gasbyn Optimizer"; Filename: "{app}\GasbynOptimizer.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\GasbynOptimizer.exe"; Description: "{cm:LaunchProgram,Gasbyn Optimizer}"; Flags: nowait postinstall skipifsilent shellexec
