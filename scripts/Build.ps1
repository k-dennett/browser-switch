[CmdletBinding()]
param([string]$OutputDirectory)
. (Join-Path $PSScriptRoot 'Common.ps1')
$framework = Get-BrowserSwitchFramework
$repo = Split-Path -Parent $PSScriptRoot
$projectDirectory = Join-Path $repo 'src\BrowserSwitch'
[xml]$project = Get-Content -LiteralPath (Join-Path $projectDirectory 'BrowserSwitch.csproj') -Raw
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts\windows' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$executable = Join-Path $OutputDirectory 'BrowserSwitch.exe'
$compilerArgs = @('/nologo','/noconfig','/nostdlib+','/target:winexe','/platform:anycpu','/optimize+','/warn:4','/warnaserror+','/codepage:65001',('/out:' + $executable),('/win32manifest:' + (Join-Path $projectDirectory 'app.manifest')),('/reference:' + (Join-Path $framework 'mscorlib.dll')))
$iconPath = Join-Path $projectDirectory $project.SelectSingleNode("//*[local-name()='ApplicationIcon']").InnerText
$compilerArgs += '/win32icon:' + $iconPath
foreach ($resource in $project.SelectNodes("//*[local-name()='EmbeddedResource']")) {
    $compilerArgs += '/resource:' + (Join-Path $projectDirectory $resource.Include) + ',' + $resource.LogicalName
}
foreach($reference in $project.SelectNodes("//*[local-name()='Reference']")) { if ($null -ne $reference) { $compilerArgs += '/reference:' + (Join-Path $framework ($reference.Include + '.dll')) } }
foreach($source in $project.SelectNodes("//*[local-name()='Compile']")) { if ($null -ne $source) { $compilerArgs += [IO.Path]::GetFullPath((Join-Path $projectDirectory $source.Include)) } }
& (Join-Path $framework 'csc.exe') @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de BrowserSwitch.' }
Copy-Item -LiteralPath (Join-Path $projectDirectory 'App.config') -Destination ($executable + '.config') -Force
Write-Host ('Compilado: ' + $executable)