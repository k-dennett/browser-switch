Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Get-BrowserSwitchFramework {
    if ($env:OS -ne 'Windows_NT') { throw 'Ejecuta este script con PowerShell de Windows, incluso si el código está en WSL.' }
    $release = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction Stop).Release
    if ($release -lt 528040) { throw 'Se requiere .NET Framework 4.8 ya disponible en Windows. No se instalará nada automáticamente.' }
    $folder = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
    if (-not (Test-Path -LiteralPath (Join-Path $folder 'csc.exe'))) { throw 'No se encontró el compilador de .NET Framework incluido en Windows.' }
    return $folder
}
function Get-BrowserSwitchInstallDirectory {
    $path = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'BrowserSwitch'))
    if ($path -notmatch '^[A-Za-z]:\\' -or $path.StartsWith('\\')) { throw 'LOCALAPPDATA debe estar en una unidad local de Windows, fuera de WSL y de recursos de red.' }
    return $path
}
function Invoke-BrowserSwitchCommand {
    param([Parameter(Mandatory=$true)][string]$Executable, [Parameter(Mandatory=$true)][string[]]$Arguments)
    # These administrative app commands accept only flags and filesystem paths, never URLs.
    $quoted = foreach($argument in $Arguments) {
        if ($argument.Contains('"') -or $argument.EndsWith('\')) { throw 'Argumento de preparación no válido.' }
        '"' + $argument + '"'
    }
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $Executable
    $start.Arguments = $quoted -join ' '
    $start.WorkingDirectory = Split-Path -Parent $Executable
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    try {
        $errorText = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(20000)) { $process.Kill(); throw 'BrowserSwitch no terminó la comprobación en 20 segundos.' }
        if ($process.ExitCode -ne 0) { throw ('BrowserSwitch devolvió un error: ' + $errorText.Result) }
    } finally { $process.Dispose() }
}
function Assert-BrowserSwitchUnpackaged {
    if (-not ('BrowserSwitchSetup.PackageContext' -as [type])) {
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
namespace BrowserSwitchSetup {
    public static class PackageContext {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetCurrentPackageFullName(ref uint length, StringBuilder name);
    }
}
"@
    }
    [uint32]$length = 0
    $result = [BrowserSwitchSetup.PackageContext]::GetCurrentPackageFullName([ref]$length, $null)
    if ($result -ne 15700) { throw 'Esta consola pertenece a una aplicación empaquetada y puede virtualizar LOCALAPPDATA. Ejecuta Install.ps1 desde PowerShell de Windows abierto normalmente desde Inicio.' }
}