# 本地打包 Release 附件（工具单文件 exe，不含 PCL2 二进制）
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet publish "src\PCL2Patch.Cli\PCL2Patch.Cli.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "out\fd"
    dotnet publish "src\PCL2Patch.Cli\PCL2Patch.Cli.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "out\sc"
    dotnet publish "src\PCL2Patch.Gui\PCL2Patch.Gui.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "out\gui"

    Copy-Item "out\fd\PCL2Patch.exe" "PCL2Patch.exe" -Force
    Copy-Item "out\sc\PCL2Patch.exe" "PCL2Patch-selfcontained.exe" -Force
    Copy-Item "out\gui\PCL2Patch.Gui.exe" "PCL2Patch.Gui.exe" -Force

    $lines = @(
        "$((Get-FileHash PCL2Patch.exe -Algorithm SHA256).Hash.ToLower())  PCL2Patch.exe",
        "$((Get-FileHash PCL2Patch-selfcontained.exe -Algorithm SHA256).Hash.ToLower())  PCL2Patch-selfcontained.exe",
        "$((Get-FileHash PCL2Patch.Gui.exe -Algorithm SHA256).Hash.ToLower())  PCL2Patch.Gui.exe"
    )
    $lines | Set-Content -Encoding ascii SHA256SUMS.txt

    Write-Host ""
    Write-Host "已生成 Release 附件："
    Get-Content SHA256SUMS.txt
    Get-ChildItem PCL2Patch*.exe | Format-Table Name, Length
}
finally {
    Pop-Location
}