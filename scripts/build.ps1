param([switch]$Publish)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$dotnet=Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if(-not (Test-Path -LiteralPath $dotnet)){$dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
$env:DOTNET_CLI_HOME=Join-Path $projectRoot '.tools\cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$project=Join-Path $projectRoot 'src\QuietDesk\QuietDesk.csproj'
if($Publish){& $dotnet publish $project -c Release -r win-x64 --self-contained true -o (Join-Path $projectRoot 'artifacts\QuietDesk-v0.5.2-win-x64')}
else{& $dotnet build $project -c Release}
if($LASTEXITCODE -ne 0){throw "Build failed: $LASTEXITCODE"}
