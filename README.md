# PCL2Patch

针对 **Plain Craft Launcher 2**（PCL2）的 IL 补丁工具：解除官方版中 `RestrictMcDownload` 常量带来的下载/安装限制。

不重新编译 PCL2，直接修改 PE 程序集的方法体，所以其余功能与官方版本完全一致。

## 背景

PCL2 源码 `Modules/ModSecret.vb` 中：

```vb
Public Const RestrictMcDownload As Boolean = True
```

这是编译期常量。`True` 时，编译器会把所有相关判断**内联并优化**：

- 下载页的版本列表换成"关闭下载"占位加载器（内部直接抛异常）；
- 资源详情页的整合包版本列表点击行为变成"保存 .mrpack"；
- `ModpackInstall` 的整个安装分支（CurseForge / HMCL / MMC / MCBBS / Modrinth / 压缩包）被判定不可达而删除，只剩限制 throw。

因此改元数据里的常量没有意义（已内联），必须改 IL。

## 补丁内容（5 处）

| # | 目标 | 修改 |
|---|------|------|
| 1 | `PCL.PageDownloadInstall::LoaderInit` | 把 `"关闭下载"` 占位 `LoaderTask` 的构造替换为 `ldsfld PCL.ModDownload::DlClientListLoader` |
| 2 | `PCL.PageDownloadModify::LoadInstanceFinished` | `get_CardMinecraft` 后的 `ldc.i4.2`（Collapsed）改为 `ldc.i4.0`（Visible） |
| 3 | `PCL.ModModpack::ModpackInstall()` | 从参考 exe 搬运完整方法体（41 条指令） |
| 4 | `PCL.PageDownloadResourceDetail::InitializeStackResourceVersions` | 循环里的 `ldftn Save_Click` 改为 `ldftn _Lambda$__R18-5`（转调 `Install_Click`） |
| 5 | `PCL.ModModpack::ModpackInstall(string,string,string)` | 从参考 exe 搬运完整安装分支（330 条指令，含全部 `InstallPack*` 与正常 `ret`） |

详细 IL 说明见 [docs/patch-notes.md](docs/patch-notes.md)。

## 环境要求

- Windows
- .NET 8 SDK（构建工具用）
- 官方版 PCL2 exe（2.99.x，`PCL.ModSecret.VersionBranchMain = "Official"`）
- 参考 exe：**同版本源码**、`RestrictMcDownload = False` 编译出的 PCL2（见下）

### 获取参考 exe

1. `git clone https://github.com/Meloong-Git/PCL`
2. `git submodule update --init --recursive`
3. 把 `Plain Craft Launcher 2/Modules/ModSecret.vb` 里的
   `Public Const RestrictMcDownload As Boolean = True` 改为 `False`
4. 用 VS（.NET Framework 4.8 目标 + WPF）编译 `Plain Craft Launcher 2.vbproj`

> 参考 exe 与官方 exe 必须来自同一提交/版本，否则方法体引用的成员可能不一致。

## 构建

```powershell
cd src
dotnet build -c Release
```

或使用脚本（若系统禁止运行脚本）：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
```

产物：`src/bin/Release/net8.0/PCL2Patch.exe`

## 使用

```powershell
# 依赖查找路径（可选；默认使用 exe 所在目录）
$env:PCL2PATCH_REFS = "C:\refs;D:\PCL\PCLCS\bin"

PCL2Patch.exe apply <官方exe> <参考exe> <输出exe>
```

示例：

```powershell
PCL2Patch.exe apply "PCL2_official.exe" "PCL2_unrestricted.exe" "PCL2_patched.exe"
```

反汇编检查：

```powershell
PCL2Patch.exe dump "PCL2_patched.exe" "PCL.ModModpack::ModpackInstall"
PCL2Patch.exe dump "PCL2_patched.exe" "PageDownload"
```

## 验证

```powershell
# IL 校验（可选，PEVerify）
PEVerify.exe PCL2_patched.exe /nologo /il
```

预期：除 `LoaderTask.ctor` 的泛型签名误报外无新增错误（官方原版同样存在该误报）。

运行验证：

- 启动后日志应有 `管理员权限：True`（推荐管理员运行）与 `DlClientList Mojang ... Finished`；
- 下载页可见完整版本列表；
- 整合包项目点版本 → 直接进入安装流程（而非"保存 .mrpack"）。

## 注意事项

- **以管理员权限运行 PCL2**：普通权限下 `%LocalAppData%\Temp\PCL\Cache` 写入被拒，Mojang/BMCLAPI 源会报 `UnauthorizedAccessException`。
- PCL2 有单实例检测，同一时间只能运行一个实例；测试时先关闭已运行的实例，否则新实例会以退出码 4 直接退出。
- 修改后的 exe 无数字签名变更（程序集本身未签名）。
- 本仓库不包含任何 PCL2 二进制文件（见 `.gitignore`）。
- 仅供个人研究/自用，请遵守 PCL 的许可证与所在地法律。

## 发布 Release

`.gitignore` 里的 `*.exe` 只影响 **git 跟踪**；GitHub Release 的附件是单独上传的，
不受 `.gitignore` 影响。

工作流按「构建 / 发布」分离（参考 Carpet-Igny-Addition 的组织方式）：

| 工作流 | 触发 | 作用 |
|---|---|---|
| `build.yml` | `workflow_call`（被下面两个调用） | 发布两个单文件 exe 并上传 artifact |
| `ci.yml`（**Dev Builds**） | 每次 push / PR / 手动 | 每次提交都构建一份构建版，产物在 Actions 的 artifact 中 |
| `release.yml`（**Release**） | Release 被 publish / 手动 dispatch | 构建后把 exe 挂到对应 Release |

构建产物：

- `PCL2Patch.exe`：framework-dependent 单文件（需 .NET 8 Runtime，约 665 KB）
- `PCL2Patch-selfcontained.exe`：self-contained 单文件（免运行时，约 65 MB）
- `SHA256SUMS.txt`

发布流程（在 GitHub 页面操作即可）：

1. `Actions → Dev Builds` 每次 push 自动跑，可在 artifact 下载构建版；
2. 在 GitHub 上 **Draft a new release**，选择 tag 并 **Publish release**；
3. `Release` 工作流触发，自动构建并上传上述三个文件；
4. 也可在 `Actions → Release → Run workflow` 为已有 tag 追加附件。

> 本仓库的 Release 只发布**工具本体**（MIT 许可）。补丁后的 `PCL2.exe` 属于
> 《PCL 分发有限许可》范围，不建议随 Release 分发。

## 目录结构

```
PCL2Patch/
├── .github/workflows/release.yml  # 打 tag 自动构建并发布 Release
├── .gitignore
├── LICENSE              # MIT
├── src/                 # 补丁工具源码 (C# / .NET 8 / Mono.Cecil)
├── scripts/
│   ├── build.ps1        # 构建脚本
│   └── package-release.ps1  # 本地打包 Release 附件
├── docs/patch-notes.md  # 逐处 IL 修改说明
└── README.md
```

## 许可证

[MIT](LICENSE)

