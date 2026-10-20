# 构建补丁工具（Core + GUI）
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet build "src\PCL2Patch.Core\PCL2Patch.Core.csproj" -c Release
    dotnet build "src\PCL2Patch.Gui\PCL2Patch.Gui.csproj" -c Release
    Write-Host ""
    Write-Host "GUI: src\PCL2Patch.Gui\bin\Release\net8.0-windows\PCL2Patch.exe"
}
finally {
    Pop-Location
}