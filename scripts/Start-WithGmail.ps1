#Requires -Version 7.0
# Compatibility launcher: SMTP is no longer needed.
[CmdletBinding()]
param([string]$Email, [switch]$SeedAdmin, [int]$Port = 5080)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($Port -lt 1024 -or $Port -gt 65535) { throw 'Cổng phải từ 1024 đến 65535.' }
if ($SeedAdmin) {
    if (!$Email) { $Email = Read-Host 'Email tài khoản quản trị demo' }
    $previousEmail = $env:AdminSeed__Email
    try {
        $env:AdminSeed__Email = $Email.Trim()
        & dotnet run --project (Join-Path $projectRoot 'OutfitStudio.csproj') -- --seed-admin-demo
        if ($LASTEXITCODE -ne 0) { throw 'Không tạo được tài khoản quản trị demo.' }
    } finally { $env:AdminSeed__Email = $previousEmail }
}
& dotnet run --project (Join-Path $projectRoot 'OutfitStudio.csproj') --urls "http://localhost:$Port"
