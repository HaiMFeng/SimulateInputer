using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SimulateInputer.Services;

/// <summary>
/// 基于 RegisterHotKey 的全局热键封装，任意前台程序下按下都能收到通知。
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x494E;
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _registered;
    private bool _disposed;

    /// <summary>当前已注册的热键，未注册时为空热键。</summary>
    public HotkeyDefinition Current { get; private set; } = HotkeyDefinition.Empty;

    /// <summary>热键被按下（在 UI 线程触发）。</summary>
    public event EventHandler? Pressed;

    /// <summary>
    /// 注册（或替换）热键。失败时保留原有热键并返回 false。
    /// </summary>
    public bool TryRegister(Window window, HotkeyDefinition hotkey, out string? error)
    {
        ArgumentNullException.ThrowIfNull(window);
        ObjectDisposedException.ThrowIf(_disposed, this);

        error = null;
        EnsureHook(window);

        HotkeyDefinition previous = Current;
        Unregister();

        if (hotkey.IsEmpty)
        {
            Current = HotkeyDefinition.Empty;
            return true;
        }

        if (RegisterCore(hotkey))
        {
            Current = hotkey;
            return true;
        }

        int code = Marshal.GetLastWin32Error();
        error = code == ErrorHotkeyAlreadyRegistered
            ? "该组合键已被其他程序占用"
            : $"系统拒绝注册（错误码 {code}）";

        if (!previous.IsEmpty && RegisterCore(previous))
        {
            Current = previous;
        }
        else
        {
            Current = HotkeyDefinition.Empty;
        }

        return false;
    }

    /// <summary>取消当前热键。</summary>
    public void Unregister()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
        }

        _registered = false;
        Current = HotkeyDefinition.Empty;
    }

    private bool RegisterCore(HotkeyDefinition hotkey)
    {
        if (hotkey.VirtualKey == 0) return false;

        _registered = RegisterHotKey(_hwnd, HotkeyId, hotkey.Win32Modifiers, hotkey.VirtualKey);
        return _registered;
    }

    private void EnsureHook(Window window)
    {
        if (_source is not null) return;

        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Unregister();

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        _hwnd = IntPtr.Zero;
    }
}
