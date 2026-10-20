using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PCL2Patch.Core;
using Wpf.Ui.Appearance;

namespace PCL2Patch.Gui;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private bool _initializing;

    public MainWindow()
    {
        _initializing = true;
        InitializeComponent();

        Title = $"PCL2Patch v{typeof(MainWindow).Assembly.GetName().Version?.ToString(3)}";
        AppTitleBar.Title = Title;

        Loc.Load();
        BuildSelectors();
        ApplyLocalization();
        LoadTheme();
        StatusText.Text = Loc.Get("Status.Ready");

        _initializing = false;
    }

    // ---------- 本地化 ----------

    private void BuildSelectors()
    {
        ThemeBox.Items.Clear();
        LanguageBox.Items.Clear();
        for (var i = 0; i < 3; i++)
        {
            ThemeBox.Items.Add(new ComboBoxItem());
            LanguageBox.Items.Add(new ComboBoxItem());
        }
    }

    private void ApplyLocalization()
    {
        AppTitleBar.Title = Title;
        TitleText.Text = Loc.Get("Title");
        DescText.Text = Loc.Get("Description");
        ThemeLabel.Text = Loc.Get("Label.Theme");
        LanguageLabel.Text = Loc.Get("Label.Language");
        ProgramLabel.Text = Loc.Get("Label.Program");
        BrowseProgramButton.Content = Loc.Get("Button.Browse");
        PatchButton.Content = Loc.Get("Button.Start");
        RestoreButton.Content = Loc.Get("Button.Restore");
        OfficialBox.PlaceholderText = Loc.Get("Placeholder.Program");

        ((ComboBoxItem)ThemeBox.Items[0]).Content = Loc.Get("Theme.Light");
        ((ComboBoxItem)ThemeBox.Items[1]).Content = Loc.Get("Theme.Dark");
        ((ComboBoxItem)ThemeBox.Items[2]).Content = Loc.Get("Theme.System");

        ((ComboBoxItem)LanguageBox.Items[0]).Content = Loc.Get("Lang.Auto");
        ((ComboBoxItem)LanguageBox.Items[1]).Content = "中文";
        ((ComboBoxItem)LanguageBox.Items[2]).Content = "English";
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || LanguageBox.SelectedIndex < 0) return;
        Loc.Set(LanguageBox.SelectedIndex switch
        {
            1 => AppLanguage.Chinese,
            2 => AppLanguage.English,
            _ => AppLanguage.Auto
        });
        ApplyLocalization();
    }

    // ---------- 主题 ----------

    private void LoadTheme()
    {
        var index = 2;
        try
        {
            index = ReadThemePreference() switch
            {
                "light" => 0,
                "dark" => 1,
                _ => 2
            };
        }
        catch
        {
            // 读取失败时保持默认
        }

        ThemeBox.SelectedIndex = index;
        ApplyTheme(index);

        LanguageBox.SelectedIndex = Loc.Language switch
        {
            AppLanguage.Chinese => 1,
            AppLanguage.English => 2,
            _ => 0
        };
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || ThemeBox.SelectedIndex < 0) return;
        ApplyTheme(ThemeBox.SelectedIndex);
    }

    private void ApplyTheme(int index)
    {
        try
        {
            if (index == 2)
            {
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

        SaveThemePreference(index switch { 0 => "light", 1 => "dark", _ => "system" });
    }

    private static string ReadThemePreference()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\PCL2Patch");
        return key?.GetValue("Theme") as string ?? "system";
    }

    private static void SaveThemePreference(string value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\PCL2Patch");
            key?.SetValue("Theme", value);
        }
        catch
        {
            // 保存失败忽略
        }
    }

    // ---------- 补丁 ----------

    // ---------- 拖放 ----------

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && HasExe(e.Data)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;

        var exe = files.FirstOrDefault(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        if (exe is null) return;

        OfficialBox.Text = exe;
        StatusText.Text = Loc.Get("Status.Ready");
        e.Handled = true;
    }

    private static bool HasExe(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is not string[] files) return false;
        return files.Any(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }
    private void BrowseOfficial_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.Get("Label.Program"),
            Filter = "PCL2 (*.exe)|*.exe|*|*.*"
        };
        if (dialog.ShowDialog(this) == true)
            OfficialBox.Text = dialog.FileName;
    }

    private async void Patch_Click(object sender, RoutedEventArgs e)
    {
        var official = OfficialBox.Text.Trim();
        if (!File.Exists(official))
        {
            MessageBox.Show(this, Loc.Get("Msg.SelectProgram"), Loc.Get("Msg.Hint"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (IsFileLocked(official))
        {
            MessageBox.Show(this, Loc.Get("Msg.Busy"), Loc.Get("Msg.Hint"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool alreadyPatched;
        bool officialBuild;
        try
        {
            (alreadyPatched, officialBuild) = await Task.Run(() => (
                PatchEngine.IsPatched(official),
                PatchEngine.IsOfficialBuild(official)));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"{Loc.Get("Msg.InvalidProgram")}\n{ex.Message}", Loc.Get("Msg.Hint"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (alreadyPatched)
        {
            MessageBox.Show(this, Loc.Get("Msg.AlreadyPatched"), Loc.Get("Msg.Hint"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!officialBuild)
        {
            MessageBox.Show(this, Loc.Get("Msg.NotOfficial"), Loc.Get("Msg.Hint"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PatchButton.IsEnabled = false;
        RestoreButton.IsEnabled = false;
        LogBox.Clear();
        StatusText.Text = Loc.Get("Status.Patching");
        AppendLog($"{Loc.Get("Log.Program")}: {official}");
        AppendLog(string.Empty);

        try
        {
            string backup = string.Empty;
            await Task.Run(() => backup = PatchEngine.ApplyInPlace(official, AppendLog));

            AppendLog(string.Empty);
            AppendLog($"{Loc.Get("Log.Backup")}: {backup}");
            AppendLog($"{Loc.Get("Log.Done")}: {official}");
            StatusText.Text = Loc.Get("Status.Done");
            MessageBox.Show(this,
                $"{Loc.Get("Msg.Success")}\n{official}\n\n{Loc.Get("Msg.Backup")}\n{backup}",
                Loc.Get("Msg.SuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = Loc.Get("Status.Failed");
            AppendLog($"{Loc.Get("Log.Failed")}: {ex.Message}");
            MessageBox.Show(this, $"{Loc.Get("Msg.Failed")}\n{ex.Message}", Loc.Get("Msg.FailedTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PatchButton.IsEnabled = true;
            RestoreButton.IsEnabled = true;
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var official = OfficialBox.Text.Trim();
        if (!File.Exists(official))
        {
            MessageBox.Show(this, Loc.Get("Msg.SelectProgram"), Loc.Get("Msg.Hint"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (IsFileLocked(official))
        {
            MessageBox.Show(this, Loc.Get("Msg.Busy"), Loc.Get("Msg.Hint"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var backup = PatchEngine.FindBackup(official);
        if (backup == null)
        {
            MessageBox.Show(this, Loc.Get("Msg.NoBackup"), Loc.Get("Msg.Hint"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(this, $"{Loc.Get("Msg.RestoreConfirm")}\n{backup}", Loc.Get("Msg.Hint"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        PatchButton.IsEnabled = false;
        RestoreButton.IsEnabled = false;
        StatusText.Text = Loc.Get("Status.Restoring");

        try
        {
            await Task.Run(() => PatchEngine.Restore(official));
            StatusText.Text = Loc.Get("Status.Done");
            AppendLog($"{Loc.Get("Log.Restore")}: {official}");
            MessageBox.Show(this, $"{Loc.Get("Msg.RestoreSuccess")}\n{official}",
                Loc.Get("Msg.SuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = Loc.Get("Status.Failed");
            AppendLog($"{Loc.Get("Log.Failed")}: {ex.Message}");
            MessageBox.Show(this, $"{Loc.Get("Msg.Failed")}\n{ex.Message}", Loc.Get("Msg.FailedTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PatchButton.IsEnabled = true;
            RestoreButton.IsEnabled = true;
        }
    }

    private static bool IsFileLocked(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
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