param([int]$RunnerId=0)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$runner=if($RunnerId -gt 0){Get-Process -Id $RunnerId -ErrorAction SilentlyContinue}else{$null}
if($runner){$runner.WaitForExit()}
foreach($count in @(1,4)){if((Get-Content "$root/artifacts/v03/performance-$count.json" -Raw|ConvertFrom-Json).status -ne 'complete'){throw 'Core acceptance incomplete'}}
$exe="$root/artifacts/QuietDesk-v0.3.0-win-x64/QuietDesk.exe"
$p=Start-Process -FilePath $exe -ArgumentList @('--acceptance-run','--acceptance-radio','--channels',1,'--seconds',30,'--out',"$root/artifacts/v03/hls-performance.json") -WindowStyle Hidden -PassThru
$p.WaitForExit()
if($p.ExitCode -ne 0){throw 'HLS sample failed'}
& D:/miniconda3/python.exe "$root/scripts/summarize-v03.py"
if($LASTEXITCODE -ne 0){throw 'Report generation failed'}
& "$root/scripts/package.ps1"
