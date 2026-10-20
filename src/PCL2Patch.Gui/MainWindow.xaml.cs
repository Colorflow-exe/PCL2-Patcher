using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PCL2Patch.Core;
using Wpf.Ui.Appearance;

namespace PCL2Patch.Gui;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        LoadTheme();
    }

    // ---------- 主题 ----------

    private void LoadTheme()
    {
        var index = 2; // 默认跟随系统
        try
        {
            var path = ThemeSettingsPath;
            if (File.Exists(path))
            {
                index = File.ReadAllText(path).Trim().ToLowerInvariant() switch
                {
                    "light" => 0,
                    "dark" => 1,
                    _ => 2
                };
            }
        }
        catch
        {
            // 读取失败时保持默认
        }

        ThemeBox.SelectedIndex = index;
        ApplyTheme(index);
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeBox.SelectedIndex >= 0)
            ApplyTheme(ThemeBox.SelectedIndex);
    }

    private void ApplyTheme(int index)
    {
        try
        {
            if (index == 2)
            {
                // 跟随系统：监听系统主题变化
                SystemThemeWatcher.Watch(this);
                ApplicationThemeManager.ApplySystemTheme();
            }
            else
            {
                SystemThemeWatcher.UnWatch(this);
                ApplicationThemeManager.Apply(index == 0 ? ApplicationTheme.Light : ApplicationTheme.Dark);
            }
        }
        catch
        {
            // 主题切换失败不影响主流程
        }

        try
        {
            File.WriteAllText(ThemeSettingsPath, index switch { 0 => "light", 1 => "dark", _ => "system" });
        }
        catch
        {
            // 保存失败忽略
        }
    }

    private static string ThemeSettingsPath
    {
        get
        {
            // 优先 exe 同目录（便携），失败回退到临时目录
            var exeDir = Path.Combine(AppContext.BaseDirectory, "theme.txt");
            try
            {
                File.AppendAllText(exeDir, string.Empty);
                return exeDir;
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "PCL2Patch-theme.txt");
            }
        }
    }

    // ---------- 补丁 ----------

    private void BrowseOfficial_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择官方 PCL2 程序",
            Filter = "PCL2 程序 (*.exe)|*.exe|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        OfficialBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(OutputBox.Text))
            OutputBox.Text = SuggestOutput(dialog.FileName);
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var seed = string.IsNullOrWhiteSpace(OfficialBox.Text) ? "PCL2.exe" : OfficialBox.Text;
        var dialog = new SaveFileDialog
        {
            Title = "选择输出路径",
            Filter = "PCL2 程序 (*.exe)|*.exe",
            FileName = Path.GetFileName(SuggestOutput(seed))
        };
        if (dialog.ShowDialog(this) == true)
            OutputBox.Text = dialog.FileName;
    }

    private async void Patch_Click(object sender, RoutedEventArgs e)
    {
        var official = OfficialBox.Text.Trim();
        if (!File.Exists(official))
        {
            MessageBox.Show(this, "请选择官方 PCL2 程序。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var output = string.IsNullOrWhiteSpace(OutputBox.Text) ? SuggestOutput(official) : OutputBox.Text.Trim();
        OutputBox.Text = output;

        PatchButton.IsEnabled = false;
        LogBox.Clear();
        StatusText.Text = "正在修补…";
        AppendLog($"官方程序：{official}");
        AppendLog($"输出路径：{output}");
        AppendLog(string.Empty);

        try
        {
            await Task.Run(() => PatchEngine.Apply(official, output, AppendLog));
            StatusText.Text = "完成";
            AppendLog(string.Empty);
            AppendLog($"修补完成：{output}");
            MessageBox.Show(this, $"修补完成：\n{output}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = "失败";
            AppendLog($"失败：{ex.Message}");
            MessageBox.Show(this, $"修补失败：\n{ex.Message}", "失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PatchButton.IsEnabled = true;
        }
    }

    private static string SuggestOutput(string officialPath)
    {
        var dir = Path.GetDirectoryName(officialPath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(officialPath);
        return Path.Combine(dir, name + "_patched.exe");
    }

    private void AppendLog(string line)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendLog(line));
            return;
        }
        LogBox.AppendText(line + Environment.NewLine);
        LogBox.ScrollToEnd();
    }
}
