using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace SytexLCore;

public partial class MiniMenuWindow : Window
{
    public event Action? OnToggleAuto;
    public event Action? OnManualScan;
    public event Action? OnOpenSettings;
    public event Action? OnMinimizeToTray;
    public event Action? OnFullClose;

    private bool _isActive = false;
    private DispatcherTimer? _topmostTimer;

    // ── Win32 Topmost Zorlayıcı ──
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insert, int x, int y, int w, int h, int flags);
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOMOVE = 0x0002;
    private const int SWP_NOACTIVATE = 0x0010;

    public MiniMenuWindow()
    {
        InitializeComponent();
        UpdateStatusIndicator(false);

        // Oyunlar tam ekrana geçtiğinde dahi mini menünün her zaman en üstte kalmasını sağlayan siber döngü
        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _topmostTimer.Tick += (_, _) => SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
            _topmostTimer.Start();
        };

        Closed += (s, e) =>
        {
            _topmostTimer?.Stop();
        };
    }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    public void UpdateStatusIndicator(bool active)
    {
        _isActive = active;
        if (StatusDot == null) return;

        if (active)
        {
            StatusDot.Background = new SolidColorBrush(Color.FromRgb(0, 255, 102)); // Neon Yeşil
            if (DotGlow != null) DotGlow.Color = Color.FromRgb(0, 255, 102);
        }
        else
        {
            StatusDot.Background = new SolidColorBrush(Color.FromRgb(255, 0, 85)); // Neon Pembe/Kırmızı
            if (DotGlow != null) DotGlow.Color = Color.FromRgb(255, 0, 85);
        }
    }

    private void AutoBtn_Click(object sender, RoutedEventArgs e)
    {
        OnToggleAuto?.Invoke();
    }

    private void ManualBtn_Click(object sender, RoutedEventArgs e)
    {
        OnManualScan?.Invoke();
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        OnOpenSettings?.Invoke();
    }

    private void TrayBtn_Click(object sender, RoutedEventArgs e)
    {
        OnMinimizeToTray?.Invoke();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        OnFullClose?.Invoke();
    }
}
