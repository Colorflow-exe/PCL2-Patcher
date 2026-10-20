# 构建补丁工具（Core + GUI），成品输出到仓库外的 ..\dist
# GUI 目标 .NET Framework 4.8（Windows 自带），Costura 合并为单 exe（无 dll、不落盘解压）。
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "..\dist"
Push-Location $root
try {
    New-Item -ItemType Directory -Force -Path $dist | Out-Null

    dotnet build "src\PCL2Patch.Gui\PCL2Patch.Gui.csproj" -c Release

    $built = Join-Path $root "src\PCL2Patch.Gui\bin\Release\net48\PCL2Patch.exe"
    Copy-Item $built (Join-Path $dist "PCL2Patch.exe") -Force
    $hash = (Get-FileHash (Join-Path $dist "PCL2Patch.exe") -Algorithm SHA256).Hash.ToLower()
    "$hash  PCL2Patch.exe" | Set-Content -Encoding ascii (Join-Path $dist "SHA256SUMS.txt")

    Write-Host ""
    Write-Host "已生成：$dist"
    Get-ChildItem (Join-Path $dist "PCL2Patch.exe") | Format-Table Name, Length
    Write-Host "SHA256: $hash"
}
finally {
    Pop-Location
}