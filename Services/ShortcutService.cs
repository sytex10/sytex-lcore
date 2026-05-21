using System.Runtime.InteropServices;
using System.Windows.Input;

namespace SytexLCore.Services;

public sealed class ShortcutService : IDisposable
{
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookID = IntPtr.Zero;

    public event Action? OnManuelTriggered;
    public event Action? OnAutoTriggered;
    public event Action? OnSettingsTriggered;

    private uint _manuelKey = 0x78;   // F9
    private uint _autoKey = 0x77;     // F8
    private uint _settingsKey = 0x76; // F7

    public ShortcutService(IntPtr hwnd)
    {
        _proc = HookCallback;
        _hookID = SetHook(_proc);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    private IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            IntPtr hModule = GetModuleHandle(curModule.ModuleName);
            if (hModule == IntPtr.Zero && curModule.FileName != null)
            {
                hModule = LoadLibrary(curModule.FileName);
            }
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, hModule, 0);
        }
    }

    public void RegisterShortcuts(uint manuel, uint auto, uint settings)
    {
        _manuelKey = manuel;
        _autoKey = auto;
        _settingsKey = settings;
    }

    public void UnregisterAll()
    {
        // Hook tabanlı olduğu için ayrı bir unregister işlemine gerek yok, hook her şeyi yönetir.
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam);
            uint vk = (uint)vkCode;

            if (vk == _manuelKey)
            {
                OnManuelTriggered?.Invoke();
            }
            else if (vk == _autoKey)
            {
                OnAutoTriggered?.Invoke();
            }
            else if (vk == _settingsKey)
            {
                OnSettingsTriggered?.Invoke();
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
        }
    }
}
