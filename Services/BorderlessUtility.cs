using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SytexLCore.Services;

public static class BorderlessUtility
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    const int GWL_STYLE = -16;
    const int WS_CAPTION = 0x00C00000;
    const int WS_THICKFRAME = 0x00040000;
    const int WS_MINIMIZEBOX = 0x00020000;
    const int WS_MAXIMIZEBOX = 0x00010000;
    const uint SWP_FRAMECHANGED = 0x0020;
    const uint SWP_NOZORDER = 0x0004;

    /// <summary>
    /// Ekrandaki en mantıklı oyunu bulur ve bordersız tam ekrana dönüştürür.
    /// </summary>
    public static string AutoForceBorderless()
    {
        IntPtr targetHwnd = IntPtr.Zero;
        string targetTitle = "";

        // Tüm pencereleri tarıyoruz
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            var title = sb.ToString();

            if (string.IsNullOrWhiteSpace(title)) return true;
            
            // Programın kendini ve sistem uygulamalarını es geç
            string lowerTitle = title.ToLower();
            if (lowerTitle.Contains("sytex") || lowerTitle == "program manager" || lowerTitle == "settings" || lowerTitle == "ayarlar")
                return true;

            GetWindowThreadProcessId(hWnd, out uint processId);
            try
            {
                var proc = Process.GetProcessById((int)processId);
                var procName = proc.ProcessName.ToLower();
                if (procName == "explorer" || procName == "chrome" || procName == "msedge" || procName == "discord" || procName == "devenv")
                    return true;
            }
            catch 
            { 
                // Erişim reddedildi (oyun yönetici olabilir). İsim filtresini atla ama pencereyi oyun olarak değerlendir.
            }

            // Pencere boyutu çok küçükse oyun değildir (Genişlik 800, Yükseklik 600'den büyük olmalı)
            if (GetWindowRect(hWnd, out RECT rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                if (width >= 800 && height >= 600)
                {
                    targetHwnd = hWnd;
                    targetTitle = title;
                    return false; // Bulduk, taramayı durdur
                }
            }
            return true;
        }, IntPtr.Zero);

        if (targetHwnd == IntPtr.Zero)
            return "❌ Açık bir oyun penceresi bulunamadı. Lütfen oyunu pencereli modda açın.";

        // Bulunan pencereyi Borderless Fullscreen yap
        int style = GetWindowLong(targetHwnd, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
        SetWindowLong(targetHwnd, GWL_STYLE, style);

        int sw = GetSystemMetrics(0);
        int sh = GetSystemMetrics(1);
        SetWindowPos(targetHwnd, IntPtr.Zero, 0, 0, sw, sh, SWP_FRAMECHANGED | SWP_NOZORDER);

        return $"🎮 '{targetTitle}' bulundu ve tam ekran yapıldı!";
    }
}
