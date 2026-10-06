[CmdletBinding()]
param(
    [ValidateSet('menu','install','update','uninstall','download','status','check')][string]$Action = 'menu',
    [string]$GameDirectory,
    [ValidateSet('auto','public','beta')][string]$Channel,
    [ValidateSet('auto','fork','upstream')][string]$Source = 'auto',
    [ValidateSet('auto','Il2cpp','Mono')][string]$Runtime = 'auto',
    [ValidateSet('auto','Client','Server')][string]$Side = 'auto',
    [string]$ReleaseTag,
    [switch]$Offline,
    [switch]$KeepLoader,
    [switch]$RestorePrevious,
    [switch]$AllowGameVersionMismatch
)
$ErrorActionPreference = 'Stop'
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $settings = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'installer-settings.json') -Raw | ConvertFrom-Json
    $runtimeFolder = Join-Path $PSScriptRoot '.runtime'
    $python = Join-Path $runtimeFolder 'python.exe'
    if (!(Test-Path -LiteralPath $python)) {
        $bundled = Join-Path $PSScriptRoot ('Dependencies/' + $settings.python.file)
        $archive = $bundled
        if (!(Test-Path -LiteralPath $bundled)) {
            if ($Offline) { throw 'The offline package is missing its Windows runtime. Extract the full Client ZIP.' }
            $cache = Join-Path $PSScriptRoot '.downloads'
            New-Item -ItemType Directory -Path $cache -Force | Out-Null
            $archive = Join-Path $cache $settings.python.file
            if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $settings.python.sha256) {
                $temporary = $archive + '.part'
                try {
                    Write-Host 'Preparing the bundled installer runtime...'
                    Invoke-WebRequest -Uri $settings.python.url -OutFile $temporary -UseBasicParsing
                    if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $settings.python.sha256) { throw 'Windows runtime checksum mismatch.' }
                    Move-Item -LiteralPath $temporary -Destination $archive -Force
                } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
            }
        }
        if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $settings.python.sha256) { throw 'Windows runtime checksum mismatch.' }
        $staging = Join-Path $PSScriptRoot ('.runtime-' + [guid]::NewGuid().ToString('N'))
        try {
            Expand-Archive -LiteralPath $archive -DestinationPath $staging
            if (!(Test-Path -LiteralPath (Join-Path $staging 'python.exe'))) { throw 'Windows runtime does not contain python.exe.' }
            if (Test-Path -LiteralPath $runtimeFolder) { Remove-Item -LiteralPath $runtimeFolder -Recurse -Force }
            Move-Item -LiteralPath $staging -Destination $runtimeFolder
        } finally { if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force } }
    }
    $selectedRuntime = if ($Runtime -ieq 'auto') { 'auto' } elseif ($Runtime -ieq 'Mono') { 'Mono' } else { 'Il2cpp' }
    $selectedSide = if ($Side -ieq 'auto') { 'auto' } elseif ($Side -ieq 'Server') { 'Server' } else { 'Client' }
    $arguments = @('-I', (Join-Path $PSScriptRoot 's1ds_installer.py'), $Action, '--runtime', $selectedRuntime, '--side', $selectedSide, '--source', $Source.ToLowerInvariant())
    if ($GameDirectory) { $arguments += @('--game-directory', $GameDirectory) }
    if ($Channel) { $arguments += @('--channel', $Channel.ToLowerInvariant()) }
    if ($ReleaseTag) { $arguments += @('--tag', $ReleaseTag) }
    if ($Offline) { $arguments += '--offline' }
    if ($KeepLoader) { $arguments += '--keep-loader' }
    if ($RestorePrevious) { $arguments += '--restore-previous' }
    if ($AllowGameVersionMismatch) { $arguments += '--allow-game-version-mismatch' }
    & $python @arguments
    exit $LASTEXITCODE
} catch {
    Write-Host ('S1DS setup failed: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
