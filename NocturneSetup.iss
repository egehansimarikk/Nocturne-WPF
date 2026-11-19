[Setup]
AppId={{8E4F3A21-7C9B-4D1E-9A62-3F8B1C5D4E90}
AppName=Nocturne
AppVersion=0.1.3
AppPublisher=egehansimarikk
DefaultDirName={autopf}\Nocturne
DefaultGroupName=Nocturne
UninstallDisplayIcon={app}\Assets\nocturne.ico
SetupIconFile=C:\Users\Egehan\source\repos\Nocturne\Assets\nocturne.ico
OutputDir=C:\Users\Egehan\source\repos\Nocturne\Output
OutputBaseFilename=NocturneSetup-0.1.3-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
CloseApplications=yes
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "swedish"; MessagesFile: "compiler:Languages\Swedish.isl"
Name: "norwegian"; MessagesFile: "compiler:Languages\Norwegian.isl"
Name: "danish"; MessagesFile: "compiler:Languages\Danish.isl"
Name: "finnish"; MessagesFile: "compiler:Languages\Finnish.isl"
Name: "czech"; MessagesFile: "compiler:Languages\Czech.isl"
Name: "hungarian"; MessagesFile: "compiler:Languages\Hungarian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "C:\Users\Egehan\source\repos\Nocturne\bin\Release\net10.0-windows10.0.19041.0\win-x64\*"; DestDir: "{app}"; Excludes: "*.pdb,publish\*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Nocturne"; Filename: "{app}\Nocturne.exe"; IconFilename: "{app}\Assets\nocturne.ico"
Name: "{autodesktop}\Nocturne"; Filename: "{app}\Nocturne.exe"; IconFilename: "{app}\Assets\nocturne.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\Nocturne.exe"; Description: "{cm:LaunchProgram,Nocturne}"; Flags: nowait postinstall skipifsilent