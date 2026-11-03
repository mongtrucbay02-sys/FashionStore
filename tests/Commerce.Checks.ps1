#Requires -Version 7.0
param([string]$BaseUri = 'http://127.0.0.1:5086', [Parameter(Mandatory)][string]$StateDirectory)
$ErrorActionPreference = 'Stop'
Import-Module Microsoft.PowerShell.Utility
$base = $BaseUri.TrimEnd('/')
$count = 0
$session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
function Page($path){Invoke-WebRequest ($base+$path) -WebSession $session -TimeoutSec 12 -SkipHttpErrorCheck}
function Token($path){$page=Page $path;[Net.WebUtility]::HtmlDecode([regex]::Match($page.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value)}
function Post($path,$source,$body){$session.Headers.Remove('RequestVerificationToken')|Out-Null;$body['__RequestVerificationToken']=Token $source;Invoke-WebRequest ($base+$path) -Method Post -Body $body -ContentType 'application/x-www-form-urlencoded' -WebSession $session -TimeoutSec 12 -SkipHttpErrorCheck}
function Assert($ok,$name){if(!$ok){throw $name};$script:count++;Write-Output "PASS: $name"}
$before=@{};foreach($f in 'App_Data/users.json','App_Data/orders.json'){if(Test-Path -LiteralPath $f){$before[$f]=(Get-FileHash -LiteralPath $f).Hash}}
$page=Page '/Outfit'
Assert ($page.StatusCode -eq 200 -and $page.Content.Contains('outfit-preview') -and $page.Content.Contains('data-size-advisor') -and !$page.Content.Contains('id="outfit-ai"') -and !$page.Content.Contains('id="recommendations"')) 'Outfit page retains size advice and real-product preview without AI or style suggestions'
$encoded=[regex]::Match($page.Content,'<script id="catalog-data" type="application/json">(.*?)</script>','Singleline').Groups[1].Value
$catalog=$encoded|ConvertFrom-Json
Assert ($catalog.products.Count -eq 31 -and @($catalog.products|Where-Object category -eq set).Count -eq 7 -and @($catalog.products|Where-Object category -eq shoes).Count -eq 0) '31 new products include sets and no invented shoes'
foreach($p in $catalog.products){if((Page $p.image).StatusCode -ne 200){throw ('Missing image: '+$p.id)}}
Assert $true 'All 31 cropped photos are served successfully'
Assert ((Page '/images/outfit-ai/default-look.png').StatusCode -eq 404 -and (Page '/images/outfits/polo-black.svg').StatusCode -eq 404) 'Obsolete images are removed'
foreach ($endpoint in @('/Outfit/Advice','/Outfit/GenerateLook','/Outfit/Suggestions')) {
    $r=Invoke-WebRequest ($base+$endpoint) -Method Post -Body '{}' -ContentType 'application/json' -WebSession $session -TimeoutSec 12 -SkipHttpErrorCheck
    Assert ($r.StatusCode -eq 404) ('Removed outfit AI/suggestion endpoint is unavailable: '+$endpoint)
}
$script=Page '/js/outfit.js'
Assert ($script.StatusCode -eq 200 -and !$script.Content.Contains('fetch(') -and !$script.Content.Contains('lookPhoto') -and !$script.Content.Contains('initAI')) 'Outfit client has no AI requests or saved AI-photo references'
$email='admin@fashionstore.local'
$response=Post '/Account/Login' '/Account/Login' @{Email=$email;Password='AdminDemo!2026'}
$r=Post '/Cart/AddOutfit' '/Outfit' @{mode='set';setId='loop-set';setSize='M'}
Assert ($r.StatusCode -eq 200 -and [Net.WebUtility]::HtmlDecode($r.Content).Contains('Loop Set')) 'Full set adds to cart as one item'
$r=Post '/Order/Checkout' '/Order/Checkout' @{FullName='Khach thu';Phone='0900000000';Address='Dia chi kiem thu';PaymentMethod='COD'}
$ordersPath=Join-Path $StateDirectory 'orders.json'
$orders=Get-Content -LiteralPath $ordersPath -Raw|ConvertFrom-Json
$o=$orders|Where-Object UserEmail -eq $email|Select-Object -Last 1
Assert ($null -ne $o -and $o.Items.Count -eq 1 -and $o.Items[0].ProductId -eq 2023 -and $o.Subtotal -eq 1970000 -and $o.Total -eq 2000000) 'Set checkout stores correct product and server price'
$r=Post '/Cart/AddOutfit' '/Outfit' @{mode='separates';shirtId='loop-blazer';pantsId='loop-pant';shirtSize='M';pantsSize='L'}
$decoded=[Net.WebUtility]::HtmlDecode($r.Content)
Assert ($decoded.Contains('Loop Blazer') -and $decoded.Contains('Loop Pant')) 'Separates add two items with independent sizes'
Assert ((Post '/Cart/AddOutfit' '/Outfit' @{mode='set';setId='loop-set';setSize='invalid'}).StatusCode -eq 400) 'Unsupported set size rejected'
Assert ((Post '/Cart/AddOutfit' '/Outfit' @{mode='separates';shirtId='polo-black';pantsId='loop-pant';shirtSize='M';pantsSize='M'}).StatusCode -eq 400) 'Old catalog IDs cannot be purchased'
$detail=Page ('/Order/Detail/'+$o.Id)
Assert ([Net.WebUtility]::HtmlDecode($detail.Content).Contains('Loop Set')) 'Completed order detail preserves item name'
# Promote only this isolated test account to check the admin product form.
$testUsersPath=Join-Path $StateDirectory 'users.json'
$testUsers=Get-Content -LiteralPath $testUsersPath -Raw|ConvertFrom-Json
foreach($testUser in $testUsers){if($testUser.Email -eq $email){$testUser.Role='Admin'}}
ConvertTo-Json -InputObject @($testUsers) -Depth 20 | Set-Content -LiteralPath $testUsersPath -Encoding utf8
$admin=Page '/Admin/Dashboard'
Assert ($admin.StatusCode -eq 200 -and !$admin.Content.Contains('ProductPhoto') -and !$admin.Content.Contains('Studio AI')) 'Admin navigation has no Studio AI entry'
$form=Page '/Admin/CreateProduct'
Assert ($form.StatusCode -eq 200 -and $form.Content.Contains('id="imageFile"') -and $form.Content.Contains('id="imageUrl"') -and !$form.Content.Contains('photo-widget') -and !$form.Content.Contains('Studio AI')) 'Admin keeps normal image upload and preview without AI controls'
foreach($endpoint in @('/ProductPhoto','/ProductPhoto/Generate','/ProductPhoto/Apply','/ProductPhoto/Download')) {
    Assert ((Page $endpoint).StatusCode -eq 404) ('Removed Studio route unavailable: '+$endpoint)
}
foreach($f in $before.Keys){Assert ((Get-FileHash -LiteralPath $f).Hash -eq $before[$f]) ('Real user data unchanged: '+[IO.Path]::GetFileName($f))}
Write-Output "Completed $count HTTP checks with isolated JSON data. No paid provider request was made."

