# 构建补丁工具（Core + GUI）
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet build "src\PCL2Patcher.Core\PCL2Patcher.Core.csproj" -c Release
    dotnet build "src\PCL2Patcher.Gui\PCL2Patcher.Gui.csproj" -c Release
    Write-Host ""
    Write-Host "GUI: src\PCL2Patcher.Gui\bin\Release\net8.0-windows\PCL2Patcher.exe"
}
finally {
    Pop-Location
}