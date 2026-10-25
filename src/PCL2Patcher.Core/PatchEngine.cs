using System.IO.Compression;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace PCL2Patcher.Core;

/// <summary>
/// PCL2 限制解除补丁引擎：对官方 PCL2 程序集应用 5 处 IL 修改。
/// 被常量优化删除的方法体从内置参考程序集搬运，无需用户提供参考 exe。
/// </summary>
public static class PatchEngine
{
    /// <summary>应用全部补丁。</summary>
    public static void Apply(string officialPath, string outputPath, Action<string>? log = null)
    {
        log ??= _ => { };
        using var official = AssemblyDefinition.ReadAssembly(officialPath, ReaderParams(officialPath));
        var module = official.MainModule;

        PatchDownloadLoader(module, log);
        PatchMinecraftCard(module, log);

        using var reference = LoadReferenceAssembly();
        CloneMethod(reference, module, "PCL.ModModpack", "ModpackInstall", 0, log);
        CloneMethod(reference, module, "PCL.PageDownloadResourceDetail", "InitializeStackResourceVersions", 1, log, "[4/5] 整合包版本点击 -> Install_Click");
        CloneMethod(reference, module, "PCL.ModModpack", "ModpackInstall", 3, log);

        official.Write(outputPath);
        log($"已写出: {outputPath}");
    }

