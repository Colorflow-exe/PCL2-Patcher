# 构建补丁工具
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location (Join-Path $root "src")
try {
    dotnet build -c Release
    Write-Host ""
    Write-Host "产物: src\bin\Release\net8.0\PCL2Patch.exe"
}
finally {
    Pop-Location
}
