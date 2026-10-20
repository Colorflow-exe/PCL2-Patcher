using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

/// <summary>
/// PCL2Patch 自解压启动器：内嵌完整的 self-contained GUI 目录（payload.zip），
/// 首次运行时解压到 exe 同目录的 PCL2Patch_runtime，然后启动 GUI。
/// 对外只交付一个 exe。
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            var baseDir = Path.GetDirectoryName(Application.ExecutablePath) ?? ".";
            var runtimeDir = Path.Combine(baseDir, "PCL2Patch_runtime");
            var gui = Path.Combine(runtimeDir, "PCL2Patch.exe");

            if (!File.Exists(gui))
            {
                if (!TryExtract(runtimeDir))
                {
                    // exe 同目录不可写（如只读位置）时回退到临时目录
                    runtimeDir = Path.Combine(Path.GetTempPath(), "PCL2Patch_runtime");
                    gui = Path.Combine(runtimeDir, "PCL2Patch.exe");
                    if (!File.Exists(gui)) TryExtract(runtimeDir);
                }
            }

            Process.Start(new ProcessStartInfo(gui) { WorkingDirectory = runtimeDir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("PCL2Patch 启动失败：" + ex.Message, "PCL2Patch", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool TryExtract(string targetDir)
    {
        try
        {
            Directory.CreateDirectory(targetDir);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            {
                if (stream == null) throw new InvalidOperationException("缺少内置 payload.zip");

                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var target = Path.Combine(targetDir, entry.FullName.Replace('/', '\\'));
                        if (entry.Name.Length == 0)
                        {
                            Directory.CreateDirectory(target);
                            continue;
                        }

                        var parent = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                        entry.ExtractToFile(target, true);
                    }
                }
            }
            return File.Exists(Path.Combine(targetDir, "PCL2Patch.exe"));
        }
        catch
        {
            return false;
        }
    }
}
