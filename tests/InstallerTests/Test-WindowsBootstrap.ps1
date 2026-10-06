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
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$package/Install-Windows.ps1" -Action update -GameDirectory $game -Offline
    if ($LASTEXITCODE -ne 0) { throw 'Windows automatic beta update failed.' }
    $receipt = Get-Content "$game/.s1ds-installer/state.json" -Raw | ConvertFrom-Json
    if ($receipt.channel -ne 'beta' -or $receipt.source -ne 'fork') { throw 'Automatic update lost the existing beta selection.' }
    foreach ($channel in @('public','beta')) {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$package/Install-Windows.ps1" -Action install -GameDirectory $game -Channel $channel -Offline
        if ($LASTEXITCODE -ne 0) { throw "Windows $channel install failed." }
        if ([IO.File]::ReadAllText("$game/Mods/DedicatedServerMod_Il2cpp_Client.dll") -ne $channel) { throw 'Wrong installed channel.' }
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$package/Install-Windows.ps1" -Action install -GameDirectory $game -Channel public -Source upstream
    if ($LASTEXITCODE -ne 0) { throw 'Windows original upstream install failed.' }
    $receipt = Get-Content "$game/.s1ds-installer/state.json" -Raw | ConvertFrom-Json
    if ($receipt.source -ne 'upstream' -or $receipt.repository -ne 'ifBars/S1DedicatedServers') { throw 'Wrong upstream source in receipt.' }
    # Removing the receipt exercises metadata detection of the real upstream DLL.
    Remove-Item -LiteralPath "$game/.s1ds-installer/state.json"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$package/Install-Windows.ps1" -Action update -GameDirectory $game
    if ($LASTEXITCODE -ne 0) { throw 'Windows automatic detection of a manual upstream install failed.' }
    $receipt = Get-Content "$game/.s1ds-installer/state.json" -Raw | ConvertFrom-Json
    if ($receipt.source -ne 'upstream' -or $receipt.tag -notlike 'v*') { throw 'Automatic update replaced upstream with the fork.' }
    $upstreamHash = (Get-FileHash "$game/Mods/DedicatedServerMod_Il2cpp_Client.dll" -Algorithm SHA256).Hash
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$package/Install-Windows.ps1" -Action install -GameDirectory $game -Channel public -Source upstream -Offline
    if ($LASTEXITCODE -eq 0) { throw 'Upstream offline unexpectedly used fork files.' }
    if ((Get-FileHash "$game/Mods/DedicatedServerMod_Il2cpp_Client.dll" -Algorithm SHA256).Hash -ne $upstreamHash) { throw 'Failed upstream request changed the mod.' }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$package/Install-Windows.ps1" -Action install -GameDirectory $game -Channel public -Source fork -Offline
    if ($LASTEXITCODE -ne 0 -or [IO.File]::ReadAllText("$game/Mods/DedicatedServerMod_Il2cpp_Client.dll") -ne 'public') { throw 'Windows switch back to fork failed.' }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$package/Install-Windows.ps1" -Action uninstall -GameDirectory $game -Offline
    if ($LASTEXITCODE -ne 0 -or (Test-Path "$game/Mods/DedicatedServerMod_Il2cpp_Client.dll")) { throw 'Windows uninstall failed.' }
    if ([IO.File]::ReadAllText("$game/version.dll") -ne 'fixture') { throw 'Pre-existing loader was changed.' }
    Write-Host 'PASS: Windows automatic beta update, manual upstream metadata detection, source switching, rejection without mutation, and uninstall.'
} finally { Remove-Item -LiteralPath $temp -Recurse -Force }
