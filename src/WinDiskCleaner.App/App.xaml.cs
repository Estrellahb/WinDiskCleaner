using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WinDiskCleaner.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowFatalError(e.Exception);
        e.Handled = true;
        Shutdown(1);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            ShowFatalError(exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowFatalError(e.Exception);
        e.SetObserved();
    }

    private static void ShowFatalError(Exception exception)
    {
        var unwrapped = UnwrapException(exception);
        var logPath = WriteCrashLog(unwrapped);
        var message = new StringBuilder()
            .AppendLine("WinDiskCleaner 启动或运行时发生错误。")
            .AppendLine()
            .AppendLine($"错误类型：{unwrapped.GetType().FullName}")
            .AppendLine($"错误信息：{unwrapped.Message}")
            .AppendLine()
            .AppendLine($"错误日志：{logPath}")
            .AppendLine()
            .AppendLine("可将以上内容或日志文件提交到项目 Issue 页面。")
            .ToString();

        MessageBox.Show(message, "WinDiskCleaner 错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static Exception UnwrapException(Exception exception)
    {
        while (exception is TargetInvocationException && exception.InnerException is not null)
        {
            exception = exception.InnerException;
        }

        return exception;
    }

    private static string WriteCrashLog(Exception exception)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                appData = Path.Combine(Path.GetTempPath(), "WinDiskCleaner");
            }

            var logDir = Path.Combine(appData, "WinDiskCleaner", "Logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(logPath, exception.ToString(), Encoding.UTF8);
            return logPath;
        }
        catch
        {
            return "日志写入失败";
        }
    }
}
