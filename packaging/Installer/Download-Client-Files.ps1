param([ValidateSet('public','beta')][string]$Channel = 'public', [ValidateSet('fork','upstream')][string]$Source = 'fork', [ValidateSet('Il2cpp','Mono')][string]$Runtime = 'Il2cpp', [string]$ReleaseTag, [switch]$Offline)
& (Join-Path $PSScriptRoot 'Install-Windows.ps1') -Action download -Channel $Channel -Source $Source -Runtime $Runtime -ReleaseTag $ReleaseTag -Offline:$Offline
