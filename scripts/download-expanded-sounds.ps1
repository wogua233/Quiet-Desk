$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$destination=Join-Path $projectRoot '.tools\sound-source'
New-Item -ItemType Directory -Force $destination | Out-Null
$manifest=Get-Content (Join-Path $projectRoot 'docs\expanded-source-manifest.json') -Raw | ConvertFrom-Json
foreach($entry in $manifest){
    $path=Join-Path $destination $entry.file
    Invoke-WebRequest $entry.url -OutFile $path
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256){throw "Source checksum changed: $($entry.file)"}
}
