using System.IO;
using System.Windows;

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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Program.Log("OnStartup");
        var window = new MainWindow();
        Program.Log("MainWindow created");
        window.Show();
        Program.Log("MainWindow shown");
    }

    static void Log(Exception? ex)
    {
        if (ex is null) return;
        Program.Log("UNHANDLED: " + ex);
    }
}
