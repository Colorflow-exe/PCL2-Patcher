using System.IO;
using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

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

        // 在创建窗口之前应用主题，避免首帧使用默认主题、点击后才重绘
        ApplyStartupTheme();

        var window = new MainWindow();
        window.Show();
        window.Activate();
    }

    private static void ApplyStartupTheme()
    {
        var mode = "system";
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\PCL2Patch");
            mode = key?.GetValue("Theme") as string ?? "system";
        }
        catch
        {
            // 读取失败时跟随系统
        }

        try
        {
            switch (mode)
            {
                case "light":
                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    break;
                case "dark":
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    break;
                default:
                    ApplicationThemeManager.ApplySystemTheme();
                    break;
            }
        }
        catch
        {
            // 主题应用失败时保持默认
        }
    }

    static void Log(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
        }
        catch
        {
            // 日志失败忽略
        }
    }
}