using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SytexLCore.Services;

public sealed class GameDetectedEventArgs : EventArgs
{
    public string ProcessName { get; init; } = "";
    public string WindowTitle  { get; init; } = "";
    public IntPtr WindowHandle { get; init; }
}

public sealed class GameDetector : IDisposable
{
    public event EventHandler<GameDetectedEventArgs>? GameFound;
    public event EventHandler? GameLost;

    private System.Threading.Timer? _timer;
    private string? _currentGame;
    private Process? _currentGameProcess;
    private bool _disposed;

    // Asla oyun olmayan sistem ve masaüstü uygulama süreçleri
    private static readonly HashSet<string> _systemExcluded = new(StringComparer.OrdinalIgnoreCase)
    {
        "sytex-lcore", "sytex",
        "dwm", "csrss", "wininit", "winlogon", "services", "lsass",
        "taskhostw", "sihost", "shellexperiencehost", "searchhost",
        "systemsettings", "textinputhost",
        "nvidia share", "nvcontainer", "nvdisplay.container",
        "explorer", "taskmgr", "cmd", "powershell", "conhost", "wt",
        "chrome", "firefox", "msedge", "opera", "brave", "discord",
        "notepad", "notepad++", "spotify", "steam", "steamwebhelper",
        "epicgameslauncher", "galaxyclient", "calculator", "calc",
        "devenv", "code", "outlook", "teams", "slack", "control",
        "rundll32", "searchapp", "hxtsr", "applicationframehost"
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int  GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);

    public void Start() => _timer = new System.Threading.Timer(_ => Scan(), null, 0, 1500);

    private void Scan()
    {
        if (_disposed) return;
        try
        {
            // Eğer daha önce algılanmış bir oyun varsa ve hala arka planda çalışıyorsa, oyunu hemen kaybetme!
            if (_currentGameProcess != null)
            {
                try
                {
                    _currentGameProcess.Refresh();
                    if (_currentGameProcess.HasExited)
                    {
                        _currentGameProcess = null;
                        ReportLost();
                    }
                }
                catch
                {
                    _currentGameProcess = null;
                    ReportLost();
                }
            }

            var fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || !IsWindowVisible(fg)) return; // Odak kaybolduğunda durumunu koru!

            GetWindowThreadProcessId(fg, out uint pid);
            if (pid == 0) return;

            Process? proc;
            try { proc = Process.GetProcessById((int)pid); }
            catch { return; }

            string name = proc.ProcessName;
            
            // Kendimizi veya sistem araçlarını yoksay
            if (name.Equals("sytex-lcore", StringComparison.OrdinalIgnoreCase) || 
                name.Equals("sytex", StringComparison.OrdinalIgnoreCase)) 
            {
                return; 
            }

            var sb = new StringBuilder(256);
            GetWindowText(fg, sb, 256);
            string title = sb.ToString().Trim();
            if (string.IsNullOrEmpty(title)) return;

            // Web tarayıcıları listesi (sadece pencereli modda elenirler, tam ekranda veya YouTube izlerken serbest bırakılırlar)
            bool isBrowser = name.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("firefox", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("msedge", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("opera", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("brave", StringComparison.OrdinalIgnoreCase);

            bool isYouTube = title.Contains("youtube", StringComparison.OrdinalIgnoreCase);

            if (_systemExcluded.Contains(name) && !isBrowser) return; // Sistem araçları odağa geldiğinde mevcut oyunu bozma

            if (GetWindowRect(fg, out RECT rect))
            {
                int w = rect.Right - rect.Left;
                int h = rect.Bottom - rect.Top;
                
                int sw = GetSystemMetrics(0); // SM_CXSCREEN
                int sh = GetSystemMetrics(1); // SM_CYSCREEN
                
                // Tarayıcının tam ekranda (F11 veya youtube tam ekran) olup olmadığını kontrol et
                bool isFullScreen = (rect.Left <= 15 && rect.Top <= 15 && rect.Right >= sw - 15 && rect.Bottom >= sh - 15) || 
                                     (w >= sw * 0.95 && h >= sh * 0.95);

                // Oyunlar kesinlikle küçük pencereli araçlardan büyüktür.
                // Tarayıcıları ise sadece tam ekrandayken veya YouTube izlerken algıla (böylece Youtube testleri kusursuz çalışır!)
                if (w < 600 || h < 400 || (isBrowser && !isFullScreen && !isYouTube))
                {
                    return; // Mevcut oyunu kaybetme, sadece odağın geçici olarak başka yere kaydığını kabul et
                }
            }

            if (_currentGame != name)
            {
                _currentGame = name;
                _currentGameProcess = proc;
                GameFound?.Invoke(this, new GameDetectedEventArgs
                {
                    ProcessName  = name,
                    WindowTitle  = title,
                    WindowHandle = fg
                });
            }
        }
        catch { }
    }

    private void ReportLost()
    {
        if (_currentGame == null) return;
        _currentGame = null;
        GameLost?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() { _disposed = true; _timer?.Dispose(); }
}
