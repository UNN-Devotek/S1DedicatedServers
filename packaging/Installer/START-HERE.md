# Unnamed Schedule I setup

This package installs the Unnamed S1DS fork, including server announcements and the fork's gameplay/runtime fixes. Every player and the server should use matching game and mod builds.

## Windows

1. Close Schedule I. In Steam, select the desired **Public (Betas: None)** or **Beta** game branch and finish its download.
2. Extract the entire ZIP. In Steam, choose **Manage → Browse local files** and copy the folder containing `Schedule I.exe`.
3. Double-click **Install-Windows.cmd**. Choose public or beta, then paste the game folder path. The installer downloads the latest matching release and verifies its checksums.
4. Launch through Steam. Use **Servers → Favorites → Unnamed Schedule I → Join**. Ask the host for the password.

For one-click channel selection, use **Install-Public.cmd** or **Install-Beta.cmd**. The same scripts also update an existing install. Steam must already have the matching game branch; these scripts change the mod files only.

The full **Client ZIP** includes mod files and dependencies for offline installation. The **Setup ZIP** downloads them. In the full ZIP's menu, choose bundled/offline files when prompted, or run:

```powershell
.\Install-Windows.ps1 -Action install -Channel public -GameDirectory 'C:\Games\Schedule I' -Offline
.\Install-Windows.ps1 -Action install -Channel beta -GameDirectory 'C:\Games\Schedule I'
```

Install the [.NET 6 Desktop Runtime for Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) if MelonLoader prompts for it. First launch can take a minute while it prepares the game.

## Linux / Steam Deck

Use the Windows game through Proton. Launch it once, then close it. Install Python 3.10+ and Protontricks; native and Flatpak Protontricks are supported. Extract the ZIP and run:

```bash
bash Install-Linux.sh '/path/to/Schedule I' public
bash Install-Linux.sh '/path/to/Schedule I' beta
# Full ZIP, bundled files:
bash Install-Linux.sh '/path/to/Schedule I' public --offline
```

For a new loader installation the launcher installs the .NET runtime and sets the Proton DLL override. An existing compatible MelonLoader installation is reused.

## Uninstall

Close the game. Double-click **Uninstall-Windows.cmd**, or run:

```powershell
.\Install-Windows.ps1 -Action uninstall -GameDirectory 'C:\Games\Schedule I'
```

```bash
bash Uninstall-Linux.sh --game-directory '/path/to/Schedule I'
```

Uninstall removes files recorded by this installer. It preserves saves, history, unrelated mods, original backups and files you changed afterward. It removes only the unchanged favorite added by the installer. It keeps MelonLoader if other mods/plugins use it. Pre-existing MelonLoader files are left in place.

The original S1DS DLL is saved in `.s1ds-installer/originals` when replaced; normal uninstall removes the active S1DS DLL. Use `-RestorePrevious` on Windows or `--restore-previous` on Linux to restore the previous mod instead. Use `-KeepLoader` / `--keep-loader` to retain installer-owned loader files. Shared .NET/Proton prerequisites are retained.

If you used the old installer, run this updated installer once to create its receipt before using the new uninstaller. The new installer preserves old `S1DS-Client-Backups` folders.

## Releases and troubleshooting

[Public and beta downloads](https://github.com/UNN-Devotek/S1DedicatedServers/releases) use separate tags: `public-vVERSION` and `beta-vVERSION`. Beta releases are prereleases. The installer selects the newest published release in the chosen channel, including beta prereleases; it ignores drafts and other tags.

- Keep Steam running and signed into the account that owns the game.
- A detected Steam build mismatch stops installation. Switch/update the game first.
- An existing compatible MelonLoader is reused to preserve other mods.
- Checksum failures stop installation; download a fresh ZIP instead of disabling validation.
- Interrupted file changes roll back. Installer backups and receipts live in `.s1ds-installer` inside the game folder.
- To install a specific version, use `-ReleaseTag public-v1.1.0-unn.1` on Windows or `--tag public-v1.1.0-unn.1` with `s1ds_installer.py`.
- Mono/client/server selection is available in the command-line engine when those packages exist in the selected release. These initial releases provide IL2CPP client/server builds.
