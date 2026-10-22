# PCL2Patcher

直接修改官方 **Plain Craft Launcher 2** 的程序集 IL，解除 `RestrictMcDownload` 常量带来的下载 / 安装限制。不重新编译 PCL2，其余功能与官方版一致。

## 优势

可作用于非开源版、快照版

## 修改内容

| # | 位置 | 改动 |
|---|------|------|
| 1 | `PCL.PageDownloadInstall::LoaderInit` | 把「关闭下载」占位加载器换成 `DlClientListLoader`，恢复下载页版本列表 |
| 2 | `PCL.PageDownloadModify::LoadInstanceFinished` | Minecraft 原版卡片由 `Collapsed` 改为 `Visible` |
| 3 | `PCL.ModModpack::ModpackInstall()` | 恢复选择文件流程（搬运 41 条指令） |
| 4 | `PCL.PageDownloadResourceDetail::InitializeStackResourceVersions` | 点击版本改为进入安装（转调 `Install_Click`） |
| 5 | `PCL.ModModpack::ModpackInstall(string,string,string)` | 恢复完整安装分支（搬运 330 条指令，含 CurseForge / HMCL / MMC / MCBBS / Modrinth 等） |

详细 IL 说明见 [docs/patch-notes.md](docs/patch-notes.md)。

## 声明

本项目全部代码由 DeepSeek 生成。
