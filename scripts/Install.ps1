[CmdletBinding()]
param([switch]$OpenSettings)
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-BrowserSwitchUnpackaged
$null = Get-BrowserSwitchFramework
& (Join-Path $PSScriptRoot 'Build.ps1')
$repo = Split-Path -Parent $PSScriptRoot
Invoke-BrowserSwitchCommand -Executable (Join-Path $repo 'artifacts\windows\BrowserSwitch.exe') -Arguments @('--install')
$exe = Join-Path (Get-BrowserSwitchInstallDirectory) 'BrowserSwitch.exe'
Write-Host ('Instalado, registrado y ejecutándose en la bandeja: ' + $exe)
Write-Host 'Se conservan tus preferencias y un respaldo de la versión anterior. Elige BrowserSwitch en Aplicaciones predeterminadas si aún no está activo.'
if ($OpenSettings) { Invoke-BrowserSwitchCommand -Executable $exe -Arguments @('--defaults') }