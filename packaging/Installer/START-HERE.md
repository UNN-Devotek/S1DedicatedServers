# Unnamed Schedule I setup

This package installs the Unnamed S1DS fork, including server announcements and the fork's gameplay/runtime fixes. Every player and the server should use matching game and mod builds.

## Windows

1. Close Schedule I. In Steam, select the desired **Public (Betas: None)** or **Beta** game branch and finish its download.
2. Extract the entire ZIP. In Steam, choose **Manage → Browse local files** and copy the folder containing `Schedule I.exe`.
3. Double-click **Update-Windows.cmd**, or use **Install-Windows.cmd** and press Enter for **Detect and update automatically**. Paste the game folder path. The installer detects an existing mod's source, channel, runtime and client/server side, then downloads the latest matching release and verifies its checksums. For a first install it selects our fork and detects the game branch/runtime.
4. Launch through Steam. Use **Servers → Favorites → Unnamed Schedule I → Join**. Ask the host for the password.

For one-click channel selection, use **Install-Public.cmd** or **Install-Beta.cmd**. The same scripts also update an existing install. Steam must already have the matching game branch; these scripts change the mod files only.

Automatic updates use a matching install receipt or read the actual DLL's MelonInfo and assembly metadata without loading it. Manual ifBars installs are detected as upstream; older fork public/beta installs are detected too. A changed DLL takes priority over a stale receipt. Multiple S1DS runtime/side DLLs or an unidentified custom build stop automatic selection; remove duplicates or choose source/channel explicitly. Backups and logs are not used as the active mod.

Automatic updates keep the installed source/channel/runtime/side. They skip unchanged current files and do not replace a newer local build with an older online release. An explicit release tag or bundled/offline selection permits a deliberate downgrade. To switch Steam branches or distribution sources, use the explicit public/beta/upstream choices after changing Steam. Missing recorded mod files are repaired using their previous selection.

```powershell
.\Install-Windows.ps1 -Action update -GameDirectory 'C:\Games\Schedule I'
```

The full **Client ZIP** includes this release's channel and dependencies for offline installation. **Unnamed-Schedule-I-Public-and-Beta.zip** contains both channels and is the shareable handout; it is built and attached to every tagged release. The **Setup ZIP** downloads the files.

Every installer uses the **latest published public or beta release** when downloading online, even if its ZIP originally contained an older version. Drafts and the other channel are ignored. It uses the ZIP's bundled version only when you choose bundled/offline files or pass `-Offline` / `--offline`. To get fresh installer scripts, download the Setup ZIP from the newest release.

### Original ifBars releases

The menu also offers **5: Switch/install Original ifBars Public**. This downloads the latest stable release directly from [ifBars/S1DedicatedServers](https://github.com/ifBars/S1DedicatedServers/releases), verifies GitHub's SHA256 archive digest, and installs the selected client DLL using the same backups and receipt. Choose the source matching your server: original upstream files replace the fork mod and do not include our fork's fixes. Explicit menu options 1 and 2 select our fork; automatic option 0 preserves the detected source.

Upstream selection is online and public only. Beta and bundled/offline files use our fork. Upstream does not publish an exact Steam build ID; its installs verify the selected Mono/IL2CPP runtime, while our fork installs also check the Steam build ID. Switch Steam to the public branch before using upstream. The installer patches the mod/loader files in your installed game; Steam supplies game updates.

```powershell
.\Install-Windows.ps1 -Action install -Source upstream -Channel public -GameDirectory 'C:\Games\Schedule I'
# Download a Mono client instead:
.\Download-Client-Files.ps1 -Source upstream -Runtime Mono
```

```bash
bash Install-Linux.sh '/path/to/Schedule I' public --source upstream
python3 s1ds_installer.py download --source upstream --channel public --runtime Mono
```

Run the normal public/beta installer again to switch back to our fork. `-ReleaseTag v1.1.0` / `--tag v1.1.0` pins an upstream release when the source is upstream. Older upstream releases without a GitHub SHA256 digest are rejected. Status records which repository, source, tag, runtime and side are installed. Verified downloads are cached in `.downloads` alongside the installer; upstream archives are under `.downloads/upstream/TAG`.

In a full ZIP's menu, choose bundled/offline files when prompted, or run:

```powershell
.\Install-Windows.ps1 -Action install -Channel public -GameDirectory 'C:\Games\Schedule I' -Offline
.\Install-Windows.ps1 -Action install -Channel beta -GameDirectory 'C:\Games\Schedule I'
```

Install the [.NET 6 Desktop Runtime for Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) if MelonLoader prompts for it. First launch can take a minute while it prepares the game.

## Linux / Steam Deck

Use the Windows game through Proton. In Steam, select the matching public/beta game branch, launch it once, then close it. Use **Desktop Mode** on Steam Deck. Install Python 3.10+ and [Protontricks](https://github.com/Matoking/protontricks); on Steam Deck, install Protontricks from **Discover**. Native and Flatpak Protontricks are supported.

Extract the entire ZIP, open a terminal in the extracted folder, and run:

```bash
bash Install-Linux.sh
```

Press Enter for automatic detection/update, or choose an explicit public/beta/upstream switch, uninstall, or status. Paste the game folder from Steam's **Manage → Browse local files**. To update without source/channel questions:

```bash
bash Update-Linux.sh '/path/to/Schedule I'
# Equivalent:
bash Install-Linux.sh '/path/to/Schedule I' auto
```

In the full Client ZIP, choose **Use bundled files** after an explicit public/beta choice to install its included mod files. To select a channel directly:

```bash
bash Install-Linux.sh '/path/to/Schedule I' public --source fork
bash Install-Linux.sh '/path/to/Schedule I' beta --source fork
# Full ZIP, bundled files:
bash Install-Linux.sh '/path/to/Schedule I' public --offline
```

The launcher verifies the selected downloads and game before preparing Proton. It ensures the game's .NET 6 Desktop Runtime and MelonLoader DLL override are configured even when loader files already exist, following [MelonLoader's Linux instructions](https://github.com/LavaGang/MelonWiki/blob/master/docs/gettingstarted.md). An existing compatible MelonLoader installation is reused. Flatpak Protontricks receives access to the selected Steam library for that invocation, including external drives/SD cards; permanent Flatpak permissions are not changed.

The bundled/offline option supplies mod and MelonLoader files. A **first-time .NET installation through Protontricks still needs internet**; prepare the game's Proton runtime while online before installing offline. Runtime and DLL override setup failures stop before any mod files are installed. Launch the game normally through Steam afterward.

To download the latest beta files without installing them:

```bash
python3 s1ds_installer.py download --channel beta
```

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
- To install a specific version, use `-ReleaseTag public-v1.1.0-unn.4` on Windows or `--tag public-v1.1.0-unn.4` on Linux. Explicit version selection overrides the default latest-release lookup.
- Mono/client/server selection is available in the command-line engine when those packages exist in the selected release. These initial releases provide IL2CPP client/server builds.
