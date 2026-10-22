# PCL2Patcher

直接修改官方 **Plain Craft Launcher 2** 的程序集 IL，解除 `RestrictMcDownload` 常量带来的下载 / 安装限制。不重新编译 PCL2，其余功能与官方版一致。

## 修改内容

| # | 位置 | 改动 |
|---|------|------|
| 1 | `PCL.PageDownloadInstall::LoaderInit` | 把「关闭下载」占位加载器换成 `DlClientListLoader`，恢复下载页版本列表 |
| 2 | `PCL.PageDownloadModify::LoadInstanceFinished` | Minecraft 原版卡片由 `Collapsed` 改为 `Visible` |
| 3 | `PCL.ModModpack::ModpackInstall()` | 恢复选择文件流程（搬运 41 条指令） |
| 4 | `PCL.PageDownloadResourceDetail::InitializeStackResourceVersions` | 点击版本改为进入安装（转调 `Install_Click`） |
| 5 | `PCL.ModModpack::ModpackInstall(string,string,string)` | 恢复完整安装分支（搬运 330 条指令，含 CurseForge / HMCL / MMC / MCBBS / Modrinth 等） |

## IL 修改细节

对象：`Plain Craft Launcher 2.exe`，AssemblyVersion `2.99.0.0`，`PCL.ModSecret.VersionBranchMain = "Official"`。

工具：Mono.Cecil 0.11.5，读入 → 修改 → 写出。所有修改点如下。

---

### 1. `PCL.PageDownloadInstall::LoaderInit`

原 IL（官方版，`RestrictMcDownload = True` 内联后）：

```
ldarg.0
call        ControlInit()
ldarg.0
callvirt    get_LoadMinecraft()
...         get_PanLoad() / get_PanAllBack()
ldnull
ldstr       "关闭下载"
ldsfld      _Closure$__::$IR3-3        ; 闭包缓存
brfalse.s   ...
...         构造 Action`1 指向 _Lambda$__R3-3（方法体直接 throw）
newobj      LoaderTask`2<String,DlClientListResult>::.ctor(
                String, Action`1<LoaderTask>, Func`1<TIn>, ThreadPriority)
ldarg.0
ldftn       _Lambda$__R3-4
newobj      Action`1
ldnull
ldc.i4.1
call        MyPageRight::PageLoaderInit(...)
ret
```

修改：删除 `ldstr "关闭下载"` 到构造 `LoaderTask` 的 `newobj`（含闭包缓存分支），
插入：

```
ldsfld      PCL.ModDownload::DlClientListLoader
```

结果：`PageLoaderInit(..., DlClientListLoader, AddressOf LoadMinecraft_OnFinish, Nothing, True)`，
即恢复 `RestrictMcDownload = False` 时的源码分支。

---

### 2. `PCL.PageDownloadModify::LoadInstanceFinished`

源码：

```vb
CardMinecraft.Visibility = If(RestrictMcDownload, Visibility.Collapsed, Visibility.Visible)
```

`True` 时编译为：

```
ldarg.0
callvirt    PCL.PageDownloadInstall::get_CardMinecraft()
ldc.i4.2                    ; Visibility.Collapsed
callvirt    UIElement::set_Visibility(Visibility)
```

修改：`ldc.i4.2` → `ldc.i4.0`（Visible）。

注意：同方法后面 `BtnBack.Visibility` 也是 `ldc.i4.2`，只改 `get_CardMinecraft` 后 3 条内的那一条。

---

### 3. `PCL.ModModpack::ModpackInstall()`（无参重载）

源码：

```vb
Public Sub ModpackInstall()
    If RestrictMcDownload Then
        Hint("为遵守相关法律法规……", HintType.Red)
        Return
    End If
    Dim File = Dialogs.SelectFile(...)
    ...
