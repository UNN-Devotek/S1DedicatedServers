# Build Setup Guide

This guide helps contributors set up their local development environment for the DedicatedServerMod project.

## Prerequisites

- .NET SDK with support for both `netstandard2.1` and `net6.0`
- Bun
- Visual Studio, Rider, or VS Code with C# support
- Schedule I game installed

## First-Time Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd DedicatedServerMod
```

### 2. Configure Local Build Paths

The project uses a `local.build.props` file for user-specific paths. This file is git-ignored so each contributor can have their own configuration.

1. Copy the example template:
   ```bash
   copy local.build.props.example local.build.props
   ```

2. Edit `local.build.props` and update the paths to match your local environment:
   ```xml
   <PropertyGroup>
       <PublicIl2CppGamePath>YOUR_PUBLIC_GAME_PATH</PublicIl2CppGamePath>
       <BetaIl2CppGamePath>YOUR_BETA_GAME_PATH</BetaIl2CppGamePath>
       <!-- Add PublicMonoGamePath/BetaMonoGamePath if building Mono too. -->
   </PropertyGroup>
   ```

   **Example paths:**
   - `C:\Program Files (x86)\Steam\steamapps\common\Schedule I`
   - `D:\Games\Schedule I`

### 3. Restore NuGet Packages

The project uses Krafs.Publicizer to automatically publicize the Assembly-CSharp.dll at build time, eliminating the need for manually creating a publicized DLL.

```bash
dotnet restore
```

### 4. Install Web Panel Dependencies

The embedded web panel lives in `webpanel` and uses Bun exclusively.

```bash
cd webpanel
bun install
cd ..
```

## Public and beta builds

Copy `local.build.props.example` to `local.build.props` and configure separate
public/beta game roots. Launch each IL2CPP installation with MelonLoader once to
generate that installation's `MelonLoader/Il2CppAssemblies`. The beta must use
its own generated assemblies and matching MelonLoader references; public
references are never a fallback. Do not put proprietary game DLLs in this repo.

The same sources support both client and server for each game branch:

| Runtime | Public client / server | Beta client / server |
| --- | --- | --- |
| IL2CPP | `Il2cpp_Client`, `Il2cpp_Server` | `Il2cpp_Client_Beta`, `Il2cpp_Server_Beta` |
| Mono | `Mono_Client`, `Mono_Server` | `Mono_Client_Beta`, `Mono_Server_Beta` |

Mono targets `netstandard2.1`; IL2CPP targets `net6.0`. `GameBranch=Public` is
also the default when using the original configuration names; explicitly
passing `-p:GameBranch=Beta` selects beta with those names.

```sh
# Four IL2CPP artifacts: public client/server and beta client/server.
pwsh -File build/Build-Mod.ps1
# Eight artifacts, when matching Mono references are configured too.
pwsh -File build/Build-Mod.ps1 -Runtime Both
# Or build a single artifact directly (no PowerShell required).
dotnet build DedicatedServerMod.csproj -c Il2cpp_Client_Beta -p:CustomAfterMicrosoftCommonTargets=.github/ci/il2cpp-publicizer.targets
```

Outputs are under `bin/Public/<runtime>_<side>/<framework>/` and
`bin/Beta/<runtime>_<side>/<framework>/`. Intermediate reference caches are
isolated under `obj/Public/` and `obj/Beta/`. Assembly informational version
and `GameBranch` metadata identify the branch. DLL filenames stay compatible
with MelonLoader and the existing mod policy, so keep each branch in its own
package. Install only the matching server DLL on the host and matching client
DLL on **every** client; changing the server alone is insufficient.

`AutomateLocalDeployment` defaults to `false`. Copy artifacts deliberately or
opt in to deployment in `local.build.props`. Beta uses its own Mods directory
or `BetaClientDeploymentPath`/`BetaServerDeploymentPath` overrides. Assembly-only
build inputs require an explicit deployment path if deployment is enabled.

The Build workflow creates an eight-job Public/Beta × Mono/IL2CPP × Client/Server
matrix with named branch artifacts. Configure `GAME_ASSEMBLIES_REPO` and
`GAME_ASSEMBLIES_TOKEN` for Mono, and `IL2CPP_ASSEMBLIES_REPO` and
`IL2CPP_ASSEMBLIES_TOKEN` for IL2CPP. Assembly repositories must have matching
`main` (Public) and `beta` branches. Mono expects `Managed/` and `MelonLoader/`;
IL2CPP supports `MelonLoader/Il2CppAssemblies` + `MelonLoader/net6` or those two
directories at the repository root. Full builds require those private inputs;
the regression workflow uses managed doubles and needs no game DLLs. The
release and documentation workflows use Public assemblies; beta artifacts are
created through the Build workflow or locally, without publishing a stable release.

Compatibility verified here is **Public 0.4.6f13 / beta 0.4.7f7** (IL2CPP).
Beta API mappings cover player identity/data loading and visibility, sleep,
time, messaging, movement, avatar culling and inherited police responses.
Later beta updates can change APIs again and need matching references and
another runtime smoke test. Full Mono builds and multiplayer beta gameplay
require additional verification; managed regression tests are not substitutes.

### Building the embedded web panel

```bash
cd webpanel
bun run typecheck
bun run build
```

`bun run build` writes the static frontend bundle into `Server/WebPanel/Static`, which is what the dedicated server serves at runtime.

## How It Works

### Assembly Publicization

Previously, contributors needed to manually create `Assembly-CSharp-publicized.dll` using a separate tool. Now:

1. **Krafs.Publicizer** NuGet package is included in the project
2. During build, it automatically publicizes `Assembly-CSharp.dll`
3. References to Assembly-CSharp have `Publicize="true"` metadata
4. No manual steps required!

### Path Management

- **local.build.props**: Your personal game installation paths (git-ignored)
- **local.build.props.example**: Template with example paths (committed to git)
- **.csproj**: References `$(MonoGamePath)` and `$(Il2CppGamePath)` from your local.build.props
- **webpanel/**: Bun-managed React workspace for the embedded localhost panel

## Troubleshooting

### Build fails with "Could not find Assembly-CSharp.dll"

**Solution**: Make sure your `local.build.props` paths point to the correct game installation directory.

### Build fails with missing NuGet packages

**Solution**: Run `dotnet restore` to download all required packages.

### Changes to local.build.props aren't detected

**Solution**: Clean and rebuild the project:
```bash
dotnet clean
dotnet build -c <configuration>
```

## Contributing

When contributing:

1. **Never commit** your `local.build.props` file (it's git-ignored)
2. **Do commit** changes to `local.build.props.example` if you add new path properties
3. Test your changes with both Mono and IL2CPP configurations when possible
4. Use Bun for the frontend workspace; do not use npm or pnpm

## Additional Resources

- [Krafs.Publicizer Documentation](https://github.com/krafs/Publicizer)
- [MSBuild Property Reference](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties)
