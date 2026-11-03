#Requires -Version 7.0
[CmdletBinding()]
param([string]$Server = '.\SQLEXPRESS', [string]$Database = 'FashionStoreDB')
$ErrorActionPreference = 'Stop'
if ($Server -notmatch '^[A-Za-z0-9_.,\\()\-]+$' -or $Database -notmatch '^[A-Za-z][A-Za-z0-9_]{0,127}$') { throw 'Tên server/database không hợp lệ.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = (Get-Command dotnet).Source
$start.WorkingDirectory = $projectRoot
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.Environment['Storage__Provider'] = 'SqlServer'
$start.Environment['ConnectionStrings__FashionStore'] = "Server=$Server;Database=$Database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10"
foreach ($arg in @('run','--project',(Join-Path $projectRoot 'OutfitStudio.csproj'),'--','--init-sql')) { $start.ArgumentList.Add($arg) }
Write-Output 'Dừng website JSON cũ trước khi chuyển dữ liệu. Chỉ nhập vào database trống; các file JSON gốc được giữ nguyên.'
$child = [Diagnostics.Process]::Start($start)
try { $child.WaitForExit(); if ($child.ExitCode -ne 0) { throw 'Khởi tạo SQL Server không thành công. Xem thông báo bên trên.' } } finally { $child.Dispose() }
