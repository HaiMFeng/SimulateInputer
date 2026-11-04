using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SimulateInputer.Services;

/// <summary>
/// 通过 SendInput 逐字模拟键盘输入（Unicode 事件，可输入中文、emoji 等）。
/// </summary>
public static class InputSimulator
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;

    private const int VkTab = 0x09;
    private const int VkReturn = 0x0D;

    private const int ProgressStride = 8;

    private static readonly int[] ModifierVirtualKeys = [0x11 /*Ctrl*/, 0x10 /*Shift*/, 0x12 /*Alt*/, 0x5B /*LWin*/, 0x5C /*RWin*/];

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
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Msg;
        public ushort ParamLow;
        public ushort ParamHigh;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, [In] Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    /// <summary>把各种换行统一成 \n，避免目标程序出现多余空行。</summary>
    public static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    /// <summary>
    /// 等待用户松开 Ctrl / Shift / Alt / Win，避免热键的修饰键污染输入内容。
    /// </summary>
    public static async Task WaitForModifiersReleasedAsync(CancellationToken token, int timeoutMs = 4000)
    {
        if (!AnyModifierDown()) return;

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(20, token);
            if (!AnyModifierDown()) return;
        }
    }

    /// <summary>
    /// 逐字输入文本。<paramref name="delayMs"/> 为每个字符之间的间隔，0 表示尽可能快。
    /// </summary>
    public static async Task TypeAsync(string text, int delayMs, Action<int>? progress, CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();
        long plannedElapsed = 0;

        for (int i = 0; i < text.Length; i++)
        {
            token.ThrowIfCancellationRequested();

            char current = text[i];
            if (char.IsHighSurrogate(current) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                SendUnicode(current, text[i + 1]);
                i++;
            }
            else if (current == '\n')
            {
                SendVirtualKey(VkReturn);
            }
            else if (current == '\t')
            {
                SendVirtualKey(VkTab);
            }
            else if (char.IsControl(current))
            {
                // 其它控制字符直接跳过
            }
            else
            {
                SendUnicode(current, null);
            }

            if (progress is not null && ((i % ProgressStride == 0) || i == text.Length - 1))
            {
                progress(Math.Min(i + 1, text.Length));
            }

            if (delayMs > 0 && i < text.Length - 1)
            {
                plannedElapsed += delayMs;
                int wait = (int)(plannedElapsed - stopwatch.ElapsedMilliseconds);
                if (wait > 0)
                {
                    await Task.Delay(wait, token);
                }
            }
        }
    }

    private static bool AnyModifierDown()
    {
        foreach (int virtualKey in ModifierVirtualKeys)
        {
            if ((GetAsyncKeyState(virtualKey) & 0x8000) != 0) return true;
        }

        return false;
    }

    private static void SendUnicode(char first, char? second)
    {
        int count = second.HasValue ? 2 : 1;
        var inputs = new Input[count * 2];

        for (int i = 0; i < count; i++)
        {
            ushort scanCode = (ushort)(i == 0 ? first : second!.Value);
            inputs[i] = CreateUnicodeInput(scanCode, keyUp: false);
            inputs[count + i] = CreateUnicodeInput(scanCode, keyUp: true);
        }

        Send(inputs);
    }

    private static void SendVirtualKey(int virtualKey)
    {
        Input[] inputs =
        [
            CreateKeyInput((ushort)virtualKey, keyUp: false),
            CreateKeyInput((ushort)virtualKey, keyUp: true)
        ];

        Send(inputs);
    }

    private static Input CreateUnicodeInput(ushort scanCode, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = 0,
                ScanCode = scanCode,
                Flags = keyUp ? KeyEventUnicode | KeyEventKeyUp : KeyEventUnicode,
                Time = 0,
                ExtraInfo = IntPtr.Zero
            }
        }
    };

    private static Input CreateKeyInput(ushort virtualKey, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                ScanCode = 0,
                Flags = keyUp ? KeyEventKeyUp : 0,
                Time = 0,
                ExtraInfo = IntPtr.Zero
            }
        }
    };

    private static void Send(Input[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent == (uint)inputs.Length) return;

        int code = Marshal.GetLastWin32Error();
        throw new InvalidOperationException(code == 5
            ? "目标窗口权限高于本程序，系统拦截了模拟输入。请尝试以管理员身份运行本程序。"
            : $"模拟输入被系统拒绝（错误码 {code}）。");
    }
}
