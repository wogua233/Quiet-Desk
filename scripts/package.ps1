$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$release=Join-Path $projectRoot 'artifacts\QuietDesk-v0.5.2-win-x64'
if(-not (Test-Path -LiteralPath (Join-Path $release 'QuietDesk.exe'))){throw 'Publish the release first.'}
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $release 'README.md')
$releaseDocs=Join-Path $release 'docs'
New-Item -ItemType Directory -Path $releaseDocs -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') | Copy-Item -Destination $releaseDocs -Recurse -Force
Compress-Archive -Path (Join-Path $release '*') -DestinationPath (Join-Path $projectRoot 'artifacts\QuietDesk-win-x64.zip') -Force
$stage=Join-Path $projectRoot ('artifacts\source-'+[DateTime]::Now.ToString('yyyyMMddHHmmss'))
New-Item -ItemType Directory -Path $stage | Out-Null
foreach($folder in @('src','docs','scripts')){
    Get-ChildItem -LiteralPath (Join-Path $projectRoot $folder) -File -Recurse | Where-Object {$_.FullName -notmatch '\\(bin|obj)\\'} | ForEach-Object {
        $relative=$_.FullName.Substring($projectRoot.Length+1)
        $destination=Join-Path $stage $relative
        New-Item -ItemType Directory -Force (Split-Path -Parent $destination) | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination
    }
}
foreach($file in @('README.md','global.json','.gitignore')){Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination (Join-Path $stage $file)}
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath (Join-Path $projectRoot 'artifacts\QuietDesk-source.zip') -Force
Get-FileHash -LiteralPath (Join-Path $projectRoot 'artifacts\QuietDesk-win-x64.zip'),(Join-Path $projectRoot 'artifacts\QuietDesk-source.zip') -Algorithm SHA256 | Format-Table -AutoSize
