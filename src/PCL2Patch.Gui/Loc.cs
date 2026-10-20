using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Win32;

namespace PCL2Patch.Gui;

public enum AppLanguage
{
    Auto,
    Chinese,
    English
}

/// <summary>内置中英文文案（单文件发布，不使用卫星程序集）。</summary>
public static class Loc
{
    public static AppLanguage Language { get; private set; } = AppLanguage.Auto;

    static readonly Dictionary<string, (string Zh, string En)> Strings = new()
    {
        ["Title"] = ("PCL2 限制解除补丁", "PCL2 Restriction Patch"),
        ["Description"] = ("选择官方 PCL2 程序，点击“开始修补”生成解除下载/安装限制的副本。", "Select the official PCL2 executable and click \"Start\" to create an unrestricted copy."),
        ["Label.Program"] = ("PCL2 程序", "PCL2 program"),
        ["Label.Output"] = ("输出路径", "Output path"),
        ["Label.Theme"] = ("主题", "Theme"),
        ["Label.Language"] = ("语言", "Language"),
        ["Button.Browse"] = ("浏览…", "Browse…"),
        ["Button.Start"] = ("开始修补", "Start"),
        ["Placeholder.Program"] = ("选择 Plain Craft Launcher 2.exe", "Select Plain Craft Launcher 2.exe"),
        ["Placeholder.Output"] = ("默认在 PCL2 同目录生成 *_patched.exe", "Defaults to *_patched.exe next to PCL2"),
        ["Theme.Light"] = ("浅色", "Light"),
        ["Theme.Dark"] = ("深色", "Dark"),
        ["Theme.System"] = ("跟随系统", "System"),
        ["Lang.Auto"] = ("自动", "Auto"),
        ["Status.Ready"] = ("就绪", "Ready"),
        ["Status.Patching"] = ("正在修补…", "Patching…"),
        ["Status.Done"] = ("完成", "Done"),
        ["Status.Failed"] = ("失败", "Failed"),
        ["Log.Program"] = ("官方程序", "Program"),
        ["Log.Output"] = ("输出路径", "Output"),
        ["Log.Done"] = ("修补完成", "Patched"),
        ["Log.Backup"] = ("已备份原程序", "Backup"),
        ["Msg.Backup"] = ("原程序备份：", "Backup:"),
        ["Msg.SelectProgram"] = ("请选择官方 PCL2 程序。", "Please select the official PCL2 executable."),
        ["Msg.Success"] = ("修补完成：", "Patch completed:"),
        ["Msg.Failed"] = ("修补失败：", "Patch failed:"),
        ["Msg.Hint"] = ("提示", "Hint"),
        ["Msg.SuccessTitle"] = ("成功", "Success"),
        ["Msg.FailedTitle"] = ("失败", "Failed")
    };

    public static bool IsChinese => Language switch
    {
        AppLanguage.Chinese => true,
        AppLanguage.English => false,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("zh", StringComparison.OrdinalIgnoreCase)
    };

    public static string Get(string key)
    {
        if (!Strings.TryGetValue(key, out var value)) return key;
        return IsChinese ? value.Zh : value.En;
    }

    public static void Load()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\PCL2Patch");
            Language = (key?.GetValue("Language") as string) switch
            {
                "zh" => AppLanguage.Chinese,
                "en" => AppLanguage.English,
                _ => AppLanguage.Auto
            };
        }
        catch
        {
            Language = AppLanguage.Auto;
        }
    }

    public static void Set(AppLanguage language)
    {
        Language = language;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\PCL2Patch");
            key?.SetValue("Language", language switch
            {
                AppLanguage.Chinese => "zh",
                AppLanguage.English => "en",
                _ => "auto"
            });
        }
        catch
        {
            // 保存失败忽略
        }
    }
}