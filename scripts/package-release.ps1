# 构建补丁工具（Core + GUI），成品输出到仓库外的 ..\dist
# 说明：WPF + WPF-UI 在 PublishSingleFile 下启动会崩溃，因此改用 self-contained 目录 + zip。
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "..\dist"
Push-Location $root
try {
    New-Item -ItemType Directory -Force -Path $dist | Out-Null
    $stage = Join-Path $dist "PCL2Patch"

    dotnet publish "src\PCL2Patch.Gui\PCL2Patch.Gui.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $stage

    $zip = Join-Path $dist "PCL2Patch-win-x64.zip"
    Remove-Item $zip -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal

    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
    "$hash  PCL2Patch-win-x64.zip" | Set-Content -Encoding ascii (Join-Path $dist "SHA256SUMS.txt")

    Write-Host ""
    Write-Host "已生成：$dist"
    Get-ChildItem $zip | Format-Table Name, Length
    Write-Host "SHA256: $hash"
}
finally {
    Pop-Location
}