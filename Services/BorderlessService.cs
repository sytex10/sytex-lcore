using System.Runtime.InteropServices;

namespace SytexLCore.Services;

public static class BorderlessService
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;
    private const long WS_BORDER = 0x00800000L;
    private const long WS_MAXIMIZE = 0x01000000L;
    private const int SWP_FRAMECHANGED = 0x0020;
    private const int SWP_NOACTIVATE = 0x0010;
    private const int SWP_SHOWWINDOW = 0x0040;
    private const IntPtr HWND_TOP = 0;

    [DllImport("user32.dll")] private static extern long GetWindowLong(IntPtr hWnd, int idx);
    [DllImport("user32.dll")] private static extern long SetWindowLong(IntPtr hWnd, int idx, long val);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insert, int x, int y, int w, int h, int flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] private static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    public static bool ForceBorderless(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        try
        {
            long style = GetWindowLong(hWnd, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_BORDER);
            SetWindowLong(hWnd, GWL_STYLE, style);

            int sw = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
            int sh = (int)System.Windows.SystemParameters.PrimaryScreenHeight;

            SetWindowPos(hWnd, HWND_TOP, 0, 0, sw, sh,
                SWP_FRAMECHANGED | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            return true;
        }
        catch { return false; }
    }

    public static bool IsFullscreen(IntPtr hWnd)
    {
        if (!GetWindowRect(hWnd, out RECT r)) return false;
        int sw = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
        int sh = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
        return r.Left <= 0 && r.Top <= 0 && r.Right >= sw && r.Bottom >= sh;
    }
}
