using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using Hardcodet.Wpf.TaskbarNotification;
using SytexLCore.Models;
using SytexLCore.Services;
using WpfRect = System.Windows.Rect;

namespace SytexLCore;

public partial class MainWindow : Window
{
    private readonly GameDetector _detector = new();
    private readonly ProfileService _profileSvc = new();
    private readonly TranslationService _translator = new();
    
    private TaskbarIcon? _trayIcon;
    private OverlayWindow? _overlay;
    private MiniMenuWindow? _miniMenu;
    private ShortcutService? _shortcutSvc;
    private CropWindow? _activeCropper;
    
    private GameProfile _currentProfile = new() { ProcessName = "manual", DisplayName = "Manuel" };
    private int _totalTranslatedCount = 0;

    private static readonly (string label, string code)[] _sourceLangs =
    {
        ("Otomatik", "auto"), ("İngilizce", "en"), ("Japonca", "ja"), ("Korece", "ko"), 
        ("Çince", "zh-CN"), ("Rusça", "ru"), ("Fransızca", "fr"), 
        ("Almanca", "de"), ("İspanyolca", "es")
    };

    private static readonly (string label, string code)[] _targetLangs =
    {
        ("Türkçe", "tr"), ("İngilizce", "en"), ("Almanca", "de"), 
        ("Fransızca", "fr"), ("İspanyolca", "es"), ("Japonca", "ja")
    };

