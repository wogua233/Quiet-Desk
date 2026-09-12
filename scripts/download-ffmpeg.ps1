$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$manifest=Get-Content (Join-Path $root 'docs/ffmpeg-manifest.json') -Raw|ConvertFrom-Json
$archive=Join-Path $root '.tools/ffmpeg-pinned.zip'
New-Item -ItemType Directory -Force (Split-Path -Parent $archive)|Out-Null
if(-not (Test-Path -LiteralPath $archive)){Invoke-WebRequest $manifest.url -OutFile $archive}
$expected=$manifest.digest.Replace('sha256:','')
if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLower() -ne $expected){throw 'FFmpeg SHA256 mismatch; refusing a different version.'}
$destination=Join-Path $root '.tools/ffmpeg-pinned'
Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
$package=Get-ChildItem -LiteralPath $destination -Directory|Select-Object -First 1
$assets=Join-Path $root 'src/QuietDesk/Assets/FFmpeg'
New-Item -ItemType Directory -Path $assets -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $package.FullName 'bin/ffmpeg.exe') -Destination $assets -Force
Get-ChildItem -LiteralPath (Join-Path $package.FullName 'bin') -Filter '*.dll'|Copy-Item -Destination $assets -Force
Copy-Item -LiteralPath (Join-Path $package.FullName 'LICENSE.txt') -Destination $assets -Force
Write-Output 'Pinned LGPL shared decoder restored. Keep bundled license and source documents.'
