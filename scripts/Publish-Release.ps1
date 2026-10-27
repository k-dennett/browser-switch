[CmdletBinding()]
param([string]$Repository = 'k-dennett/browser-switch')
. (Join-Path $PSScriptRoot 'Common.ps1')
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Repositorio de GitHub no válido.' }
$repo = Split-Path -Parent $PSScriptRoot
$git = (Get-Command git.exe -ErrorAction Stop).Source
$gitArgs = @('-c',('safe.directory=' + $repo),'-C',$repo)
if (@(& $git @gitArgs status --porcelain).Count -ne 0) { throw 'Publica desde un directorio de trabajo limpio.' }
$origin = (& $git @gitArgs remote get-url origin).Trim()
if ($origin -ne ('https://github.com/' + $Repository + '.git') -and $origin -ne ('https://github.com/' + $Repository) -and $origin -ne ('git@github.com:' + $Repository + '.git')) { throw 'El repositorio de publicación no coincide con origin.' }
$output = Join-Path $repo 'artifacts\releases'
$receipt = Get-Content -LiteralPath (Join-Path $output 'package-validation.json') -Raw | ConvertFrom-Json
$commit = (& $git @gitArgs rev-parse HEAD).Trim()
if (-not $receipt.verified -or $receipt.commit -ne $commit) { throw 'El paquete no está verificado para el commit actual. Ejecuta Package.ps1.' }
$tag = 'v' + $receipt.version
$zipPath = Join-Path $output $receipt.zip
$sumPath = Join-Path $output 'SHA256SUMS.txt'
if ((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $receipt.sha256) { throw 'El ZIP cambió desde su verificación.' }
$expectedSum = $receipt.sha256 + '  ' + $receipt.zip
if ([IO.File]::ReadAllText($sumPath).Trim() -ne $expectedSum) { throw 'SHA256SUMS.txt no corresponde al ZIP.' }
$oldPrompt = $env:GIT_TERMINAL_PROMPT
$oldInteractive = $env:GCM_INTERACTIVE
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'Never'
$headers = $null
try {
    $remoteMain = @(& $git @gitArgs ls-remote origin refs/heads/main)
    if ($LASTEXITCODE -ne 0 -or $remoteMain.Count -ne 1 -or ($remoteMain[0] -split '\s+')[0] -ne $commit) { throw 'Sube primero el commit preparado a origin/main.' }
    $credentialLines = @("protocol=https`nhost=github.com`n`n" | & $git @gitArgs credential fill)
    if ($LASTEXITCODE -ne 0) { throw 'No hay credenciales de Git disponibles para GitHub.' }
    $password = $null
    foreach ($line in $credentialLines) { if ($line.StartsWith('password=')) { $password = $line.Substring(9) } }
    $credentialLines = $null
    if (-not $password) { throw 'El gestor de credenciales no entregó autenticación.' }
    # Credentials stay in memory and are never written to files, process arguments or output.
    $headers = @{Authorization=('Bearer ' + $password);Accept='application/vnd.github+json';'X-GitHub-Api-Version'='2022-11-28'}
    $password = $null
    $base = 'https://api.github.com/repos/' + $Repository
    function Invoke-ReleaseApi {
        param([string]$Method,[string]$Uri,[object]$Data)
        $arguments = @{Method=$Method;Uri=$Uri;Headers=$headers;UserAgent='BrowserSwitch-release';ErrorAction='Stop'}
        if ($null -ne $Data) { $arguments.Body = [Text.Encoding]::UTF8.GetBytes(($Data | ConvertTo-Json -Depth 6)); $arguments.ContentType = 'application/json; charset=utf-8' }
        try { $response = Invoke-RestMethod @arguments; return $response }
        catch { $message = $_.Exception.Message; if ($message.Length -gt 240) { $message = $message.Substring(0,240) }; throw ('GitHub API: ' + $message) }
    }
    $metadata = Invoke-ReleaseApi -Method Get -Uri $base
    if (-not $metadata.permissions.push) { throw 'La cuenta autenticada no tiene permiso para publicar en este repositorio.' }
    $releases = @(Invoke-ReleaseApi -Method Get -Uri ($base + '/releases?per_page=100'))
    $matching = @($releases | Where-Object {$_.tag_name -eq $tag})
    if ($matching.Count -gt 1) { throw 'Hay varios borradores para la misma versión; revísalos antes de publicar.' }
    $release = if ($matching.Count -eq 1) {$matching[0]} else {$null}
    if ($release -and -not $release.draft) { throw ('La release ' + $tag + ' ya está publicada; no se reemplazará.') }
    $localTag = @(& $git @gitArgs tag --list $tag)
    if ($localTag.Count -eq 0) {
        & $git @gitArgs tag -a $tag -m ('BrowserSwitch ' + $tag + ' para Windows') $commit
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear la etiqueta local.' }
    }
    $tagCommit = (& $git @gitArgs rev-parse ($tag + '^{commit}')).Trim()
    if ($tagCommit -ne $commit) { throw 'La etiqueta local apunta a otro commit; no se modificará.' }
    & $git @gitArgs push origin ('refs/tags/' + $tag)
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo publicar la etiqueta; no se usará force.' }
    $body = [IO.File]::ReadAllText((Join-Path $repo ('docs\releases\' + $tag + '.md')))
    if (-not $release) {
        $release = Invoke-ReleaseApi -Method Post -Uri ($base + '/releases') -Data @{tag_name=$tag;target_commitish=$commit;name=('BrowserSwitch ' + $tag + ' · Windows');body=$body;draft=$true;prerelease=$false}
    }
    $uploaded = @()
    foreach ($filePath in @($zipPath,$sumPath)) {
        $name = [IO.Path]::GetFileName($filePath)
        $size = (Get-Item -LiteralPath $filePath).Length
        $hash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $assets = @(Invoke-ReleaseApi -Method Get -Uri ($base + '/releases/' + $release.id + '/assets'))
        $existing = @($assets | Where-Object {$_.name -eq $name})
        if ($existing.Count -gt 0) {
            $asset = $existing[0]
            if ($asset.size -ne $size -or -not ($asset.PSObject.Properties.Name -contains 'digest') -or $asset.digest -ne ('sha256:' + $hash)) { throw ('El borrador ya contiene otro archivo llamado ' + $name + '; se conserva sin sobrescribir.') }
        } else {
            $uploadBase = $release.upload_url -replace '\{.*$',''
            if (([uri]$uploadBase).Host -ne 'uploads.github.com') { throw 'Host de subida de GitHub inesperado.' }
            $contentType = if ($name.EndsWith('.zip')) {'application/zip'} else {'text/plain'}
            $asset = Invoke-RestMethod -Method Post -Uri ($uploadBase + '?name=' + [uri]::EscapeDataString($name)) -Headers $headers -UserAgent 'BrowserSwitch-release' -InFile $filePath -ContentType $contentType -ErrorAction Stop
        }
        if ($asset.state -ne 'uploaded' -or $asset.size -ne $size) { throw ('GitHub no confirmó la subida de ' + $name) }
        if (($asset.PSObject.Properties.Name -contains 'digest') -and $asset.digest -and $asset.digest -ne ('sha256:' + $hash)) { throw ('GitHub devolvió otro hash para ' + $name) }
        $uploaded += $asset
    }
    # Everything is prepared and uploaded before the one final publication step.
    $published = Invoke-ReleaseApi -Method Patch -Uri ($base + '/releases/' + $release.id) -Data @{draft=$false;body=$body;make_latest='true'}
    if ($published.draft) { throw 'GitHub mantuvo la release como borrador.' }
    $zipAsset = @($uploaded | Where-Object {$_.name -eq $receipt.zip})[0]
    $downloadHeaders = $headers.Clone()
    $downloadHeaders.Accept = 'application/octet-stream'
    $downloadPath = Join-Path $output 'download-verification.zip'
    Invoke-WebRequest -Uri ($base + '/releases/assets/' + $zipAsset.id) -Headers $downloadHeaders -UserAgent 'BrowserSwitch-release' -OutFile $downloadPath -UseBasicParsing -ErrorAction Stop | Out-Null
    if ((Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $receipt.sha256) { throw 'La descarga posterior a la publicación no coincide con el paquete comprobado.' }
    $result = [ordered]@{tag=$tag;commit=$commit;releaseUrl=$published.html_url;zipUrl=$zipAsset.browser_download_url;sha256=$receipt.sha256;downloadVerified=$true;privateRepository=$metadata.private}
    [IO.File]::WriteAllText((Join-Path $output 'published-release.json'),($result | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
    [pscustomobject]$result
} finally {
    $headers = $null
    $credentialLines = $null
    $password = $null
    $env:GIT_TERMINAL_PROMPT = $oldPrompt
    $env:GCM_INTERACTIVE = $oldInteractive
}