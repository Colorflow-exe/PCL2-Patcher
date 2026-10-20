# 更新日志

## v0.1.0 (2026-10-20)

首个版本。

- 补丁：解除 PCL2 下载/安装限制（5 处 IL 修改）
  - 下载页版本列表恢复 `DlClientListLoader`
  - 修改页 Minecraft 原版卡片显示
  - 整合包版本列表点击恢复为“安装”
  - 搬运 `ModpackInstall()` 完整选文件流程
  - 搬运 `ModpackInstall(文件)` 完整安装分支（330 条指令）
- GUI：WPF + Fluent（WPF-UI），支持浅色 / 深色 / 跟随系统主题
- 内置参考程序集（gzip 压缩），无需用户提供参考 exe
- 单文件 self-contained 发布，免安装 .NET 运行时