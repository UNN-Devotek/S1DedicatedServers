# Fork builds and releases

Public repository: **UNN-Devotek/S1DedicatedServers**. `main` is the protected release branch; `dev` is the integration branch. Changes enter main by pull request with required checks. Game compilation references are read from a separate private repository using a read-only deploy key and are never included in artifacts or installer ZIPs.

## Automatic builds

Pushes to main/dev and pull requests targeting them build Public/Beta × IL2CPP × Client/Server. They also run installer regressions on Linux/Windows, the native Windows bootstrap smoke test, announcement checks, and existing gameplay regression workflows when relevant.

Repository variable `S1DS_ASSEMBLIES_REPO` names the private reference repository. Secret `S1DS_ASSEMBLIES_SSH_KEY` is its read-only deploy key. Reference branches are `public` and `beta`, with `MelonLoader/Il2CppAssemblies` and `MelonLoader/net6`.

`S1DS_BUILD_RUNTIMES` defaults to `["Il2cpp"]`. Set it to `["Il2cpp","Mono"]` after supplying real matching `Managed` and `MelonLoader/net35` directories on both reference branches. Public and beta never share/fall back to the wrong game assemblies. Untrusted fork PRs cannot access the private checkout; maintainers should review them and build the reviewed changes on a trusted branch.

## Tag a release

1. Update `API/Version.cs` and relevant number constants in dev. The fork starts at **1.1.0-unn.1**.
2. Update game versions/build IDs and dependency pins in `packaging/Installer/installer-settings.json` when the underlying game or loader changes. Refresh matching private reference branches.
3. Merge a passing PR into main.
4. Tag the reviewed main commit and push the desired channel(s):

```bash
git tag -a public-v1.1.0-unn.1 main -m 'Public fork 1.1.0-unn.1'
git tag -a beta-v1.1.0-unn.1 main -m 'Beta fork 1.1.0-unn.1'
git push origin public-v1.1.0-unn.1 beta-v1.1.0-unn.1
```

The tag workflow validates the version and main ancestry, builds only matching references, packages DLLs plus installer/checksums, creates a draft release, uploads every asset, and then publishes it. Public becomes Latest; beta is a prerelease and never replaces Latest. Existing releases are not overwritten; increment the version for a new release. For a failed run before publication, inspect/remove the incomplete draft before retrying. Manual workflow dispatch accepts an existing tag and pins its exact source commit.

Each channel includes:

- `S1DS-Public-Il2cpp-Client.zip` / `S1DS-Beta-Il2cpp-Client.zip`
- Corresponding Server ZIPs (Mono ZIPs when enabled)
- `Unnamed-Schedule-I-Setup.zip`: shared online installer/uninstaller scripts
- `Unnamed-Schedule-I-Client.zip`: scripts plus that channel's offline payload, MelonLoader archive, and official portable Windows installer runtime
- `release-manifest.json`: channel/version/commit/game metadata and SHA256 values
- `SHA256SUMS`

No Steam/game binaries, saves, logs, private keys or credentials are packaged. The distributable MelonLoader/portable Python dependencies keep their included licenses.

## Local packaging and one handout for both channels

```powershell
pwsh -NoProfile -File build/Build-Mod.ps1
python build/Package-Release.py --version 1.1.0-unn.1 --channel both --output artifacts/release-1.1.0-unn.1
```

Use a new/empty output directory. Packaging checks the source version and required compiled DLLs, verifies pinned dependencies, includes licenses, and validates each generated ZIP. The top-level `Unnamed-Schedule-I-Client.zip` contains both channels for offline switching. The per-channel subdirectories contain upload-ready release assets. Build from a clean reviewed commit so the manifest commit matches the binary source.

Installer tests:

```bash
python -m unittest discover -s tests/InstallerTests -v
dotnet run --project tests/ServerAnnouncementTests
```

On Windows, run `powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerTests/Test-WindowsBootstrap.ps1` to test the actual Windows bootstrap with isolated fixture folders. The packaging pipeline tests script/file behavior; gameplay and multiplayer UI behavior still require real matching clients/server.