    public MainWindow()
    {
        // ── 0. LOGOYU ICONA DÖNÜŞTÜR VE UYGULA ──
        try
        {
            string pngPath = "Sytex L-Core Logo.png";
            string icoPath = "Sytex L-Core Logo.ico";
            if (System.IO.File.Exists(pngPath) && !System.IO.File.Exists(icoPath))
            {
                byte[] pngBytes = System.IO.File.ReadAllBytes(pngPath);
                using var ms = new System.IO.MemoryStream();
                using var bw = new System.IO.BinaryWriter(ms);

                // Header
                bw.Write((ushort)0); // Reserved
                bw.Write((ushort)1); // Type (1 = Icon)
                bw.Write((ushort)1); // Image count (1)

                // Entry
                bw.Write((byte)0);   // Width (256, represented as 0)
                bw.Write((byte)0);   // Height (256, represented as 0)
                bw.Write((byte)0);   // Color count (0 = no palette)
                bw.Write((byte)0);   // Reserved
                bw.Write((ushort)1); // Planes (1)
                bw.Write((ushort)32);// Bit count (32 bits)
                bw.Write((uint)pngBytes.Length); // Size of the PNG image
                bw.Write((uint)22);  // Offset of PNG data (6 + 16)

                // Raw PNG data
                bw.Write(pngBytes);

                bw.Flush();
                System.IO.File.WriteAllBytes(icoPath, ms.ToArray());
            }

            if (System.IO.File.Exists(icoPath))
            {
                Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath(icoPath)));
            }
        }
        catch { }

        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Activated += MainWindow_Activated;
        Deactivated += MainWindow_Deactivated;

        // ── 1. TRAY ICON KURULUMU ──
        _trayIcon = new TaskbarIcon 
        { 
            ToolTipText = "SYTEX L-Core Ultimate", 
            ContextMenu = new ContextMenu()
        };

        if (System.IO.File.Exists("Sytex L-Core Logo.ico"))
        {
            try { _trayIcon.Icon = new System.Drawing.Icon("Sytex L-Core Logo.ico"); }
            catch { _trayIcon.Icon = System.Drawing.SystemIcons.Application; }
        }
        else
        {
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        var menuShow = new MenuItem { Header = "Aç" }; 
        menuShow.Click += (_, _) => ShowMainWindow();
        var menuExit = new MenuItem { Header = "Kapat" }; 
        menuExit.Click += (_, _) => ExitApplication();
        
        _trayIcon.ContextMenu.Items.Add(menuShow);
        _trayIcon.ContextMenu.Items.Add(new Separator());
        _trayIcon.ContextMenu.Items.Add(menuExit);
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        // ── 2. DİL COMBOBOX'LARI ──
        foreach (var (label, code) in _sourceLangs)
            SourceLangBox.Items.Add(new ComboBoxItem { Content = label, Tag = code });
        SourceLangBox.SelectedIndex = 0; // Auto

        foreach (var (label, code) in _targetLangs)
            TargetLangBox.Items.Add(new ComboBoxItem { Content = label, Tag = code });
        TargetLangBox.SelectedIndex = 0; // Türkçe

        // ── 3. DETEKTÖR BAŞLATMA ──
        _detector.GameFound += (_, ev) => Dispatcher.Invoke(async () =>
        {
            _currentProfile = await _profileSvc.LoadAsync(ev.ProcessName);
            GameNameBox.Text = ev.WindowTitle;
            Log($"[SYSTEM] Oyun algılandı: '{ev.WindowTitle}' ({ev.ProcessName})");
            SyncProfileToUi();
            UpdateGameCard(ev.ProcessName, ev.WindowTitle);
        });
        _detector.GameLost += (_, _) => Dispatcher.Invoke(() =>
        {
            GameNameBox.Text = "Bekleniyor...";
            Log("[SYSTEM] Oyun bekleniyor...");
            ResetGameCard();
        });
        _detector.Start();

        // ── 4. SHORTCUT SERVISI BAŞLATMA ──
        var hwnd = new WindowInteropHelper(this).Handle;
        _shortcutSvc = new ShortcutService(hwnd);
        _shortcutSvc.OnAutoTriggered += () => Dispatcher.Invoke(ToggleAutoTranslation);
        _shortcutSvc.OnManuelTriggered += () => Dispatcher.Invoke(TriggerManualScan);
        _shortcutSvc.OnSettingsTriggered += () => Dispatcher.Invoke(ShowSettingsWindow);

        RegisterGlobalShortcuts();

        // Mini menü hazırla
        CreateMiniMenu();
    }

    private void RegisterGlobalShortcuts()
    {
        if (_shortcutSvc != null && _currentProfile != null)
        {
            _shortcutSvc.RegisterShortcuts(
                _currentProfile.ManuelHotkey,
                _currentProfile.AutoHotkey,
                _currentProfile.SettingsHotkey
            );
        }
    }

    private void SyncProfileToUi()
    {
        if (_currentProfile == null) return;

        // Kaynak dil seçimi
        for (int i = 0; i < SourceLangBox.Items.Count; i++)
        {
            if ((SourceLangBox.Items[i] as ComboBoxItem)?.Tag?.ToString() == _currentProfile.SourceLanguage)
            {
                SourceLangBox.SelectedIndex = i;
                break;
            }
        }

        // Hedef dil seçimi
        for (int i = 0; i < TargetLangBox.Items.Count; i++)
        {
            if ((TargetLangBox.Items[i] as ComboBoxItem)?.Tag?.ToString() == _currentProfile.TargetLanguage)
            {
                TargetLangBox.SelectedIndex = i;
                break;
            }
        }
    }

    private void SyncUiToProfile()
    {
        if (_currentProfile == null) return;
        _currentProfile.SourceLanguage = (SourceLangBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "auto";
        _currentProfile.TargetLanguage = (TargetLangBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "tr";
    }

    private void Log(string message)
    {
        string time = DateTime.Now.ToString("HH:mm:ss");
        TerminalLog.Text = $"[{time}] {message}\n" + TerminalLog.Text;
        if (TerminalLog.Text.Length > 2000) TerminalLog.Text = TerminalLog.Text.Substring(0, 2000);
        TerminalScroll.ScrollToTop();
    }

    private void AppendTerminal(string original, string translated)
    {
        _totalTranslatedCount++;
        TotalTranslationsLabel.Text = $"Toplam Çeviri: {_totalTranslatedCount}";

        string time = DateTime.Now.ToString("HH:mm:ss");
        TerminalLog.Text = $"[{time}] ▸ {original}\n      → {translated}\n\n" + TerminalLog.Text;
        if (TerminalLog.Text.Length > 3000) TerminalLog.Text = TerminalLog.Text.Substring(0, 3000);
        TerminalScroll.ScrollToTop();
    }

    // ── AKIŞ YÖNETİCİLERİ ───────────────────────────────────────────────────

    private void ToggleAutoTranslation()
    {
        if (_overlay == null)
        {
            StartOverlay();
        }
        else
        {
            StopOverlay();
        }
    }

    private void StartOverlay()
    {
        if (_overlay != null) return;

        SyncUiToProfile();
        
        _translator.SourceLanguage = _currentProfile.SourceLanguage;
        _translator.TargetLanguage = _currentProfile.TargetLanguage;
        _translator.Provider = TranslationProvider.GoogleGTX; // Ücretsiz, api keysiz
        
        _overlay = new OverlayWindow(_currentProfile, _translator, _currentProfile.AutoHotkey);
        _overlay.OnTextTranslated += (orig, trans) => Dispatcher.Invoke(() => AppendTerminal(orig, trans));
        _overlay.OnBack += () => Dispatcher.Invoke(StopOverlay);
        _overlay.Show();

        // Mini Menüyü aktif yap
        if (_miniMenu != null)
        {
            _miniMenu.UpdateStatusIndicator(true);
            _miniMenu.Show();
        }

        Hide();
        BottomStatusLabel.Text = "ÇEVİRİ AKTİF (F10 ile durdurun)";
        BottomStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 102));
        Log("[SYSTEM] Çeviri overlay aktif!");
    }

    private void StopOverlay()
    {
        if (_overlay == null) return;
        
        try { _overlay.Close(); } catch { }
        _overlay = null;

        if (_miniMenu != null)
        {
            _miniMenu.UpdateStatusIndicator(false);
        }

        ShowMainWindow();
        BottomStatusLabel.Text = "Hazır (F10 ile başlatın)";
        BottomStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0, 245, 255));
        Log("[SYSTEM] Çeviri overlay durduruldu.");
    }

    private void TriggerManualScan()
    {
        // Eğer zaten açık olan aktif bir seçim ekranı varsa, onu kapat! (F9 ile aç / F9 ile kapat döngüsü)
        if (_activeCropper != null)
        {
            _activeCropper.Close();
            return;
        }

        bool overlayWasRunning = _overlay != null;
        if (overlayWasRunning) StopOverlay();

        Hide();
        if (_miniMenu != null) _miniMenu.Hide();

        var cropper = new CropWindow(_currentProfile?.ManuelHotkey ?? 0x78);
        _activeCropper = cropper;

        if (IsVisible) cropper.Owner = this;
        else cropper.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        cropper.Closed += async (s, ev) =>
        {
            _activeCropper = null; // Kırpıcı kapandığı an referansı serbest bırak!

            if (cropper.SelectedRegion != WpfRect.Empty && _currentProfile != null)
            {
                var reg = cropper.SelectedRegion;

                // Güvenlik Kontrolü: Çok küçük seçimler (yanlış tık) filtrele
                if (reg.Width < 20 || reg.Height < 20)
                {
                    Log("[WARNING] Seçim alanı çok küçük, işlem iptal edildi.");
                    goto done;
                }

                // Güvenlik Payı (Padding): Sol ve sağdan 50px, üst ve alttan 15px ekstra tarama payı ekle!
                // Böylece altyazının kenarlarda kesilmesini (örn: Atreus -> treus, sometimes -> om times) tamamen engelle!
                double padX = 50;
                double padY = 15;
                
                double newX = Math.Max(0, reg.X - padX);
                double newY = Math.Max(0, reg.Y - padY);
                double newW = reg.Width + (padX * 2.0);
                double newH = reg.Height + (padY * 2.0);
                
                _currentProfile.OcrRegion = new OcrRegion { X = newX, Y = newY, Width = newW, Height = newH };
                Log($"[MANUEL] Yeni OCR bölgesi ayarlandı: X:{newX:0}, Y:{newY:0}, G:{newW:0}, Y:{newH:0}");
                await _profileSvc.SaveAsync(_currentProfile);

                // Seçim penceresinin tamamen kapanması ve ekranın yenilenmesi için bekle
                await Task.Delay(400);

                // Çeviri Balonunu (Floating Bubble) hemen seçilen alanın üstünde göster!
                var bubble = new TranslationBubbleWindow(reg, _currentProfile, _translator);
                bubble.Show();
            }

            done:
            if (overlayWasRunning)
            {
                StartOverlay();
            }
            else
            {
                // Ana uygulamaya atmıyoruz! Sadece mini menüyü gösterip odağı oraya veriyoruz.
                if (_miniMenu != null)
                {
                    _miniMenu.Show();
                    _miniMenu.Activate();
                    _miniMenu.Focus();
                }
            }
        };
        cropper.Show();
        cropper.Activate();
    }

    private void CreateMiniMenu()
    {
        _miniMenu = new MiniMenuWindow();
        
        _miniMenu.OnToggleAuto += () => Dispatcher.Invoke(ToggleAutoTranslation);
        _miniMenu.OnManualScan += () => Dispatcher.Invoke(TriggerManualScan);
        _miniMenu.OnOpenSettings += () => Dispatcher.Invoke(ShowSettingsWindow);
        _miniMenu.OnForceBorderless += () => Dispatcher.Invoke(() => BorderlessBtn_Click(null, null));
        _miniMenu.OnMinimizeToTray += () => Dispatcher.Invoke(MinimizeToTray);
        _miniMenu.OnFullClose += () => Dispatcher.Invoke(ExitApplication);
    }

    private async void ShowSettingsWindow()
    {
        bool wasRunning = _overlay != null;
        if (wasRunning) StopOverlay();

        var settings = new SettingsWindow(_currentProfile);
        if (IsVisible) settings.Owner = this;
        else settings.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        if (settings.ShowDialog() == true)
        {
            // Profili asenkron kaydet (deadlock'suz!)
            await _profileSvc.SaveAsync(_currentProfile);
            Log("[SYSTEM] Gelişmiş ayarlar başarıyla kaydedildi.");

            // Kısayolları güncelle
            RegisterGlobalShortcuts();
        }

        if (wasRunning) StartOverlay();

        // Ayarlar kapandıktan sonra kilitlenmeyi önlemek için odağı mini menüye ya da ana pencereye geri ver
        if (_miniMenu != null && _miniMenu.IsVisible)
        {
            _miniMenu.Activate();
            _miniMenu.Focus();
        }
        else
        {
            Activate();
            Focus();
        }
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (_miniMenu != null) _miniMenu.Hide();
    }

    private void MinimizeToTray()
    {
        Hide();
        if (_miniMenu != null) _miniMenu.Show();
        Log("[SYSTEM] Arka planda çalışıyor. Mini menüyü kullanabilirsiniz.");
    }

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        _detector.Dispose();
        _translator.Dispose();
        _shortcutSvc?.Dispose();

        try { _overlay?.Close(); } catch { }
        try { _miniMenu?.Close(); } catch { }

        Application.Current.Shutdown();
    }

    // ── BUTON EVENTLERI ──────────────────────────────────────────────────────

    private void StartBtn_Click(object s, RoutedEventArgs e) => ToggleAutoTranslation();

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsWindow();
    }

    private void FeedbackBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://sytex.com.tr/",
                UseShellExecute = true
            });
            Log("[SYSTEM] Geri bildirim sayfası açıldı.");
        }
        catch { }
    }

    private void BorderlessBtn_Click(object s, RoutedEventArgs e)
    {

        var res = BorderlessUtility.AutoForceBorderless();
        Log($"[BORDERLESS] {res}");
    }

    private void TitleBar_MouseDown(object s, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void MinimizeBtn_Click(object s, RoutedEventArgs e) => MinimizeToTray();

    private void CloseBtn_Click(object s, RoutedEventArgs e)
    {
        if (_currentProfile != null && _currentProfile.MinimizeOnClose)
        {
            MinimizeToTray();
        }
        else
        {
            ExitApplication();
        }
    }

    private void Window_Closing(object s, CancelEventArgs e)
    {
        if (_currentProfile != null && _currentProfile.MinimizeOnClose)
        {
            e.Cancel = true;
            MinimizeToTray();
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            MinimizeToTray();
        }
    }

    [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject([In] IntPtr hObject);

    private void UpdateGameCard(string processName, string windowTitle)
    {
        try
        {
            var nameWithoutExe = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) 
                ? processName.Substring(0, processName.Length - 4) 
                : processName;

            var processes = Process.GetProcessesByName(nameWithoutExe);
            if (processes.Length > 0)
            {
                string mainModulePath = processes[0].MainModule?.FileName ?? "";
                var imgSource = GetIconAsImageSource(mainModulePath);
                if (imgSource != null)
                {
                    GameIconImage.Source = imgSource;
                    GameTitleLabel.Text = windowTitle;
                    DefaultLogoImage.Visibility = Visibility.Collapsed;
                    DetectedGamePanel.Visibility = Visibility.Visible;
                    return;
                }
            }
        }
        catch { }

        // Hata durumunda veya bulamazsa varsayılana dön
        ResetGameCard();
    }

    private void ResetGameCard()
    {
        DefaultLogoImage.Visibility = Visibility.Visible;
        DetectedGamePanel.Visibility = Visibility.Collapsed;
    }

    private ImageSource? GetIconAsImageSource(string processPath)
    {
        try
        {
            if (System.IO.File.Exists(processPath))
            {
                using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(processPath);
                if (sysIcon != null)
                {
                    using var bitmap = sysIcon.ToBitmap();
                    var hBitmap = bitmap.GetHbitmap();
                    var wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                    
                    DeleteObject(hBitmap);
                    return wpfBitmap;
                }
            }
        }
        catch { }
        return null;
    }

    private void MainWindow_Activated(object sender, EventArgs e)
    {
        // Ana pencere öne geldiğinde mini menüyü gizle (ekranda kalabalık yapmasın)
        if (_miniMenu != null && _overlay == null) 
        {
            _miniMenu.Hide();
        }
    }

    private void MainWindow_Deactivated(object sender, EventArgs e)
    {
        // Ana pencere odağı kaybedip arka plana geçince (örneğin oyuna odaklanınca) mini menüyü otomatik göster!
        if (_miniMenu != null)
        {
            _miniMenu.Show();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        ExitApplication();
        base.OnClosed(e);
    }

    // ── DİL GEÇİŞİ ─────────────────────────────────────────────────────────
    private void LangToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        Loc.Toggle();
        UpdateUiLanguage();
    }

    private void UpdateUiLanguage()
    {
        bool isEn = Loc.Current == AppLanguage.English;

        LangToggleBtn.Content = isEn ? "🌐 EN" : "🌐 TR";
        ControlPanelLabel.Text = Loc.ControlPanel;
        DetectedGameLabel.Text = Loc.DetectedGame;
        StartBtnLabel.Text = _overlay != null ? Loc.StopBtn : Loc.StartBtn;
        BorderlessBtnLabel.Text = Loc.BorderlessBtn;
        SettingsBtnLabel.Text = Loc.SettingsBtn;
        EngineLabel.Text = Loc.EngineLabel;
        OcrLabel.Text = Loc.OcrLabel;
        RecentTitle.Text = Loc.RecentTitle;
        StatusPrefixLabel.Text = Loc.StatusLabel;
        BottomStatusLabel.Text = _overlay != null ? Loc.StatusRunning : Loc.StatusReady;
        GameNameBox.Text = GameNameBox.Text == "Bekleniyor..." || GameNameBox.Text == "Waiting..." 
            ? Loc.Waiting : GameNameBox.Text;

        // Toplam çeviri sayısını koru, sadece etiketi güncelle
        TotalTranslationsLabel.Text = Loc.TotalLabel + _totalTranslatedCount;
    }
}
