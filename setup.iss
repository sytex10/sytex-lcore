; ═══════════════════════════════════════════════════════════════════
;  SYTEX L-Core INSTALLER SCRIPT - v2.0
; ═══════════════════════════════════════════════════════════════════

#define MyAppName    "SYTEX L-Core"
#define MyAppVersion "Beta"
#define MyAppPublisher "SYTEX"
#define MyAppExeName "SYTEX-LCore.exe"
#define MySourceDir  "C:\Users\mamo1\.gemini\antigravity\scratch\SYTEX-LCore-CS\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
AppId={{D3F75D27-FF4C-4061-B9D4-098FEE6ACFF7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
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
WizardImageBackColor=$18110E
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
  // Ana form
  WizardForm.Color      := DARK_BG;
  WizardForm.Font.Color := TEXT_WHITE;
  WizardForm.Font.Name  := 'Consolas';
  WizardForm.Font.Size  := 9;
  WizardForm.MainPanel.Color := DARK_BG;

  // Resimlerin arka planını karart
  WizardForm.WizardBitmapImage.BackColor := DARK_BG;
  WizardForm.WizardSmallBitmapImage.BackColor := DARK_BG;

  // Başlık yazıları
  WizardForm.PageNameLabel.Font.Color := NEON_CYAN;
  WizardForm.PageNameLabel.Font.Name  := 'Consolas';
  WizardForm.PageNameLabel.Font.Size  := 11;
  WizardForm.PageNameLabel.Font.Style := [fsBold];
  WizardForm.PageDescriptionLabel.Font.Color := TEXT_MUTED;
  WizardForm.PageDescriptionLabel.Font.Name  := 'Consolas';

  // İç sayfalar ve Ana Sayfalar
  WizardForm.InnerPage.Color := DARK_BG;
  WizardForm.WelcomePage.Color := DARK_BG;
  WizardForm.FinishedPage.Color := DARK_BG;

  // Butonlar
  StyleBtn(WizardForm.NextButton);
  StyleBtn(WizardForm.BackButton);
  StyleBtn(WizardForm.CancelButton);
  WizardForm.NextButton.Caption   := '[ DEVAM >> ]';
  WizardForm.BackButton.Caption   := '[ << GERI ]';
  WizardForm.CancelButton.Caption := '[ X CIKIS ]';

  // Klasör seçimi
  WizardForm.DirEdit.Color      := DARK_CARD;
  WizardForm.DirEdit.Font.Color := NEON_CYAN;
  WizardForm.DirEdit.Font.Name  := 'Consolas';
  StyleBtn(WizardForm.DirBrowseButton);
  WizardForm.DirBrowseButton.Caption := '[ ... ]';

  // Özet ekranı
  WizardForm.ReadyMemo.Color      := DARK_CARD;
  WizardForm.ReadyMemo.Font.Color := NEON_CYAN;
  WizardForm.ReadyMemo.Font.Name  := 'Consolas';
end;

procedure CurPageChanged(CurPageID: Integer);
var
  I: Integer;
begin
  WizardForm.Color := DARK_BG;
  WizardForm.InnerPage.Color := DARK_BG;
  
  if CurPageID = wpWelcome then
  begin
    WizardForm.WelcomePage.Color := DARK_BG;
  end;

  if CurPageID = wpFinished then
  begin
    WizardForm.FinishedPage.Color := DARK_BG;
    WizardForm.FinishedHeadingLabel.Font.Color := NEON_CYAN;
    WizardForm.FinishedLabel.Font.Color := TEXT_WHITE;
    WizardForm.RunList.Color := DARK_BG;
    WizardForm.RunList.Font.Color := TEXT_WHITE;
    for I := 0 to WizardForm.RunList.Items.Count - 1 do
      WizardForm.RunList.ItemEnabled[I] := True;
  end;
end;
