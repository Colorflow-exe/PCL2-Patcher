using PCL2Patch.Core;

// PCL2Patch —— PCL2（Plain Craft Launcher 2）限制解除补丁工具
// 用法:
//   PCL2Patch apply <官方exe> <输出exe>      一次性应用全部补丁
//   PCL2Patch dump  <exe> <Type::Method>     反汇编指定方法

if (args.Length == 0)
{
    PrintUsage();
    return 2;
}

try
{
    switch (args[0])
    {
        case "apply" when args.Length == 3:
            PatchEngine.Apply(args[1], args[2], Console.WriteLine);
            return 0;
        case "dump" when args.Length == 3:
            PatchEngine.Dump(args[1], args[2], Console.WriteLine);
            return 0;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"失败: {ex.Message}");
    return 1;
}

PrintUsage();
return 2;

static void PrintUsage()
{
    Console.WriteLine("PCL2Patch - PCL2 限制解除补丁工具");
    Console.WriteLine("  PCL2Patch apply <官方exe> <输出exe>      一次性应用全部补丁");
    Console.WriteLine("  PCL2Patch dump  <exe> <Type::Method>     反汇编指定方法");
    Console.WriteLine();
    Console.WriteLine("补丁逻辑与参考程序集均内置，无需参考 exe。");
}
