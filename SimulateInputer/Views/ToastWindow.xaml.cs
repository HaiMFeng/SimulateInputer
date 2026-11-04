using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SimulateInputer.Views;

/// <summary>
/// 屏幕右下角的轻量提示窗：淡入上浮，自动淡出，并且不会抢走目标窗口的焦点。
/// </summary>
public partial class ToastWindow : Window
{
    private const double ScreenMargin = 12;
    private const double StackGap = 6;
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private static readonly List<ToastWindow> ActiveToasts = [];

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private readonly DispatcherTimer _closeTimer;
    private bool _closing;

    private ToastWindow(string message, ToastKind kind, TimeSpan duration)
    {
        InitializeComponent();

        MessageText.Text = message;
        ApplyAppearance(kind);

        // 先给一个右下角的近似位置，避免首次显示时闪现在屏幕其它位置
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - 340 - ScreenMargin;
        Top = workArea.Bottom - 88 - ScreenMargin;

        _closeTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = duration };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            BeginClose();
        };

        SourceInitialized += OnSourceInitializedHandler;
        Loaded += OnLoadedHandler;
        Closed += OnClosedHandler;
    }

    /// <summary>显示一条提示，默认 2.6 秒后自动消失。</summary>
    public static void Show(string message, ToastKind kind = ToastKind.Success, int durationMs = 2600)
    {
        var toast = new ToastWindow(message, kind, TimeSpan.FromMilliseconds(durationMs));
        ActiveToasts.Add(toast);
        toast.Show();
        RepositionAll();
    }

    private void ApplyAppearance(ToastKind kind)
    {
        (string icon, string brushKey) = kind switch
        {
            ToastKind.Success => ("✓", "ToastSuccessBrush"),
            ToastKind.Info => ("i", "ToastInfoBrush"),
            ToastKind.Warning => ("!", "ToastWarningBrush"),
            _ => ("✕", "ToastErrorBrush")
        };

        IconText.Text = icon;
        IconBadge.Background = (Brush)FindResource(brushKey);
    }

    private void OnSourceInitializedHandler(object? sender, EventArgs e)
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, style | WsExNoActivate | WsExToolWindow);
    }

    private void OnLoadedHandler(object? sender, RoutedEventArgs e)
    {
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var lift = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        Root.BeginAnimation(OpacityProperty, fade);
        RootTransform.BeginAnimation(TranslateTransform.YProperty, lift);

        UpdateLayout();
        RepositionAll();
        _closeTimer.Start();
    }

    private void OnClosedHandler(object? sender, EventArgs e)
    {
        _closeTimer.Stop();
        ActiveToasts.Remove(this);
        RepositionAll();
    }

    private void BeginClose()
    {
        if (_closing) return;
        _closing = true;

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fade.Completed += (_, _) => Close();
        Root.BeginAnimation(OpacityProperty, fade);
    }

    private static void RepositionAll()
    {
        Rect workArea = SystemParameters.WorkArea;
        double bottom = workArea.Bottom - ScreenMargin;

        for (int i = ActiveToasts.Count - 1; i >= 0; i--)
        {
            ToastWindow toast = ActiveToasts[i];
            if (toast._closing) continue;

            toast.UpdateLayout();

            double width = toast.ActualWidth;
            double height = toast.ActualHeight;
            if (width <= 0 || height <= 0) continue;

            toast.Left = workArea.Right - ScreenMargin - width;
            toast.Top = bottom - height;
            bottom = toast.Top - StackGap;
        }
    }
}
