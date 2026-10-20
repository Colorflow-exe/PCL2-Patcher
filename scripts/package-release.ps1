# 构建补丁工具（Core + GUI），成品输出到仓库外的 ..\dist
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "..\dist"
Push-Location $root
try {
    New-Item -ItemType Directory -Force -Path $dist | Out-Null
    $stage = Join-Path $dist "_build"

    dotnet publish "src\PCL2Patch.Gui\PCL2Patch.Gui.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $stage

    Copy-Item (Join-Path $stage "PCL2Patch.exe") (Join-Path $dist "PCL2Patch.exe") -Force
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