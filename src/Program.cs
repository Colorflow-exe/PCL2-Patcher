using Mono.Cecil;
using Mono.Cecil.Cil;

// PCL2Patch —— PCL2（Plain Craft Launcher 2）限制解除补丁工具
// 用法:
//   PCL2Patch apply <官方exe> <参考exe> <输出exe>   一次性应用全部补丁
//   PCL2Patch dump  <exe> <Type::Method>            反汇编指定方法
//
// 参考 exe 必须是同版本的、以 RestrictMcDownload = False 编译的 PCL2，
// 用于搬运被官方版常量优化删除的安装逻辑。

if (args.Length == 0)
{
    PrintUsage();
    return 2;
}

switch (args[0])
{
    case "apply" when args.Length == 4:
        return Apply(args[1], args[2], args[3]);
    case "dump" when args.Length == 3:
        return Dump(args[1], args[2]);
    default:
        PrintUsage();
        return 2;
}

static void PrintUsage()
{
    Console.WriteLine("PCL2Patch - PCL2 限制解除补丁工具");
    Console.WriteLine("  PCL2Patch apply <官方exe> <参考exe> <输出exe>   一次性应用全部补丁");
    Console.WriteLine("  PCL2Patch dump  <exe> <Type::Method>            反汇编指定方法");
    Console.WriteLine();
    Console.WriteLine("参考 exe：同版本、RestrictMcDownload = False 的编译产物（见 README）。");
}

static int Apply(string officialPath, string referencePath, string outPath)
{
    var official = AssemblyDefinition.ReadAssembly(officialPath, ReaderParams(officialPath, referencePath));
    var reference = AssemblyDefinition.ReadAssembly(referencePath, ReaderParams(referencePath, officialPath));
    var target = official.MainModule;

    try
    {
        PatchDownloadLoader(target);
        PatchMinecraftCard(target);
        CloneMethod(reference, target, "PCL.ModModpack", "ModpackInstall", 0);
        PatchInstallClick(target);
        CloneMethod(reference, target, "PCL.ModModpack", "ModpackInstall", 3);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"补丁失败: {ex.Message}");
        return 1;
    }

    official.Write(outPath);
    Console.WriteLine($"已写出: {outPath}");
    return 0;
}

static ReaderParameters ReaderParams(params string[] searchPaths)
{
    var resolver = new DefaultAssemblyResolver();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    void Add(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir) || !seen.Add(dir)) return;
        resolver.AddSearchDirectory(dir);
    }
    foreach (var p in searchPaths) Add(Path.GetDirectoryName(Path.GetFullPath(p)));
    foreach (var p in (Environment.GetEnvironmentVariable("PCL2PATCH_REFS") ?? "").Split(';')) Add(p.Trim());
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
static void PatchDownloadLoader(ModuleDefinition m)
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
    Console.WriteLine("[1/5] 下载页加载器 -> DlClientListLoader");
}

// 2. 修改页：恢复 Minecraft 原版卡片可见
static void PatchMinecraftCard(ModuleDefinition m)
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
            Console.WriteLine("[2/5] 修改页 Minecraft 卡片 -> Visible");
            return;
        }
    throw new Exception("LoadInstanceFinished: 未找到 Visibility.Collapsed");
}

// 4. 资源详情页：点击版本列表项 -> 安装（Install_Click）
static void PatchInstallClick(ModuleDefinition m)
{
    var type = FindType(m, "PCL.PageDownloadResourceDetail");
    var method = type.Methods.First(x => x.Name == "InitializeStackResourceVersions");
    var lambda = type.Methods.First(x => x.Name == "_Lambda$__R18-5");
    var ins = method.Body.Instructions;

    for (int i = 0; i < ins.Count; i++)
        if (ins[i].OpCode == OpCodes.Ldftn && ins[i].Operand is MethodReference mr && mr.Name == "Save_Click")
        {
            ins[i].Operand = m.ImportReference(lambda);
            Console.WriteLine("[4/5] 整合包版本列表点击 -> Install_Click");
            return;
        }
    throw new Exception("InitializeStackResourceVersions: 未找到 Save_Click 引用");
}

// 3/5. 从参考程序集搬运整个方法体（用于被常量优化删除的安装逻辑）
static void CloneMethod(AssemblyDefinition srcAsm, ModuleDefinition dstModule, string typeName, string methodName, int paramCount)
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

    Console.WriteLine($"[{(paramCount == 0 ? "3/5" : "5/5")}] 搬运 {typeName}::{methodName}({paramCount} 参数) 共 {srcBody.Instructions.Count} 条指令");
}

static int Dump(string path, string target)
{
    var asm = AssemblyDefinition.ReadAssembly(path, ReaderParams(path));
    var m = asm.MainModule;
    if (!target.Contains("::"))
    {
        foreach (var t in m.Types.SelectMany(Flatten))
            if (t.FullName.Contains(target)) Console.WriteLine(t.FullName);
        return 0;
    }
    var typeName = target.Split("::")[0];
    var methodPart = target.Split("::")[1];
    foreach (var t in m.Types.SelectMany(Flatten).Where(t => t.FullName == typeName))
        foreach (var method in t.Methods.Where(x => x.HasBody && x.FullName.Contains(methodPart)))
        {
            Console.WriteLine($"=== {method.FullName}  ({method.Body.Instructions.Count} 条指令) ===");
            foreach (var h in method.Body.ExceptionHandlers)
                Console.WriteLine($"  [handler] {h.HandlerType} try={h.TryStart}..{h.TryEnd} handler={h.HandlerStart}..{h.HandlerEnd}");
            int n = 0;
            foreach (var i in method.Body.Instructions) Console.WriteLine($"  {n++,4}: {i}");
        }
    return 0;
}
