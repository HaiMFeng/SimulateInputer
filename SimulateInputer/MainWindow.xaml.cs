using System.Windows;
using System.Windows.Input;
using SimulateInputer.Services;
using SimulateInputer.Views;

namespace SimulateInputer;

/// <summary>
/// 主窗口：录入文本 / 设置热键与延迟，并通过全局热键触发逐字模拟输入。
/// </summary>
public partial class MainWindow : Window
{
    private const int DefaultDelayMs = 20;
    private const int MaxDelayMs = 5000;

    private static readonly HotkeyDefinition DefaultHotkey =
        new(ModifierKeys.Control, Key.J);

    private readonly GlobalHotkeyService _hotkeyService = new();

    private CancellationTokenSource? _typingCts;
    private HotkeyDefinition _registeredHotkey = HotkeyDefinition.Empty;
    private bool _isTyping;
    private int _totalCount;

    public MainWindow()
    {
        InitializeComponent();

        _hotkeyService.Pressed += OnHotkeyPressed;
        DelayBox.Text = DefaultDelayMs.ToString();

        DataObject.AddPastingHandler(DelayBox, OnDelayPasting);
    }

    /// <summary>当前生效的逐字延迟。</summary>
    private int DelayMs => int.TryParse(DelayBox.Text, out int value)
        ? Math.Clamp(value, 0, MaxDelayMs)
        : DefaultDelayMs;

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        HotkeyEditor.Hotkey = TryApplyHotkey(DefaultHotkey) ? DefaultHotkey : HotkeyDefinition.Empty;
        UpdateIdleStatus();
        TextEditor.Focus();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        CancelTyping();
        _hotkeyService.Dispose();
    }

    // ---------- 热键 ----------

    private void OnHotkeyChanged(object? sender, EventArgs e)
    {
        HotkeyDefinition requested = HotkeyEditor.Hotkey;

        if (!TryApplyHotkey(requested))
        {
            // 注册失败，恢复为当前真正生效的热键
            HotkeyEditor.Hotkey = _registeredHotkey;
            return;
        }

        ToastWindow.Show(
            requested.IsEmpty ? "已清除热键，当前无法用热键触发输入" : $"热键已更新为 {requested}",
            requested.IsEmpty ? ToastKind.Warning : ToastKind.Info,
            1800);
    }

    private bool TryApplyHotkey(HotkeyDefinition hotkey)
    {
        if (hotkey.IsEmpty)
        {
            _hotkeyService.Unregister();
            _registeredHotkey = HotkeyDefinition.Empty;
            UpdateIdleStatus();
            return true;
        }

        if (_hotkeyService.TryRegister(this, hotkey, out string? error))
        {
            _registeredHotkey = hotkey;
            UpdateIdleStatus();
            return true;
        }

        _registeredHotkey = _hotkeyService.Current;
        UpdateIdleStatus();
        ToastWindow.Show($"无法设置热键 {hotkey}：{error}", ToastKind.Error, 4000);
        return false;
    }

    // ---------- 触发与模拟输入 ----------

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_isTyping)
        {
            CancelTyping();
            return;
        }

        if (IsActive)
        {
            ToastWindow.Show("请先把光标放到目标程序的输入框，再按热键", ToastKind.Warning);
            return;
        }

        await RunTypingAsync();
    }

    private async Task RunTypingAsync()
    {
        string text = InputSimulator.Normalize(TextEditor.Text);
        if (text.Length == 0)
        {
            ToastWindow.Show("文本框是空的，没有可输入的内容", ToastKind.Warning);
            return;
        }

        int delayMs = DelayMs;
        _totalCount = text.Length;
        _typingCts = new CancellationTokenSource();
        CancellationToken token = _typingCts.Token;

        SetTypingState(true);

        try
        {
            // 留出松开热键、切换焦点的时间
            await Task.Delay(150, token);
            await InputSimulator.WaitForModifiersReleasedAsync(token);

            await InputSimulator.TypeAsync(text, delayMs, UpdateTypingProgress, token);

            ToastWindow.Show($"输入完成，共 {_totalCount} 个字符", ToastKind.Success);
        }
        catch (OperationCanceledException)
        {
            ToastWindow.Show("已停止输入", ToastKind.Info, 1800);
        }
        catch (Exception ex)
        {
            ToastWindow.Show("输入失败：" + ex.Message, ToastKind.Error, 4500);
        }
        finally
        {
            SetTypingState(false);
            _typingCts?.Dispose();
            _typingCts = null;
        }
    }

    private void UpdateTypingProgress(int typedCount) =>
        StatusText.Text = $"正在输入… {typedCount} / {_totalCount}";

    private void SetTypingState(bool isTyping)
    {
        _isTyping = isTyping;
        StopButton.IsEnabled = isTyping;

        if (isTyping)
        {
            StatusText.Text = $"正在输入… 0 / {_totalCount}";
        }
        else
        {
            UpdateIdleStatus();
        }
    }

    private void CancelTyping()
    {
        try
        {
            _typingCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 已经结束
        }
    }

    private void OnStopClick(object sender, RoutedEventArgs e) => CancelTyping();

    // ---------- 状态与输入校验 ----------

    private void UpdateIdleStatus()
    {
        // XAML 解析期间控件可能尚未创建完成
        if (_isTyping || StatusText is null || TextEditor is null) return;

        string hotkeyText = _registeredHotkey.IsEmpty
            ? "未设置（请点击上方热键框设置）"
            : _registeredHotkey.ToString();
        StatusText.Text = $"就绪 · 热键 {hotkeyText} · 延迟 {DelayMs} ms/字符 · 共 {TextEditor.Text.Length} 个字符";
    }

    private void OnTextEditorChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (CharCountText is null) return;

        CharCountText.Text = $"{TextEditor.Text.Length} 个字符";
        UpdateIdleStatus();
    }

    private void OnDelayPreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsAsciiDigit);

    private void OnDelayPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            e.CancelCommand();
            return;
        }

        string? pasted = e.DataObject.GetData(DataFormats.UnicodeText) as string;
        if (string.IsNullOrEmpty(pasted) || !pasted.All(char.IsAsciiDigit))
        {
            e.CancelCommand();
        }
    }

    private void OnDelayTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateIdleStatus();

    private void OnDelayLostFocus(object sender, RoutedEventArgs e) => DelayBox.Text = DelayMs.ToString();
}
