# 构建补丁工具（Core / CLI / GUI）
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet build "src\PCL2Patch.Core\PCL2Patch.Core.csproj" -c Release
    dotnet build "src\PCL2Patch.Cli\PCL2Patch.Cli.csproj" -c Release
    dotnet build "src\PCL2Patch.Gui\PCL2Patch.Gui.csproj" -c Release
    Write-Host ""
    Write-Host "CLI: src\PCL2Patch.Cli\bin\Release\net8.0\PCL2Patch.exe"
    Write-Host "GUI: src\PCL2Patch.Gui\bin\Release\net8.0-windows\PCL2Patch.Gui.exe"
}
finally {
    Pop-Location
}