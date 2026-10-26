[CmdletBinding()]
param([string]$DotnetPath='dotnet')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root 'artifacts\win-x64'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
foreach($project in @('v2rayN\v2rayN\v2rayN.csproj','v2rayN\AmazTool\AmazTool.csproj')){
    & $DotnetPath publish (Join-Path $root $project) -c Release -r win-x64 --self-contained true -o $output -p:UseSharedCompilation=false -m:1 -nr:false -v:minimal
    if($LASTEXITCODE -ne 0){throw 'Build failed; do not distribute partial output.'}
}
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $output 'COPYING') -Force
Copy-Item -LiteralPath (Join-Path $root 'NOTICE.md') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-ChlorideVPN.cmd') -Destination $output -Force
foreach($forbidden in @('guiConfigs','guiLogs','binConfigs')){
    if(Test-Path -LiteralPath (Join-Path $output $forbidden)){throw 'Output contains runtime state; use a fresh output directory.'}
}
[ordered]@{product='Chloride VPN';version='0.1.0-alpha.1';upstream='2dust/v2rayN';upstreamVersion='7.25.6';upstreamCommit='1883178b5f1e222b6c6db2782e7a3d989b7cc18f';applicationSha256=(Get-FileHash -LiteralPath (Join-Path $output 'v2rayN.exe')).Hash;includesCores=$false}|
    ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'BUILD-INFO.json') -Encoding UTF8
Write-Output ('Application built: '+$output)
Write-Output 'Application-only build: runtime cores and data are not bundled or downloaded by this script.'
