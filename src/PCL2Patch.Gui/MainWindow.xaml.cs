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
        OfficialBox.PlaceholderText = Loc.Get("Placeholder.Program");
        StatusText.Text = Loc.Get("Status.Ready");

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

        PatchButton.IsEnabled = false;
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