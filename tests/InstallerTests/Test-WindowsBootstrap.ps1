$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$temp = Join-Path ([IO.Path]::GetTempPath()) ('S1DS Windows bootstrap ' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $package = Join-Path $temp 'Setup'
    Copy-Item -LiteralPath (Join-Path $root 'packaging/Installer') -Destination $package -Recurse
    $game = Join-Path $temp 'Game folder'
    New-Item -ItemType Directory -Path "$game/MelonLoader/net6", "$game/Mods", "$package/Dependencies" -Force | Out-Null
    foreach ($relative in @('Schedule I.exe','GameAssembly.dll','version.dll','MelonLoader/net6/MelonLoader.dll')) { [IO.File]::WriteAllText((Join-Path $game $relative), 'fixture') }
    $settings = Get-Content "$package/installer-settings.json" -Raw | ConvertFrom-Json
    Invoke-WebRequest -Uri $settings.python.url -OutFile "$package/Dependencies/$($settings.python.file)" -UseBasicParsing
    foreach ($channel in @('public','beta')) {
        $folder = Join-Path $package "Packages/$channel"
        $payload = Join-Path $temp "payload-$channel"
        New-Item -ItemType Directory -Path "$payload/Mods", $folder -Force | Out-Null
        $dll = Join-Path $payload 'Mods/DedicatedServerMod_Il2cpp_Client.dll'
        [IO.File]::WriteAllText($dll, $channel)
        $archive = Join-Path $folder 'fixture.zip'
        Compress-Archive -LiteralPath "$payload/Mods" -DestinationPath $archive
        $manifest = @{schema=1;channel=$channel;tag="$channel-v1.1.0-unn.1";version='1.1.0-unn.1';game=@{build_id='fixture'};loader=$settings.loader;packages=@(@{runtime='Il2cpp';side='Client';file='fixture.zip';sha256=(Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant();dll_sha256=(Get-FileHash $dll -Algorithm SHA256).Hash.ToLowerInvariant()})}
        $manifest | ConvertTo-Json -Depth 10 | Set-Content "$folder/release-manifest.json"
    }
    foreach ($channel in @('public','beta','public')) {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$package/Install-Windows.ps1" -Action install -GameDirectory $game -Channel $channel -Offline
        if ($LASTEXITCODE -ne 0) { throw "Windows $channel install failed." }
        if ([IO.File]::ReadAllText("$game/Mods/DedicatedServerMod_Il2cpp_Client.dll") -ne $channel) { throw 'Wrong installed channel.' }
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$package/Install-Windows.ps1" -Action uninstall -GameDirectory $game -Offline
    if ($LASTEXITCODE -ne 0 -or (Test-Path "$game/Mods/DedicatedServerMod_Il2cpp_Client.dll")) { throw 'Windows uninstall failed.' }
    if ([IO.File]::ReadAllText("$game/version.dll") -ne 'fixture') { throw 'Pre-existing loader was changed.' }
    Write-Host 'PASS: native Windows bootstrap, public/beta/public switch, and uninstall.'
} finally { Remove-Item -LiteralPath $temp -Recurse -Force }
