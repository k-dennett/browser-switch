[CmdletBinding()]
param([string]$Version)
. (Join-Path $PSScriptRoot 'Common.ps1')
$null = Get-BrowserSwitchFramework
$repo = Split-Path -Parent $PSScriptRoot
$git = (Get-Command git.exe -ErrorAction Stop).Source
$gitArgs = @('-c',('safe.directory=' + $repo),'-C',$repo)
$dirty = @(& $git @gitArgs status --porcelain)
if ($LASTEXITCODE -ne 0 -or $dirty.Count -gt 0) { throw 'Empaqueta desde un commit limpio para identificar exactamente el código de la release.' }
$commit = (& $git @gitArgs rev-parse HEAD).Trim()
$assemblyInfo = [IO.File]::ReadAllText((Join-Path $repo 'src\BrowserSwitch\Properties\AssemblyInfo.cs'))
$versionMatch = [regex]::Match($assemblyInfo,'AssemblyFileVersion\("(?<version>\d+\.\d+\.\d+\.\d+)"\)')
if (-not $versionMatch.Success) { throw 'No se pudo leer AssemblyFileVersion.' }
$fileVersion = [version]$versionMatch.Groups['version'].Value
if (-not $Version) { $Version = $fileVersion.ToString(3) }
if ($Version -notmatch '^\d+\.\d+\.\d+$' -or $Version -ne $fileVersion.ToString(3)) { throw 'La versión del ZIP debe coincidir con AssemblyFileVersion (tres componentes).' }
$notes = Join-Path $repo ('docs\releases\v' + $Version + '.md')
if (-not (Test-Path -LiteralPath $notes)) { throw 'Faltan las notas de la release.' }
& (Join-Path $PSScriptRoot 'Test.ps1')
$artifacts = Join-Path $repo 'artifacts'
$output = Join-Path $artifacts 'releases'
[IO.Directory]::CreateDirectory($output) | Out-Null
$name = 'BrowserSwitch-' + $Version + '-windows'
$work = Join-Path $artifacts ('package-' + [guid]::NewGuid().ToString('N'))
$payload = Join-Path $work $name
[IO.Directory]::CreateDirectory((Join-Path $payload 'scripts')) | Out-Null
$files = @(
    @{Source='artifacts\windows\BrowserSwitch.exe';Target='BrowserSwitch.exe'},
    @{Source='artifacts\windows\BrowserSwitch.exe.config';Target='BrowserSwitch.exe.config'},
    @{Source='src\BrowserSwitch\Assets\BrowserSwitch.ico';Target='BrowserSwitch.ico'},
    @{Source='packaging\LEEME.txt';Target='LEEME.txt'},
    @{Source='scripts\Common.ps1';Target='scripts\Common.ps1'},
    @{Source='scripts\Uninstall.ps1';Target='scripts\Uninstall.ps1'}
)
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $repo $file.Source) -Destination (Join-Path $payload $file.Target) }
Copy-Item -LiteralPath $notes -Destination (Join-Path $payload 'NOTAS.md')
[IO.File]::WriteAllText((Join-Path $payload 'VERSION.txt'),("BrowserSwitch $Version`r`nCommit: $commit`r`nWindows / .NET Framework 4.8`r`n"),[Text.UTF8Encoding]::new($false))
$manifestFiles = @(Get-ChildItem -LiteralPath $payload -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{path=$_.FullName.Substring($payload.Length+1).Replace('\','/');size=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
$manifest = [ordered]@{product='BrowserSwitch';version=$Version;commit=$commit;platform='windows';framework='4.8';files=$manifestFiles}
$manifestJson = $manifest | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText((Join-Path $payload 'manifest.json'),$manifestJson,[Text.UTF8Encoding]::new($false))
$zip = Join-Path $output ($name + '.zip')
Compress-Archive -LiteralPath $payload -DestinationPath $zip -Force
$check = Join-Path $work 'validacion con espacios'
Expand-Archive -LiteralPath $zip -DestinationPath $check
$extracted = Join-Path $check $name
$expected = @($manifestFiles | ForEach-Object {$_.path}) + @('manifest.json')
$actual = @(Get-ChildItem -LiteralPath $extracted -File -Recurse | ForEach-Object {$_.FullName.Substring($extracted.Length+1).Replace('\','/')})
if (@(Compare-Object ($expected | Sort-Object) ($actual | Sort-Object)).Count -ne 0) { throw 'El ZIP no contiene exactamente los archivos esperados.' }
foreach ($file in $manifestFiles) {
    if ((Get-FileHash -LiteralPath (Join-Path $extracted $file.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) { throw ('Hash incorrecto tras extraer: ' + $file.path) }
}
$exe = Join-Path $extracted 'BrowserSwitch.exe'
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion -ne $fileVersion.ToString()) { throw 'Versión de ejecutable inesperada.' }
$probe = Join-Path $work 'package-check.xml'
Invoke-BrowserSwitchCommand -Executable $exe -Arguments @('--diagnostics',$probe)
[xml]$report = Get-Content -LiteralPath $probe -Raw
if ($report.DiagnosticReport.ExecutablePath -ne $exe) { throw 'No se ejecutó el binario extraído del paquete.' }
$sum = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'),($sum + '  ' + [IO.Path]::GetFileName($zip) + "`n"),[Text.Encoding]::ASCII)
$receipt = [ordered]@{version=$Version;commit=$commit;zip=[IO.Path]::GetFileName($zip);sha256=$sum;size=(Get-Item -LiteralPath $zip).Length;extractedExecutable=$exe;diagnostic=$probe;verified=$true}
[IO.File]::WriteAllText((Join-Path $output 'package-validation.json'),($receipt | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Write-Host ('Paquete comprobado: ' + $zip)
Write-Host ('SHA-256: ' + $sum)
Write-Host ('Ejecutable extraído comprobado sin depender del código fuente: ' + $exe)