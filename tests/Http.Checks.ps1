#Requires -Version 7.0
param([string]$BaseUri = 'http://127.0.0.1:5086')
$ErrorActionPreference = 'Stop'
Import-Module Microsoft.PowerShell.Utility
$base = $BaseUri.TrimEnd('/')
$count = 0
$session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
function Page($p){Invoke-WebRequest ($base+$p) -WebSession $session -TimeoutSec 12 -SkipHttpErrorCheck}
function Token($p){$page=Page $p;[Net.WebUtility]::HtmlDecode([regex]::Match($page.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value)}
function Post($p,$source,$body){$session.Headers.Remove('RequestVerificationToken')|Out-Null;$body.__RequestVerificationToken=Token $source;Invoke-WebRequest ($base+$p) -Method Post -Body $body -ContentType 'application/x-www-form-urlencoded' -WebSession $session -TimeoutSec 12 -SkipHttpErrorCheck}
function Generate($fields){$fields.__RequestVerificationToken=Token '/TryOn';Invoke-WebRequest ($base+'/TryOn/Generate') -Method Post -Form $fields -WebSession $session -TimeoutSec 12 -SkipHttpErrorCheck}
function Assert($ok,$name){if(!$ok){throw $name};$script:count++;Write-Output "PASS: $name"}
$before=@{};foreach($p in 'App_Data/users.json','App_Data/orders.json'){if(Test-Path -LiteralPath $p){$before[$p]=(Get-FileHash -LiteralPath $p).Hash}}
$p=Page '/TryOn';Assert ($p.StatusCode -eq 200 -and $p.Content.Contains('tryon-person') -and $p.Content.Contains('tryon-paint') -and $p.Content.Contains('tryon-description')) 'Public page exposes personal-photo try-on and painting controls'
$r=Generate @{source='description';description='Ao trang'};Assert ($r.BaseResponse.RequestMessage.RequestUri.AbsolutePath -eq '/Account/Login') 'Anonymous paid requests require login'
$email='admin@fashionstore.local'
$r=Post '/Account/Login' '/Account/Login' @{Email=$email;Password='AdminDemo!2026'}
$fixtures=Join-Path (Get-Location) 'tests/VirtualTryOn.Checks/Fixtures'
$body=@{source='description';description='Ao thun trang';scope='top';person=Get-Item -LiteralPath (Join-Path $fixtures 'person.png')}
Assert ((Generate $body.Clone()).StatusCode -eq 503) 'Missing API key returns clear failure, never a fake photo'
Assert ((Generate @{source='description';description='Ao'}).StatusCode -eq 400) 'Missing personal photo rejected'
$bad=$body.Clone();$bad.mask=Get-Item -LiteralPath (Join-Path $fixtures 'wrong-mask.png');Assert ((Generate $bad).StatusCode -eq 400) 'Mismatched mask size rejected'
$bad=$body.Clone();$bad.mask=Get-Item -LiteralPath (Join-Path $fixtures 'edited.png');Assert ((Generate $bad).StatusCode -eq 400) 'Opaque RGB mask rejected'
$good=$body.Clone();$good.mask=Get-Item -LiteralPath (Join-Path $fixtures 'mask.png');Assert ((Generate $good).StatusCode -eq 503) 'Valid alpha mask accepted and reaches API-key check'
$bad=$body.Clone();$bad.scope='face';Assert ((Generate $bad).StatusCode -eq 400) 'Unknown replacement scope rejected'
$bad=$body.Clone();$bad.source='upload';Assert ((Generate $bad).StatusCode -eq 400) 'Upload mode requires garment files'
$bad=$body.Clone();$bad.source='catalog';$bad.mode='separates';$bad.shirtId='loop-pant';Assert ((Generate $bad).StatusCode -eq 400) 'Catalog shirt field cannot reference pants'
$body.__RequestVerificationToken='';$r=Invoke-WebRequest ($base+'/TryOn/Generate') -Method Post -Form $body -WebSession $session -TimeoutSec 12 -SkipHttpErrorCheck;Assert ($r.StatusCode -eq 400) 'Generate requires antiforgery token'
Assert ((Page '/TryOn/Generate').StatusCode -eq 405) 'Generate is POST-only'
foreach($p in $before.Keys){Assert ((Get-FileHash -LiteralPath $p).Hash -eq $before[$p]) ('Real user data unchanged: '+[IO.Path]::GetFileName($p))}
Assert (!(Test-Path -LiteralPath 'wwwroot/images/tryon')) 'No personal/result public image folder created'
Write-Output "Completed $count production HTTP checks with isolated JSON. No Cloudflare request was made."


