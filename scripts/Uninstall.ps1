[CmdletBinding()]
param([switch]$RemovePersonalData)
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-BrowserSwitchUnpackaged
$install = Get-BrowserSwitchInstallDirectory
$exe = Join-Path $install 'BrowserSwitch.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'No se encontró la copia estable de BrowserSwitch.' }
foreach($process in @(Get-Process -Name 'BrowserSwitch' -ErrorAction SilentlyContinue)) { if ($process.Path -eq $exe) { throw 'Cierra BrowserSwitch antes de desinstalar.' } }
# Refuses while any supported association still uses BrowserSwitch; never edits UserChoice.
Invoke-BrowserSwitchCommand -Executable $exe -Arguments @('--unregister')
$ws = New-Object -ComObject WScript.Shell
$repo = Split-Path -Parent $PSScriptRoot
foreach($path in @((Join-Path ([Environment]::GetFolderPath('Desktop')) 'BrowserSwitch.lnk'),(Join-Path ([Environment]::GetFolderPath('Programs')) 'BrowserSwitch.lnk'),(Join-Path ([Environment]::GetFolderPath('Desktop')) 'Cambiar navegador.lnk'),(Join-Path $repo 'Cambiar navegador.lnk'))) {
    if ((Test-Path -LiteralPath $path) -and $ws.CreateShortcut($path).TargetPath -eq $exe) { Remove-Item -LiteralPath $path }
}
foreach($name in @('BrowserSwitch.ico','BrowserSwitch.exe','BrowserSwitch.exe.config','BrowserSwitch.exe.previous','BrowserSwitch.exe.config.previous','BrowserSwitch.new.exe','BrowserSwitch.new.exe.config')) {
    $path = Join-Path $install $name
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
}
if ($RemovePersonalData) {
    $data = [IO.Path]::GetFullPath((Join-Path $install 'Data'))
    if ($data -ne ($install.TrimEnd('\') + '\Data')) { throw 'Ruta de datos inesperada; no se eliminará.' }
    if (Test-Path -LiteralPath $data) { Remove-Item -LiteralPath $data -Recurse -Force }
}
Write-Host 'BrowserSwitch desinstalado. Los datos personales se conservan salvo que uses -RemovePersonalData.'