    /// <summary>
    /// 就地修补：先把原程序备份到同目录（隐藏文件），再用修补结果覆盖原路径。
    /// 返回备份文件路径；任何一步失败都会回滚。
    /// </summary>
    public static string ApplyInPlace(string officialPath, Action<string>? log = null)
    {
        log ??= _ => { };
        var fullPath = Path.GetFullPath(officialPath);
        var dir = Path.GetDirectoryName(fullPath)!;
        var name = Path.GetFileNameWithoutExtension(fullPath);
        var ext = Path.GetExtension(fullPath);
        var temp = Path.Combine(dir, $".{name}.patching{ext}");
        var backup = GetBackupPath(fullPath);

        try { if (File.Exists(temp)) File.Delete(temp); } catch { }

        try
        {
            Apply(fullPath, temp, log);

            // 备份到同目录（隐藏文件）
            File.Copy(fullPath, backup, true);
            File.Delete(fullPath);

            try
            {
                File.Move(temp, fullPath);
            }
            catch
            {
                File.Copy(backup, fullPath, true);
                throw;
            }
            try { File.SetAttributes(backup, File.GetAttributes(backup) | FileAttributes.Hidden); } catch { }
            return backup;
        }
        catch
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); } catch { }
            }
            throw;
        }
    }

   /// <summary>判断目标程序是否已经是补丁版（LoaderInit 中不再有“关闭下载”占位）。</summary>
    public static bool IsPatched(string path)
    {
        using var asm = AssemblyDefinition.ReadAssembly(path, ReaderParams(path));
        var type = FindType(asm.MainModule, "PCL.PageDownloadInstall");
        var method = type.Methods.First(x => x.Name == "LoaderInit");
        return !method.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "关闭下载");
    }

    /// <summary>判断是否为官方版 PCL2（VersionBranchMain == "Official"）。</summary>
    public static bool IsOfficialBuild(string path)
    {
        using var asm = AssemblyDefinition.ReadAssembly(path, ReaderParams(path));
        var type = FindType(asm.MainModule, "PCL.ModSecret");
        var field = type.Fields.FirstOrDefault(f => f.Name == "VersionBranchMain");
        return field?.Constant is string value && value == "Official";
    }

    static string GetBackupPath(string officialPath)
    {
        var fullPath = Path.GetFullPath(officialPath);
        var dir = Path.GetDirectoryName(fullPath)!;
        var name = Path.GetFileNameWithoutExtension(fullPath);
        var ext = Path.GetExtension(fullPath);
        var candidate = Path.Combine(dir, $"{name}_backup{ext}");
        var index = 1;
        while (File.Exists(candidate))
            candidate = Path.Combine(dir, $"{name}_backup_{index++}{ext}");
        return candidate;
    }

    /// <summary>查找原版备份（与目标同目录，隐藏文件）。</summary>
    public static string? FindBackup(string officialPath)
    {
        var fullPath = Path.GetFullPath(officialPath);
        var dir = Path.GetDirectoryName(fullPath)!;
        var name = Path.GetFileNameWithoutExtension(fullPath);
        var ext = Path.GetExtension(fullPath);

        var direct = Path.Combine(dir, $"{name}_backup{ext}");
        if (File.Exists(direct)) return direct;

        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_backup_{i}{ext}");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>撤销修补：从同目录备份恢复原版（覆盖当前补丁版）。</summary>
    public static void Restore(string officialPath, Action<string>? log = null)
    {
        log ??= _ => { };
        var fullPath = Path.GetFullPath(officialPath);
        var backup = FindBackup(fullPath) ?? throw new Exception("未找到原版备份");
        var staging = fullPath + ".restoring";
        try { if (File.Exists(staging)) File.Delete(staging); } catch { }

        // 先把备份复制到临时位置，确保恢复失败时原文件仍可还原
        File.Copy(backup, staging, true);
        try
        {
            File.Copy(staging, fullPath, true);
        }
        catch
        {
            try { File.Delete(staging); } catch { }
            throw;
        }

        try { File.Delete(staging); } catch { }
        try { File.Delete(backup); } catch { }
        try { File.SetAttributes(fullPath, File.GetAttributes(fullPath) & ~FileAttributes.Hidden); } catch { }
        log($"已恢复原版: {fullPath}");
    }
    /// <summary>读取内置的参考程序集（RestrictMcDownload = False 的编译版，gzip 压缩）。</summary>
    static AssemblyDefinition LoadReferenceAssembly()
    {
        var host = typeof(PatchEngine).Assembly;
        using var resource = host.GetManifestResourceStream("PCL2Patcher.Core.Resources.reference.gz")
            ?? throw new Exception("找不到内置参考程序集资源");
        using var gz = new GZipStream(resource, CompressionMode.Decompress);
        using var buffer = new MemoryStream();
        gz.CopyTo(buffer);

        // Cecil 会持有传入的流，这里用独立 MemoryStream 并保持存活
        var memory = new MemoryStream(buffer.ToArray());
        return AssemblyDefinition.ReadAssembly(memory, new ReaderParameters
        {
            AssemblyResolver = new LenientResolver(),
            ReadingMode = ReadingMode.Immediate
        });
    }

    /// <summary>反汇编指定方法，输出到 <paramref name="log"/>。</summary>
    public static void Dump(string path, string target, Action<string>? log = null)
    {
        log ??= _ => { };
        using var asm = AssemblyDefinition.ReadAssembly(path, ReaderParams(path));
        var m = asm.MainModule;

        if (!target.Contains("::"))
        {
            foreach (var t in m.Types.SelectMany(Flatten))
                if (t.FullName.Contains(target)) log(t.FullName);
            return;
        }

        var parts = target.Split(new[] { "::" }, StringSplitOptions.None);
        var typeName = parts[0];
        var methodPart = parts[1];
        foreach (var type in m.Types.SelectMany(Flatten).Where(t => t.FullName == typeName))
            foreach (var method in type.Methods.Where(x => x.HasBody && x.FullName.Contains(methodPart)))
            {
                log($"=== {method.FullName}  ({method.Body.Instructions.Count} 条指令) ===");
                foreach (var h in method.Body.ExceptionHandlers)
                    log($"  [handler] {h.HandlerType} try={h.TryStart}..{h.TryEnd} handler={h.HandlerStart}..{h.HandlerEnd}");
                int n = 0;
                foreach (var i in method.Body.Instructions) log($"  {n++,4}: {i}");
            }
    }

    static ReaderParameters ReaderParams(params string[] searchPaths)
    {
        var resolver = new LenientResolver();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir) || !seen.Add(dir)) return;
            resolver.AddSearchDirectory(dir);
        }
        foreach (var p in searchPaths) Add(Path.GetDirectoryName(Path.GetFullPath(p)));
        foreach (var p in (Environment.GetEnvironmentVariable("PCL2PATCHER_REFS") ?? "").Split(';')) Add(p.Trim());
        return new ReaderParameters { AssemblyResolver = resolver };
    }

    static IEnumerable<TypeDefinition> Flatten(TypeDefinition t)
    {
        yield return t;
        foreach (var n in t.NestedTypes)
            foreach (var c in Flatten(n))
                yield return c;
    }

    static TypeDefinition FindType(ModuleDefinition m, string name) =>
        m.Types.SelectMany(Flatten).FirstOrDefault(t => t.FullName == name)
        ?? throw new Exception($"未找到类型 {name}");

    // 1. 下载页版本列表：把“关闭下载”占位 LoaderTask 换成真正的 DlClientListLoader
    static void PatchDownloadLoader(ModuleDefinition m, Action<string> log)
    {
        var type = FindType(m, "PCL.PageDownloadInstall");
        var method = type.Methods.First(x => x.Name == "LoaderInit");
        var ins = method.Body.Instructions;

        int start = -1, end = -1;
        for (int i = 0; i < ins.Count; i++)
            if (ins[i].OpCode == OpCodes.Ldstr && (string)ins[i].Operand == "关闭下载") { start = i; break; }
        if (start < 0) throw new Exception("LoaderInit: 未找到“关闭下载”占位加载器");

        for (int i = start + 1; i < ins.Count; i++)
            if (ins[i].OpCode == OpCodes.Newobj && ins[i].Operand is MethodReference mr &&
                mr.DeclaringType.Name.StartsWith("LoaderTask"))
            { end = i; break; }
        if (end < 0) throw new Exception("LoaderInit: 未找到 LoaderTask 构造");

        var field = FindType(m, "PCL.ModDownload").Fields.First(f => f.Name == "DlClientListLoader");
        var fieldRef = new FieldReference(field.Name, m.ImportReference(field.FieldType), m.ImportReference(field.DeclaringType));

        var anchor = ins[end + 1];
        for (int i = end; i >= start; i--) ins.RemoveAt(i);
        ins.Insert(ins.IndexOf(anchor), Instruction.Create(OpCodes.Ldsfld, fieldRef));
        log("[1/5] 下载页加载器 -> DlClientListLoader");
    }

    // 2. 修改页：恢复 Minecraft 原版卡片可见
    static void PatchMinecraftCard(ModuleDefinition m, Action<string> log)
    {
        var type = FindType(m, "PCL.PageDownloadModify");
        var method = type.Methods.First(x => x.Name == "LoadInstanceFinished");
        var ins = method.Body.Instructions;

        int getCard = -1;
        for (int i = 0; i < ins.Count; i++)
            if (ins[i].OpCode == OpCodes.Callvirt && ins[i].Operand is MethodReference mr &&
                mr.Name == "get_CardMinecraft" && mr.DeclaringType.Name == "PageDownloadInstall")
            { getCard = i; break; }
        if (getCard < 0) throw new Exception("LoadInstanceFinished: 未找到 get_CardMinecraft");

        for (int i = getCard + 1; i < ins.Count && i < getCard + 4; i++)
            if (ins[i].OpCode == OpCodes.Ldc_I4_2)
            {
                ins[i].OpCode = OpCodes.Ldc_I4_0;
                ins[i].Operand = null;
                log("[2/5] 修改页 Minecraft 卡片 -> Visible");
                return;
            }
        throw new Exception("LoadInstanceFinished: 未找到 Visibility.Collapsed");
    }

    // 4. 资源详情页：点击版本列表项 -> 安装（Install_Click）
    // 3/5. 从参考程序集搬运整个方法体（被常量优化删除的安装逻辑）
    static void CloneMethod(AssemblyDefinition srcAsm, ModuleDefinition dstModule, string typeName, string methodName, int paramCount, Action<string> log, string? logLabel = null)
    {
        var srcType = FindType(srcAsm.MainModule, typeName);
        var dstType = FindType(dstModule, typeName);
        var srcMethod = srcType.Methods.First(m => m.Name == methodName && m.HasBody && m.Parameters.Count == paramCount);
        var dstMethod = dstType.Methods.First(m => m.Name == methodName && m.HasBody && m.Parameters.Count == paramCount);

        var srcBody = srcMethod.Body;
        var dstBody = dstMethod.Body;
        dstBody.Instructions.Clear();
        dstBody.ExceptionHandlers.Clear();
        dstBody.Variables.Clear();
        foreach (var v in srcBody.Variables)
            dstBody.Variables.Add(new VariableDefinition(dstModule.ImportReference(v.VariableType)));

        var map = new Dictionary<Instruction, Instruction>();
        foreach (var si in srcBody.Instructions) map[si] = Instruction.Create(OpCodes.Nop);

        object? MapOperand(Instruction si) => si.Operand switch
        {
            null => null,
            Instruction ti => map.TryGetValue(ti, out var mi) ? mi : throw new Exception("未映射的分支目标"),
            Instruction[] arr => arr.Select(x => map[x]).ToArray(),
            MethodReference mr => dstModule.ImportReference(mr),
            FieldReference fr => dstModule.ImportReference(fr),
            TypeReference tr => dstModule.ImportReference(tr),
            VariableDefinition vd => dstBody.Variables[srcBody.Variables.IndexOf(vd)],
            var other => other
        };

        foreach (var si in srcBody.Instructions)
        {
            var ni = map[si];
            ni.OpCode = si.OpCode;
            ni.Operand = MapOperand(si);
            dstBody.Instructions.Add(ni);
        }

        foreach (var h in srcBody.ExceptionHandlers)
            dstBody.ExceptionHandlers.Add(new ExceptionHandler(h.HandlerType)
            {
                TryStart = h.TryStart is null ? null : map[h.TryStart],
                TryEnd = h.TryEnd is null ? null : map[h.TryEnd],
                HandlerStart = h.HandlerStart is null ? null : map[h.HandlerStart],
                HandlerEnd = h.HandlerEnd is null ? null : map[h.HandlerEnd],
                FilterStart = h.FilterStart is null ? null : map[h.FilterStart],
                CatchType = h.CatchType is null ? null : dstModule.ImportReference(h.CatchType)
            });

        log(logLabel ?? $"[{(paramCount == 0 ? "3/5" : "5/5")}] 搬运 {typeName}::{methodName}({paramCount} 参数) 共 {srcBody.Instructions.Count} 条指令");
    }

    /// <summary>写回时若引用程序集不在本地（如 PCLCS/MeloongCore），优先从内置资源加载，再退化为空程序集。</summary>
    sealed class LenientResolver : DefaultAssemblyResolver
    {
        readonly Dictionary<string, AssemblyDefinition> _cache = new();

        public override AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            if (_cache.TryGetValue(name.FullName, out var cached)) return cached;
            var embedded = TryEmbedded(name.Name);
            if (embedded is not null) return _cache[name.FullName] = embedded;
            try
            {
                return base.Resolve(name);
            }
            catch (AssemblyResolutionException)
            {
                return Fake(name);
            }
        }

        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (_cache.TryGetValue(name.FullName, out var cached)) return cached;
            var embedded = TryEmbedded(name.Name);
            if (embedded is not null) return _cache[name.FullName] = embedded;
            try
            {
                return base.Resolve(name, parameters);
            }
            catch (AssemblyResolutionException)
            {
                return Fake(name);
            }
        }

        static AssemblyDefinition? TryEmbedded(string assemblyName)
        {
            var host = typeof(PatchEngine).Assembly;
            var res = host.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("." + assemblyName + ".dll", StringComparison.OrdinalIgnoreCase));
            if (res is null) return null;

            // 保持流存活：Cecil 延迟模式会在后续按需读取，不能立即 Dispose
            var stream = host.GetManifestResourceStream(res);
            if (stream is null) return null;
            return AssemblyDefinition.ReadAssembly(stream);
        }

        AssemblyDefinition Fake(AssemblyNameReference name)
        {
            if (_cache.TryGetValue(name.FullName, out var asm)) return asm;
            asm = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(name.Name, name.Version), name.Name, ModuleKind.Dll);
            _cache[name.FullName] = asm;
            return asm;
        }
    }
}
