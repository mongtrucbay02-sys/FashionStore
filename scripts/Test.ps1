#Requires -Version 7.0
[CmdletBinding()]
param([switch]$SkipHttp, [switch]$SqlServer)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = Join-Path $projectRoot '.test-output'
$runRoot = Join-Path $outputRoot ('run-' + [guid]::NewGuid().ToString('N'))
$previousDirectory = Get-Location
$server = $null

function Assert-Command {
    if ($LASTEXITCODE -ne 0) { throw "Lệnh kiểm tra thất bại: $LASTEXITCODE" }
}

try {
    Set-Location -LiteralPath $projectRoot
    New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
    $appOutput = Join-Path $runRoot 'app'
    dotnet build OutfitStudio.csproj --configuration Release --output $appOutput --nologo
    Assert-Command

    foreach ($suite in @('CloudflareImage.Checks', 'VirtualTryOn.Checks', 'MensCatalog.Checks', 'Store.Checks')) {
        $suiteOutput = Join-Path $runRoot $suite
        dotnet run --project "tests/$suite" --configuration Release -p:UseAppHost=false "-p:OutputPath=$suiteOutput/"
        Assert-Command
    }

    if ($SqlServer) {
        $sqlOutput = Join-Path $runRoot 'SqlServer.Checks'
        dotnet run --project 'tests/Store.Checks' --configuration Release -p:UseAppHost=false "-p:OutputPath=$sqlOutput/" -- --sql
        Assert-Command
    }
    foreach ($file in Get-ChildItem -LiteralPath 'wwwroot/js' -Filter '*.js') {
        node --check $file.FullName
        Assert-Command
    }
    node tests/size-guide.checks.cjs
    Assert-Command
    node tests/apidog.checks.cjs
    Assert-Command

    if (-not $SkipHttp) {
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $listener.Start()
        $port = $listener.LocalEndpoint.Port
        $listener.Stop()
        $baseUri = "http://127.0.0.1:$port"
        $stateRoot = Join-Path $runRoot 'state'
        $catalogRoot = Join-Path $runRoot 'catalog'
        New-Item -ItemType Directory -Path $catalogRoot -Force | Out-Null
        Copy-Item -LiteralPath 'Data/products.json', 'Data/categories.json' -Destination $catalogRoot
        Set-Content -LiteralPath (Join-Path $catalogRoot 'reviews.json') -Value '[]' -Encoding utf8

        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = (Get-Command dotnet).Source
        $start.WorkingDirectory = $projectRoot
        $start.ArgumentList.Add((Join-Path $appOutput 'OutfitStudio.dll'))
        $start.ArgumentList.Add('--urls')
        $start.ArgumentList.Add($baseUri)
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.Environment['Storage__Provider'] = 'Json'
        $start.Environment['Storage__RuntimeDirectory'] = $stateRoot
        $start.Environment['Storage__CatalogDirectory'] = $catalogRoot
        $start.Environment['Logging__LogLevel__Default'] = 'Warning'
        foreach ($key in @('CLOUDFLARE_API_TOKEN', 'CLOUDFLARE_ACCOUNT_ID', 'CloudflareAI__ApiKey', 'CloudflareAI__AccountId')) {
            $start.Environment[$key] = ''
        }
        # Commerce/try-on checks use a CLI-seeded isolated fixture.
        $start.Environment['AdminSeed__Email'] = 'admin@fashionstore.local'
        foreach ($emailSetting in @('Host','Username','Password','FromAddress')) { $start.Environment['Email__' + $emailSetting] = '' }
        $start.ArgumentList.Add('--seed-admin-demo')
        $seed = [Diagnostics.Process]::Start($start)
        $seedOut = $seed.StandardOutput.ReadToEndAsync()
        $seedErr = $seed.StandardError.ReadToEndAsync()
        $seed.WaitForExit()
        if ($seed.ExitCode -ne 0) { throw 'Không tạo được tài khoản kiểm thử tách biệt.' }
        $seed.Dispose()
        $start.ArgumentList.Remove('--seed-admin-demo') | Out-Null
        $server = [Diagnostics.Process]::Start($start)
        $stdout = $server.StandardOutput.ReadToEndAsync()
        $stderr = $server.StandardError.ReadToEndAsync()
        $ready = $false
        $deadline = [DateTime]::UtcNow.AddSeconds(20)
        while ([DateTime]::UtcNow -lt $deadline -and -not $server.HasExited) {
            try {
                $page = Invoke-WebRequest "$baseUri/TryOn" -TimeoutSec 2 -SkipHttpErrorCheck
                if ($page.StatusCode -eq 200) { $ready = $true; break }
            } catch { Start-Sleep -Milliseconds 250 }
        }
        if (-not $ready) { throw 'Máy chủ HTTP kiểm thử chưa khởi động được.' }
        & (Join-Path $projectRoot 'tests/Http.Checks.ps1') -BaseUri $baseUri
        & (Join-Path $projectRoot 'tests/Commerce.Checks.ps1') -BaseUri $baseUri -StateDirectory $stateRoot
    }

    Write-Output 'Tất cả kiểm tra đã qua. Không gọi API AI thật.'
} finally {
    if ($null -ne $server) {
        if (-not $server.HasExited) { $server.Kill($true) }
        $server.WaitForExit()
        $server.Dispose()
    }
    Set-Location -LiteralPath $previousDirectory
    $resolved = [IO.Path]::GetFullPath($runRoot)
    $allowed = [IO.Path]::GetFullPath($outputRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Đường dẫn dọn kiểm thử không hợp lệ.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}



