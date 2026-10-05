[CmdletBinding()]
param(
    [ValidateSet('Public', 'Beta', 'Both')][string]$Branch = 'Both',
    [ValidateSet('Il2cpp', 'Mono', 'Both')][string]$Runtime = 'Il2cpp',
    [ValidateSet('Client', 'Server', 'Both')][string]$Side = 'Both'
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../DedicatedServerMod.csproj'
$targets = Join-Path $PSScriptRoot '../.github/ci/il2cpp-publicizer.targets'
$branches = if ($Branch -eq 'Both') { @('Public', 'Beta') } else { @($Branch) }
$runtimes = if ($Runtime -eq 'Both') { @('Mono', 'Il2cpp') } else { @($Runtime) }
$sides = if ($Side -eq 'Both') { @('Client', 'Server') } else { @($Side) }
foreach ($gameBranch in $branches) {
    foreach ($modRuntime in $runtimes) {
        foreach ($modSide in $sides) {
            & dotnet build $project -c "$($modRuntime)_$($modSide)" "-p:GameBranch=$gameBranch" "-p:CustomAfterMicrosoftCommonTargets=$targets" -v minimal
            if ($LASTEXITCODE -ne 0) { throw "Build failed: $gameBranch $modRuntime $modSide" }
        }
    }
}
