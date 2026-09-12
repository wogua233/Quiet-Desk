$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$exe=Join-Path $root 'artifacts/QuietDesk-v0.3.0-win-x64/QuietDesk.exe'
foreach($count in @(1,4)){
 $out=Join-Path $root "artifacts/v03/performance-$count.json"
 $p=Start-Process -FilePath $exe -ArgumentList @('--acceptance-run','--channels',$count,'--seconds',600,'--out',$out) -WindowStyle Hidden -PassThru
 $p.WaitForExit()
 if($p.ExitCode -ne 0){throw "Acceptance failed: $($p.ExitCode)"}
 if((Get-Content $out -Raw|ConvertFrom-Json).status -ne 'complete'){throw 'Incomplete report'}
}
