using System.IO;
using System.Windows;
using Microsoft.Win32;
using PCL2Patch.Core;

namespace PCL2Patch.Gui;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

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
