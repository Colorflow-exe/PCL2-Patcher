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
        ["Description"] = ("选择官方 PCL2 程序（也可以直接把 exe 拖进窗口），点击“开始修补”生成解除下载/安装限制的副本。", "Select the official PCL2 executable (or drag the exe into the window) and click \"Start\" to create an unrestricted copy."),
        ["Label.Program"] = ("PCL2 程序", "PCL2 program"),
        ["Label.Theme"] = ("主题", "Theme"),
        ["Label.Language"] = ("语言", "Language"),
        ["Button.Browse"] = ("浏览…", "Browse…"),
        ["Button.Start"] = ("开始修补", "Start"),
        ["Placeholder.Program"] = ("选择 Plain Craft Launcher 2.exe", "Select Plain Craft Launcher 2.exe"),
        ["Theme.Light"] = ("浅色", "Light"),
        ["Theme.Dark"] = ("深色", "Dark"),
        ["Theme.System"] = ("跟随系统", "System"),
        ["Lang.Auto"] = ("自动", "Auto"),
        ["Status.Ready"] = ("就绪", "Ready"),
        ["Status.Patching"] = ("正在修补…", "Patching…"),
        ["Status.Done"] = ("完成", "Done"),
        ["Status.Failed"] = ("失败", "Failed"),
        ["Log.Program"] = ("官方程序", "Program"),
        ["Log.Done"] = ("修补完成", "Patched"),
        ["Log.Failed"] = ("失败", "Failed"),
        ["Log.Backup"] = ("已备份原程序", "Backup"),
        ["Msg.Backup"] = ("原程序备份：", "Backup:"),
        ["Msg.SelectProgram"] = ("请选择官方 PCL2 程序。", "Please select the official PCL2 executable."),
        ["Msg.Success"] = ("修补完成：", "Patch completed:"),
        ["Msg.Failed"] = ("修补失败：", "Patch failed:"),
        ["Msg.Hint"] = ("提示", "Hint"),
        ["Msg.SuccessTitle"] = ("成功", "Success"),
        ["Msg.FailedTitle"] = ("失败", "Failed"),
        ["Button.Restore"] = ("恢复原版", "Restore"),
        ["Status.Restoring"] = ("正在恢复…", "Restoring…"),
        ["Log.Restore"] = ("已恢复原版", "Restored"),
        ["Msg.Busy"] = ("文件被占用或没有访问权限。请先关闭 PCL2（必要时以管理员身份运行本工具）。", "The file is locked or access denied. Close PCL2 first (run this tool as administrator if needed)."),
        ["Msg.AlreadyPatched"] = ("该程序已经是补丁版，无需重复修补。如需还原，请点击“恢复原版”。", "This program is already patched. Use \"Restore\" to revert if needed."),
        ["Msg.NotOfficial"] = ("该程序不是官方版 PCL2（VersionBranchMain != Official），已取消修补。", "This is not an official PCL2 build (VersionBranchMain != Official)."),
        ["Msg.InvalidProgram"] = ("无法读取该程序，可能不是有效的 PCL2。", "Cannot read this program, it may not be a valid PCL2."),
        ["Msg.NoBackup"] = ("未找到原版备份（*_backup.exe）。", "No backup found (*_backup.exe)."),
        ["Msg.RestoreConfirm"] = ("将用备份恢复原程序，当前修补版会被替换。继续？", "Restore the original program from backup? The patched file will be replaced."),
        ["Msg.RestoreSuccess"] = ("已恢复原版：", "Restored:")
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
