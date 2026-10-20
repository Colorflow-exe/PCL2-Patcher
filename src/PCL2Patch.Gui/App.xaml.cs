using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace PCL2Patch.Gui;

public partial class App : Application
{
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, e) =>
        {
            Log(e.Exception);
            e.Handled = true;
        };
    }

    static void Log(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}
