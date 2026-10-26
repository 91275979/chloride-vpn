[CmdletBinding()]
param([string]$DotnetPath='dotnet')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
foreach($name in @('AppRoutingChecks','InstanceChecks')){
    $project=Join-Path $root ('tests\'+$name+'\'+$name+'.csproj')
    & $DotnetPath build $project -c Release -p:UseSharedCompilation=false -m:1 -nr:false -v:minimal
    if($LASTEXITCODE -ne 0){throw ('Test build failed: '+$name)}
    $dll=Join-Path $root ('tests\'+$name+'\bin\Release\net10.0\'+$name+'.dll')
    & $DotnetPath $dll (Join-Path $root 'artifacts\test-configs')
    if($LASTEXITCODE -ne 0){throw ('Test execution failed: '+$name)}
}
Write-Output 'Offline checks completed. No node credentials or live network probes were used.'
