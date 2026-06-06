using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace MidiKeyPlayer.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x4648;
    private const int WmHotkey = 0x0312;
    private HwndSource? _source;
    private IntPtr _handle;

    public event Action? StopRequested;

    public void Register(Window window, Key key = Key.F8)
    {
        _handle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);
        RegisterHotKey(_handle, HotkeyId, 0, (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            UnregisterHotKey(_handle, HotkeyId);
        }

        _source?.RemoveHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            StopRequested?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
