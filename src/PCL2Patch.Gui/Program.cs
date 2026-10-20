using System.IO;
using System.Windows;

namespace PCL2Patch.Gui;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        Log("Main enter");
        try
        {
            var app = new App();
            Log("App created");
            app.InitializeComponent();
            Log("InitializeComponent done");
            app.Run();
            Log("Run exited");
        }
        catch (Exception ex)
        {
            Log("FATAL: " + ex);
            throw;
        }
    }

    internal static void Log(string message)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "startup.log");
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\r\n");
        }
        catch
        {
            // 日志失败忽略
        }
    }
}