[Setup]
AppId={{8E4F3A21-7C9B-4D1E-9A62-3F8B1C5D4E90}
AppName=Nocturne
AppVersion=0.1.1
AppPublisher=egehansimarikk
DefaultDirName={autopf}\Nocturne
DefaultGroupName=Nocturne
UninstallDisplayIcon={app}\Nocturne.exe
SetupIconFile=C:\Users\Egehan\source\repos\Nocturne\Assets\nocturne.ico
OutputDir=C:\Users\Egehan\source\repos\Nocturne\Output
OutputBaseFilename=NocturneSetup-0.1.1-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
CloseApplications=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "C:\Users\Egehan\source\repos\Nocturne\bin\Release\net10.0-windows10.0.19041.0\win-x64\*"; DestDir: "{app}"; Excludes: "*.pdb,publish\*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Nocturne"; Filename: "{app}\Nocturne.exe"; AppUserModelID: "com.egehansimarikk.nocturne"
Name: "{autodesktop}\Nocturne"; Filename: "{app}\Nocturne.exe"; AppUserModelID: "com.egehansimarikk.nocturne"; Tasks: desktopicon

[Run]
Filename: "{app}\Nocturne.exe"; Description: "{cm:LaunchProgram,Nocturne}"; Flags: nowait postinstall skipifsilent