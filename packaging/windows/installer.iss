; Windows installer for Barudak Bogor (Inno Setup 6).
;   iscc /DAppVersion=0.1.0 /DSourceDir=<publish folder> /DOutputDir=<folder> packaging\windows\installer.iss

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\dist\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist"
#endif

[Setup]
AppId={{6B0A0C1E-8D7B-4C52-9F1D-BA2D0A9B0606}
AppName=Barudak Bogor
AppVersion={#AppVersion}
AppPublisher=Syubadubin Studios
AppCopyright=Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios
DefaultDirName={autopf}\Barudak Bogor
DefaultGroupName=Barudak Bogor
DisableProgramGroupPage=yes
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=BarudakBogor-{#AppVersion}-win-x64-setup
SetupIconFile=BarudakBogor.ico
UninstallDisplayIcon={app}\BarudakBogor.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\Barudak Bogor"; Filename: "{app}\BarudakBogor.exe"
Name: "{autodesktop}\Barudak Bogor"; Filename: "{app}\BarudakBogor.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BarudakBogor.exe"; Description: "{cm:LaunchProgram,Barudak Bogor}"; Flags: nowait postinstall skipifsilent
