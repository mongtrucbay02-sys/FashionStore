#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$temporaryRoot = Join-Path $projectRoot '.test-output'
$stage = Join-Path $temporaryRoot ('package-' + [guid]::NewGuid().ToString('N'))
$artifactRoot = Join-Path $projectRoot 'artifacts'
$archive = Join-Path $artifactRoot 'FashionStore_SQLServer.zip'

function Copy-SourceTree([string]$Source, [string]$Destination) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    foreach ($entry in Get-ChildItem -LiteralPath $Source -Force) {
        if ($entry.Name -in @('bin', 'obj', '.test-output', 'TestResults')) { continue }
        $target = Join-Path $Destination $entry.Name
        if ($entry.PSIsContainer) { Copy-SourceTree $entry.FullName $target }
        else { Copy-Item -LiteralPath $entry.FullName -Destination $target }
    }
}

try {
    New-Item -ItemType Directory -Path $stage, $artifactRoot -Force | Out-Null
    foreach ($folder in @('database','Controllers','Models','Services','Helpers','ViewModels','Views','Data','wwwroot','Properties','docs','tests','scripts')) {
        Copy-SourceTree (Join-Path $projectRoot $folder) (Join-Path $stage $folder)
    }
    foreach ($file in @('appsettings.json','Program.cs','OutfitStudio.csproj','README.md','.gitignore','.env.example','.editorconfig')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination $stage
    }
    New-Item -ItemType Directory -Path (Join-Path $stage 'App_Data') -Force | Out-Null
    foreach ($file in @('App_Data/users.json','App_Data/orders.json','Data/reviews.json')) {
        Set-Content -LiteralPath (Join-Path $stage $file) -Value '[]' -Encoding utf8
    }
    # Only packaged copies are cleared; the working website data is preserved.
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $archive, [IO.Compression.CompressionLevel]::Optimal, $false)
    Write-Output "Đã tạo: $archive"
} finally {
    $resolved = [IO.Path]::GetFullPath($stage)
    $allowed = [IO.Path]::GetFullPath($temporaryRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Đường dẫn đóng gói không hợp lệ.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}


