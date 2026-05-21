; ═══════════════════════════════════════════════════════════════════
;  SYTEX L-Core PIXEL ART INSTALLER SCRIPT
; ═══════════════════════════════════════════════════════════════════

#define MyAppName    "SYTEX L-Core"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "SYTEX"
#define MyAppExeName "SYTEX-LCore.exe"
#define MySourceDir  "C:\Users\mamo1\.gemini\antigravity\scratch\SYTEX-LCore-CS\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
AppId={{D3F75D27-FF4C-4061-B9D4-098FEE6ACFF7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=SYTEX_L-Core_Setup
OutputDir=C:\Users\mamo1\.gemini\antigravity\scratch\SYTEX-LCore-CS\Output
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardImageFile=C:\Users\mamo1\.gemini\antigravity\scratch\SYTEX-LCore-CS\setup_banner.bmp
WizardSmallImageFile=C:\Users\mamo1\.gemini\antigravity\scratch\SYTEX-LCore-CS\setup_logo.bmp
SetupIconFile=C:\Users\mamo1\.gemini\antigravity\scratch\SYTEX-LCore-CS\Sytex L-Core Logo.ico
PrivilegesRequired=lowest
ShowLanguageDialog=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}";       Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]

const
  DARK_BG    = $18110E;
  NEON_CYAN  = $FFFF00;
  NEON_PINK  = $7F00FF;
  DARK_CARD  = $271520;
  TEXT_WHITE = $FFFFFF;
  TEXT_MUTED = $B58B9D;

procedure StyleBtn(Btn: TButton);
begin
  Btn.Font.Color := NEON_CYAN;
  Btn.Font.Name  := 'Consolas';
  Btn.Font.Size  := 9;
  Btn.Font.Style := [fsBold];
end;

procedure InitializeWizard;
begin
  WizardForm.Color      := DARK_BG;
  WizardForm.OuterNotebook.Color := DARK_BG;
  WizardForm.InnerNotebook.Color := DARK_BG;
  
  WizardForm.Font.Color := TEXT_WHITE;
  WizardForm.Font.Name  := 'Consolas';
  WizardForm.Font.Size  := 9;

  WizardForm.MainPanel.Color := DARK_BG;

  WizardForm.PageNameLabel.Font.Color := NEON_CYAN;
  WizardForm.PageNameLabel.Font.Name  := 'Consolas';
  WizardForm.PageNameLabel.Font.Size  := 11;
  WizardForm.PageNameLabel.Font.Style := [fsBold];

  WizardForm.PageDescriptionLabel.Font.Color := TEXT_MUTED;
  WizardForm.PageDescriptionLabel.Font.Name  := 'Consolas';

  WizardForm.InnerPage.Color := DARK_BG;

  StyleBtn(WizardForm.NextButton);
  StyleBtn(WizardForm.BackButton);
  StyleBtn(WizardForm.CancelButton);

  WizardForm.NextButton.Caption   := '[ DEVAM >> ]';
  WizardForm.BackButton.Caption   := '[ << GERI ]';
  WizardForm.CancelButton.Caption := '[ X CIKIS ]';

  WizardForm.DirEdit.Color      := DARK_CARD;
  WizardForm.DirEdit.Font.Color := NEON_CYAN;
  WizardForm.DirEdit.Font.Name  := 'Consolas';

  StyleBtn(WizardForm.DirBrowseButton);
  WizardForm.DirBrowseButton.Caption := '[ ... ]';

  WizardForm.ReadyMemo.Color      := DARK_CARD;
  WizardForm.ReadyMemo.Font.Color := NEON_CYAN;
  WizardForm.ReadyMemo.Font.Name  := 'Consolas';

  WizardForm.WelcomePage.Color := DARK_BG;
  WizardForm.FinishedPage.Color := DARK_BG;
  
  WizardForm.WelcomeLabel1.Font.Color := NEON_CYAN;
  WizardForm.WelcomeLabel2.Font.Color := TEXT_WHITE;
  
  WizardForm.FinishedHeadingLabel.Font.Color := NEON_CYAN;
  WizardForm.FinishedLabel.Font.Color := TEXT_WHITE;
end;
