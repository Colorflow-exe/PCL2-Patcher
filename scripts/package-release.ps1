# 构建补丁工具（Core + GUI），成品输出到仓库外的 ..\dist
# GUI 目标 .NET Framework 4.8（Windows 自带），Costura 合并为单 exe（无 dll、不落盘解压）。
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "..\dist"
Push-Location $root
try {
    New-Item -ItemType Directory -Force -Path $dist | Out-Null

    dotnet build "src\PCL2-Patcher.Gui\PCL2-Patcher.Gui.csproj" -c Release

    $built = Join-Path $root "src\PCL2-Patcher.Gui\bin\Release\net48\PCL2-Patcher.exe"
    Copy-Item $built (Join-Path $dist "PCL2-Patcher.exe") -Force
    $hash = (Get-FileHash (Join-Path $dist "PCL2-Patcher.exe") -Algorithm SHA512).Hash.ToLower()
    "$hash  PCL2-Patcher.exe" | Set-Content -Encoding ascii (Join-Path $dist "SHA-512.txt")

    Write-Host ""
    Write-Host "已生成：$dist"
    Get-ChildItem (Join-Path $dist "PCL2-Patcher.exe") | Format-Table Name, Length
    Write-Host "SHA-512.txt: $hash"
}
finally {
    Pop-Location
}
