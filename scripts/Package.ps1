param([string]$Dotnet = 'dotnet', [string]$OutputRoot = 'artifacts/release')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$tag = 'v1.3.1-beta.1'
$out = [IO.Path]::GetFullPath($OutputRoot)
$workspace = [IO.Path]::GetFullPath((Get-Location).Path)
if (!$out.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be inside workspace' }
New-Item -ItemType Directory -Path $out -Force | Out-Null
$zip = Join-Path $out "Manyblox-$tag-win-x64.zip"
if (Test-Path -LiteralPath $zip) { throw 'Refusing to overwrite a package' }
$stage = Join-Path $out ('stage-' + [guid]::NewGuid().ToString('N'))
& $Dotnet restore src/Manyblox/Manyblox.csproj -r win-x64 --locked-mode --configfile NuGet.Config --artifacts-path artifacts/build -p:RuntimeFrameworkVersion=10.0.12
if ($LASTEXITCODE) { throw 'Restore failed' }
& $Dotnet publish src/Manyblox/Manyblox.csproj -c Release -r win-x64 --self-contained true --no-restore --artifacts-path artifacts/build -p:RuntimeFrameworkVersion=10.0.12 -o $stage
if ($LASTEXITCODE) { throw 'Publish failed' }
Copy-Item LICENSE,THIRD_PARTY_NOTICES.md -Destination $stage
New-Item -ItemType Directory -Path (Join-Path $stage 'licenses') | Out-Null
$packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
Copy-Item (Join-Path $packages 'microsoft.web.webview2/1.0.4258.31/LICENSE.txt') (Join-Path $stage 'licenses/WebView2-LICENSE.txt')
Copy-Item (Join-Path $packages 'microsoft.web.webview2/1.0.4258.31/NOTICE.txt') (Join-Path $stage 'licenses/WebView2-NOTICE.txt')
Copy-Item (Join-Path $packages 'microsoft.netcore.app.runtime.win-x64/10.0.12/LICENSE.TXT') (Join-Path $stage 'LICENSE.txt')
Copy-Item (Join-Path $packages 'microsoft.netcore.app.runtime.win-x64/10.0.12/THIRD-PARTY-NOTICES.TXT') (Join-Path $stage 'ThirdPartyNotices.txt')
Copy-Item (Join-Path $packages 'microsoft.windowsdesktop.app.runtime.win-x64/10.0.12/LICENSE') (Join-Path $stage 'licenses/WindowsDesktop-LICENSE.txt')
Get-ChildItem (Join-Path $stage 'runtimes') -Directory -ErrorAction SilentlyContinue | Where-Object Name -ne 'win-x64' | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force }
Get-ChildItem $stage -Filter '*.pdb' -File | Remove-Item -Force
$git = if (Get-Command git -ErrorAction SilentlyContinue) { 'git' } else { 'C:\Program Files\Git\cmd\git.exe' }
$commit = (& $git rev-parse HEAD).Trim()
$manifest = Get-ChildItem $stage -File -Recurse | ForEach-Object { [ordered]@{ path=$_.FullName.Substring($stage.Length + 1).Replace('\','/'); bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } }
[ordered]@{ version=$tag; sourceCommit=$commit; runtime='10.0.12'; selfContained=$true; files=@($manifest) } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $stage 'BUILD_MANIFEST.json') -Encoding UTF8
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Package: $zip"
Write-Output "SHA256: $hash"
