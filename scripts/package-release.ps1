# 本地打包 Release 附件（工具本体，不含 PCL2 二进制）
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    New-Item -ItemType Directory -Force -Path out | Out-Null

    dotnet publish "src\PCL2Patch.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -o "out\framework"
    Compress-Archive -Path "out\framework\*" -DestinationPath "PCL2Patch-win-x64-framework-dependent.zip" -Force

    dotnet publish "src\PCL2Patch.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "out\selfcontained"
    Compress-Archive -Path "out\selfcontained\*" -DestinationPath "PCL2Patch-win-x64-selfcontained.zip" -Force

    $lines = Get-ChildItem *.zip | ForEach-Object {
        "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)"
    }
    $lines | Set-Content -Encoding ascii SHA256SUMS.txt
    Write-Host ""
    Write-Host "已生成 Release 附件："
    Get-Content SHA256SUMS.txt
}
finally {
    Pop-Location
}
