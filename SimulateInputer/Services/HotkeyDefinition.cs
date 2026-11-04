using System.Windows.Input;

namespace SimulateInputer.Services;

/// <summary>
/// 描述一组热键（修饰键 + 主键）。
/// </summary>
public sealed record HotkeyDefinition(ModifierKeys Modifiers, Key Key)
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    public static HotkeyDefinition Empty { get; } = new(ModifierKeys.None, Key.None);

    public bool IsEmpty => Key == Key.None;

    /// <summary>转换为 RegisterHotKey 需要的修饰键位标志。</summary>
    public uint Win32Modifiers
    {
        get
        {
            uint value = ModNoRepeat;
            if (Modifiers.HasFlag(ModifierKeys.Alt)) value |= ModAlt;
            if (Modifiers.HasFlag(ModifierKeys.Control)) value |= ModControl;
            if (Modifiers.HasFlag(ModifierKeys.Shift)) value |= ModShift;
            if (Modifiers.HasFlag(ModifierKeys.Windows)) value |= ModWin;
            return value;
        }
    }

    /// <summary>主键对应的虚拟键码。</summary>
    public uint VirtualKey => (uint)KeyInterop.VirtualKeyFromKey(Key);

    /// <summary>只有功能键才允许不带修饰键。</summary>
    public static bool IsFunctionKey(Key key)
    {
        int code = (int)key;
        return code >= (int)Key.F1 && code <= (int)Key.F24;
    }

    public override string ToString()
    {
        if (IsEmpty) return string.Empty;

        var parts = new List<string>(5);
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(DescribeKey(Key));
        return string.Join(" + ", parts);
    }

    public static string DescribeModifiers(ModifierKeys modifiers)
    {
        var parts = new List<string>(4);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        return string.Join(" + ", parts);
    }

    public static string DescribeKey(Key key)
    {
        int code = (int)key;
        if (code >= (int)Key.D0 && code <= (int)Key.D9) return (code - (int)Key.D0).ToString();
        if (code >= (int)Key.NumPad0 && code <= (int)Key.NumPad9) return "Num " + (code - (int)Key.NumPad0);

        return key switch
        {
            Key.OemPlus => "=",
            Key.OemMinus => "-",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemTilde => "`",
            Key.Space => "Space",
            Key.Return => "Enter",
            Key.Escape => "Esc",
            Key.Back => "Backspace",
            Key.Next => "PageDown",
            Key.Prior => "PageUp",
            _ => key.ToString()
        };
    }
}
