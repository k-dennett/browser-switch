[CmdletBinding()]
param([switch]$OpenSettings)
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-BrowserSwitchUnpackaged
$null = Get-BrowserSwitchFramework
& (Join-Path $PSScriptRoot 'Build.ps1')
$repo = Split-Path -Parent $PSScriptRoot
Invoke-BrowserSwitchCommand -Executable (Join-Path $repo 'artifacts\windows\BrowserSwitch.exe') -Arguments @('--install')
$exe = Join-Path (Get-BrowserSwitchInstallDirectory) 'BrowserSwitch.exe'
Write-Host ('Instalado y registrado para este usuario: ' + $exe)
Write-Host 'La copia anterior sigue intacta. Elige BrowserSwitch en Aplicaciones predeterminadas si aún no está activo.'
if ($OpenSettings) { Invoke-BrowserSwitchCommand -Executable $exe -Arguments @('--defaults') }