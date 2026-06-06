using System.Runtime.InteropServices;
using System.Windows.Input;

namespace MidiKeyPlayer.Services;

public sealed class KeyboardInputService : IKeyboardInputService
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;

    public void PressKey(Key key) => SendKey(key, keyUp: false);
    public void ReleaseKey(Key key) => SendKey(key, keyUp: true);

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

    private static void SendKey(Key key, bool keyUp)
    {
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0)
        {
            return;
        }

        var input = new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                KeyboardInput = new KeyboardInput
                {
                    VirtualKey = (ushort)virtualKey,
                    Flags = keyUp ? KeyEventFKeyUp : 0
                }
            }
        };

        var sent = SendInput(1, [input], Marshal.SizeOf<Input>());
        if (sent == 0)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"SendInput 调用失败，Win32Error={error}。目标程序可能不接受普通模拟输入，或当前进程权限低于目标窗口。");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    // INPUT 的第二个字段在 Win32 中是 union。必须保留最大成员 MOUSEINPUT 的空间，
    // 否则 cbSize 过小会导致 SendInput 返回 0，常见错误码为 87。
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput MouseInput;

        [FieldOffset(0)]
        public KeyboardInput KeyboardInput;

        [FieldOffset(0)]
        public HardwareInput HardwareInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParamL;
        public ushort ParamH;
    }
}
