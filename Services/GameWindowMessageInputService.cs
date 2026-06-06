using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace MidiKeyPlayer.Services;

public sealed class GameWindowMessageInputService : IKeyboardInputService
{
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int SwRestore = 9;
    private const uint MapvkVkToVsc = 0;
    private IntPtr _cachedWindow;

    public async Task TapKeyAsync(Key key, int durationMs, CancellationToken cancellationToken)
    {
        PressKey(key);
        try
        {
            await Task.Delay(Math.Max(1, durationMs), cancellationToken);
        }
        finally
        {
            ReleaseKey(key);
        }
    }

    private void PressKey(Key key) => PostKey(key, keyUp: false);
    private void ReleaseKey(Key key) => PostKey(key, keyUp: true);

    private void PostKey(Key key, bool keyUp)
    {
        var hwnd = ResolveGameWindow();
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("未找到游戏窗口。请确认 Infinity Nikki / 无限暖暖 已启动，且窗口类名为 UnrealWindow。");
        }

        if (IsIconic(hwnd))
        {
            ShowWindow(hwnd, SwRestore);
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0)
        {
            return;
        }

        var scanCode = MapVirtualKey((uint)virtualKey, MapvkVkToVsc);
        var lParam = BuildKeyLParam(scanCode, keyUp);
        if (!PostMessage(hwnd, keyUp ? WmKeyUp : WmKeyDown, (nuint)virtualKey, lParam))
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"PostMessage 按键消息失败，Win32Error={error}。如果游戏以管理员运行，请也以管理员身份运行本工具。");
        }
    }

    private IntPtr ResolveGameWindow()
    {
        if (_cachedWindow != IntPtr.Zero && IsWindow(_cachedWindow))
        {
            return _cachedWindow;
        }

        var found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            var title = GetWindowText(hwnd);
            var className = GetClassName(hwnd);
            if ((title == "Infinity Nikki" || title == "InfinityNikki" || title == "无限暖暖") &&
                className == "UnrealWindow")
            {
                found = hwnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        _cachedWindow = found;
        return _cachedWindow;
    }

    private static nint BuildKeyLParam(uint scanCode, bool keyUp)
    {
        var value = 1 | ((int)scanCode << 16);
        if (keyUp)
        {
            value |= 1 << 30; // previous key state
            value |= 1 << 31; // transition state
        }

        return value;
    }

    private static string GetWindowText(IntPtr hwnd)
    {
        var builder = new StringBuilder(256);
        GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    private static string GetClassName(IntPtr hwnd)
    {
        var builder = new StringBuilder(256);
        GetClassName(hwnd, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
}
