param([ValidateSet('public','beta')][string]$Channel = 'public', [string]$ReleaseTag, [switch]$Offline)
& (Join-Path $PSScriptRoot 'Install-Windows.ps1') -Action download -Channel $Channel -ReleaseTag $ReleaseTag -Offline:$Offline