End Sub
```

官方版只剩 3 条指令（`newobj Closure` / `stloc.0` / `ret`），选择文件逻辑被删。

修改：从参考 exe（`RestrictMcDownload = False` 编译）整体搬运该方法体，共 41 条指令，
包括 `Dialogs.SelectFile`、`FirstOrDefault`、`RunInThread` 等。

---

### 4. `PCL.PageDownloadResourceDetail::InitializeStackResourceVersions`

源码：

```vb
If Not RestrictMcDownload AndAlso TargetResourceType = ResourceTypes.ModPack Then
    Stack.Children.Add(Version.ToListItem(AddressOf Install_Click, AddressOf Save_Click, ...))
Else
    Stack.Children.Add(Version.ToListItem(AddressOf Save_Click, ...))
End If
```

官方版条件恒假，`if` 分支被删除，循环内只剩：

```
ldarg.1
callvirt    get_Children()
ldloc.s     V_7
ldarg.0
ldftn       Save_Click(Object, EventArgs)
newobj      ClickEventHandler
ldnull
ldloc.1
callvirt    ResourceVersion::ToListItem(ClickEventHandler, MyIconButton/ClickEventHandler, Boolean)
callvirt    UIElementCollection::Add
pop
```

`Install_Click(MyListItem, EventArgs)` 方法本身仍存在（199 条指令），并且官方版里还存在
适配它的 lambda `_Lambda$__R18-5(Object, MouseButtonEventArgs)`（6 条指令，内部转调 `Install_Click`）。

修改：把循环内的 `ldftn Save_Click` 替换为 `ldftn _Lambda$__R18-5`。这样点击列表项走安装流程。

副作用：右侧图标按钮参数仍是 `ldnull`（无保存按钮）——与"直接安装"的旧版行为一致。

---

### 5. `PCL.ModModpack::ModpackInstall(string,string,string)`

源码第 88 行：

```vb
If RestrictMcDownload Then Throw New Exception("为遵守相关法律法规……")
Select Case PackType
    Case 0 : Return InstallPackCurseForge(...)
    ...
```

`True` 时整段 `Select Case`（76 条指令）与正常 `ret` 都被编译器判定不可达而删除，
官方版方法只剩 254 条指令，末尾是：

```
IL_02cd: ldstr "打开整合包文件失败，文件可能损坏……"
...
IL_02d9: throw              ; catch 块内（文件损坏）
IL_02da: ldstr "为遵守相关法律法规……"
IL_02df: newobj Exception(string)
IL_02e4: throw              ; 限制 throw
IL_02e5: ldloc.1            ; 外层 finally
...
IL_02ee: endfinally
```

**只删这三条 throw 会导致 `InvalidProgramException`**：`leave.s IL_02da` 会直接落进
finally handler，且正常返回路径已不存在。

修改：从参考 exe 整体搬运该方法体（330 条指令），包含：

- `InstallPackCurseForge` / `InstallPackHMCL` / `InstallPackMMC` / `InstallPackMCBBS` /
  `InstallPackModrinth` / `InstallPackLauncherPack` / `InstallPackCompress` 全部分支；
- 正常返回 `ldloc.0; ret`；
- 三个异常处理器（内层 `finally` ×1、`catch Exception` ×1、外层 `finally` ×1）。

搬运时逐条克隆指令，用 `module.ImportReference` 重建跨模块元数据引用，并重建局部变量与
异常处理器边界（`TryStart/TryEnd/HandlerStart/HandlerEnd/FilterStart`）。

---

### 验证记录

- `PEVerify /nologo /il`：仅 `PCL.ModLoader+LoaderTask`2::.ctor` 的泛型签名误报，
  官方原版同样存在，新增补丁无 IL 错误。
- 工具对官方原版 + 参考版执行 `apply` 的输出，与手工分步补丁的产物 SHA256 一致。
- 运行：窗口正常创建；日志 `管理员权限：True`；`DlClientList Mojang ... Finished`。
- 安装：点击整合包版本进入安装流程，`ModpackInstall` 正常执行到 `InstallPackModrinth`。