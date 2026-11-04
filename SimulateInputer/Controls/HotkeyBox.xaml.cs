using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using SimulateInputer.Services;

namespace SimulateInputer.Controls;

/// <summary>
/// 热键录制框：获得焦点后按下组合键即可完成设置，Delete / Backspace / Esc 清除。
/// </summary>
public partial class HotkeyBox : UserControl
{
    private HotkeyDefinition _hotkey = HotkeyDefinition.Empty;
    private ModifierKeys _heldModifiers = ModifierKeys.None;

    public HotkeyBox()
    {
        InitializeComponent();
        RefreshDisplay();
    }

    /// <summary>当前热键。直接赋值不会触发 <see cref="HotkeyChanged"/>。</summary>
    public HotkeyDefinition Hotkey
    {
        get => _hotkey;
        set
        {
            _hotkey = value ?? HotkeyDefinition.Empty;
            RefreshDisplay();
        }
    }

    /// <summary>用户通过键盘修改了热键。</summary>
    public event EventHandler? HotkeyChanged;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        e.Handled = true;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        _heldModifiers = Keyboard.Modifiers;
        RefreshDisplay();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        _heldModifiers = ModifierKeys.None;
        RefreshDisplay();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!IsKeyboardFocusWithin) return;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Tab 留给焦点导航
        if (key == Key.Tab) return;

        e.Handled = true;

        if (IsModifierKey(key))
        {
            _heldModifiers = Keyboard.Modifiers;
            RefreshDisplay();
            return;
        }

        if (key is Key.Back or Key.Delete or Key.Escape)
        {
            _heldModifiers = ModifierKeys.None;
            ApplyHotkey(HotkeyDefinition.Empty);
            return;
        }

        if (key is Key.None or Key.ImeProcessed or Key.DeadCharProcessed) return;

        ModifierKeys modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None && !HotkeyDefinition.IsFunctionKey(key))
        {
            FlashError();
            return;
        }

        _heldModifiers = ModifierKeys.None;
        ApplyHotkey(new HotkeyDefinition(modifiers, key));
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        if (!IsKeyboardFocusWithin) return;

        _heldModifiers = Keyboard.Modifiers;
        RefreshDisplay();
    }

    private void ApplyHotkey(HotkeyDefinition hotkey)
    {
        _hotkey = hotkey;
        RefreshDisplay();
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsModifierKey(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt
        or Key.LWin or Key.RWin;

    private void RefreshDisplay()
    {
        string text = _hotkey.ToString();
        bool hasValue = text.Length > 0;

        ValueText.Text = text;
        ValueText.Visibility = hasValue ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderText.Visibility = hasValue ? Visibility.Collapsed : Visibility.Visible;
        LockText.Visibility = IsKeyboardFocusWithin ? Visibility.Visible : Visibility.Collapsed;

        if (hasValue) return;

        if (!IsKeyboardFocusWithin)
        {
            PlaceholderText.Text = "点击此处，然后按下组合键";
            return;
        }

        _heldModifiers = Keyboard.Modifiers;
        string modifierText = HotkeyDefinition.DescribeModifiers(_heldModifiers);
        PlaceholderText.Text = string.IsNullOrEmpty(modifierText) ? "请按下组合键…" : modifierText + " + …";
    }

    private void FlashError()
    {
        ErrorBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(900))
        {
            BeginTime = TimeSpan.FromMilliseconds(120)
        });
    }
}
