# 构建补丁工具（Core + GUI）
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet build "src\PCL2-Patcher.Core\PCL2-Patcher.Core.csproj" -c Release
    dotnet build "src\PCL2-Patcher.Gui\PCL2-Patcher.Gui.csproj" -c Release
    Write-Host ""
    Write-Host "GUI: src\PCL2-Patcher.Gui\bin\Release\net8.0-windows\PCL2-Patcher.exe"
}
finally {
    Pop-Location
}