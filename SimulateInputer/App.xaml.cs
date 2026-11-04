using System.Windows;
using System.Windows.Threading;

namespace SimulateInputer;

/// <summary>
/// Application 入口。
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"程序发生未处理的错误：{e.Exception.GetBaseException()}",
            "模拟输入器",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
