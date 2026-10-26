[CmdletBinding()]
param()
. (Join-Path $PSScriptRoot 'Common.ps1')
$framework = Get-BrowserSwitchFramework
$repo = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Build.ps1')
$output = Join-Path $repo 'artifacts\windows'
$tests = Join-Path $output 'BrowserSwitch.Tests.exe'
& (Join-Path $framework 'csc.exe') /nologo /noconfig /nostdlib+ /target:exe /platform:anycpu /warn:4 /warnaserror+ /codepage:65001 (('/out:' + $tests)) (('/reference:' + (Join-Path $framework 'mscorlib.dll'))) (('/reference:' + (Join-Path $framework 'System.dll'))) (('/reference:' + (Join-Path $framework 'System.Core.dll'))) (('/reference:' + (Join-Path $framework 'System.Xml.dll'))) (('/reference:' + (Join-Path $framework 'System.Windows.Forms.dll'))) (('/reference:' + (Join-Path $framework 'System.Drawing.dll'))) (('/reference:' + (Join-Path $output 'BrowserSwitch.exe'))) (Join-Path $repo 'tests\BrowserSwitch.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron compilar las pruebas.' }
& $tests
if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas.' }