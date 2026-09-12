$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$target=Join-Path $projectRoot '.tools\sound-source'
New-Item -ItemType Directory -Force $target | Out-Null
$base='https://raw.githubusercontent.com/rafaelmardojai/blanket/9d229d2be7cb6619135d55ff9e49926e40298686/data/resources/sounds'
$manifest=Get-Content (Join-Path $projectRoot 'docs\sound-validation.json') -Raw | ConvertFrom-Json
foreach($entry in $manifest){
    $destination=Join-Path $target ($entry.sound+'.ogg')
    Invoke-WebRequest ($base+'/'+$entry.sound+'.ogg') -OutFile $destination
    if((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $entry.source_sha256){throw "Source checksum mismatch: $($entry.sound)"}
}
