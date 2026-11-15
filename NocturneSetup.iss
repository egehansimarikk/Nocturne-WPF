[Setup]
AppId={{8E4F3A21-7C9B-4D1E-9A62-3F8B1C5D4E90}
AppName=Nocturne
AppVersion=1.0.0
AppPublisher=egehansimarikk
DefaultDirName={autopf}\Nocturne
DefaultGroupName=Nocturne
UninstallDisplayIcon={app}\Nocturne.exe
SetupIconFile=C:\Users\Egehan\source\repos\Nocturne\Assets\nocturne.ico
OutputDir=C:\Users\Egehan\source\repos\Nocturne\Output
OutputBaseFilename=NocturneSetup-1.0.0-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "C:\Users\Egehan\source\repos\Nocturne\bin\Release\net10.0-windows10.0.19041.0\*"; DestDir: "{app}"; Excludes: "*.pdb,win-x64\*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Nocturne"; Filename: "{app}\Nocturne.exe"; IconFilename: "{app}\Assets\nocturne.ico"; AppUserModelID: "com.egehansimarikk.nocturne"
Name: "{autodesktop}\Nocturne"; Filename: "{app}\Nocturne.exe"; IconFilename: "{app}\Assets\nocturne.ico"; AppUserModelID: "com.egehansimarikk.nocturne"; Tasks: desktopicon

[Run]
Filename: "{app}\Nocturne.exe"; Description: "{cm:LaunchProgram,Nocturne}"; Flags: nowait postinstall skipifsilent