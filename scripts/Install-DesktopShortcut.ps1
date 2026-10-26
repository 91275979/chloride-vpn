[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ClientDirectory)
$ErrorActionPreference='Stop'
$client=(Resolve-Path -LiteralPath $ClientDirectory).ProviderPath
$exe=Join-Path $client 'v2rayN.exe'
if(-not(Test-Path -LiteralPath $exe -PathType Leaf)){throw 'Choose the extracted application directory.'}
$desktop=[Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$path=Join-Path $desktop 'Chloride VPN.lnk'
$shell=New-Object -ComObject WScript.Shell
if(Test-Path -LiteralPath $path){
    $existing=$shell.CreateShortcut($path)
    if($existing.TargetPath -ine $exe){throw 'A different shortcut already exists. Rename it before installing this one.'}
}
$shortcut=$shell.CreateShortcut($path)
$shortcut.TargetPath=$exe
$shortcut.Arguments='--show'
$shortcut.WorkingDirectory=$client
$shortcut.IconLocation=$exe+',0'
$shortcut.Description='Open Chloride VPN without changing the connection switch.'
$shortcut.Save()
Write-Output ('Shortcut created: '+$path)
