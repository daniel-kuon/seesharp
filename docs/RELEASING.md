# Releasing SeeSharp.Cli to nuget.org

The tool is the NuGet package **`SeeSharp.Cli`** (command `seesharp`), published by
`.github/workflows/release.yml` with **trusted publishing**: the workflow gets a short-lived
(1 hour) API key from nuget.org through GitHub's OIDC token — no API key is stored anywhere.
Docs: https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing

## One-time setup

1. **nuget.org policy.** Sign in at https://www.nuget.org → your user name (top right) →
   **Trusted Publishing** → *Add policy*:
   - Repository Owner: `wollume-studios`
   - Repository: `seesharp`
   - Workflow File: `release.yml` (file name only, no path)
   - Environment: leave empty
   - Policy name: e.g. `seesharp release`; Package Owner: your account. A policy owned by you
     covers packages you push, including the first push that creates `SeeSharp.Cli`.
2. **GitHub secret** with your nuget.org profile name (not your e-mail):
   ```
   gh secret set NUGET_USER --repo wollume-studios/seesharp
   ```
   (paste the profile name when asked), or *Settings → Secrets and variables → Actions → New
   repository secret*.

## Each release

The version comes from the tag (`v0.1.0` → package `0.1.0`); the `<Version>` in
`src/SeeSharp.Cli/SeeSharp.Cli.csproj` is only the local default.

```
git tag v0.1.0
git push origin v0.1.0
```

The workflow then runs the tests, packs with `-p:Version=0.1.0`, attaches the `.nupkg` to a
GitHub release `v0.1.0` and pushes it to nuget.org (`--skip-duplicate`, so re-running is
harmless). *Actions → Release → Run workflow* re-runs it for an existing tag. nuget.org needs a
few minutes to validate and index a new version before `dotnet tool install -g SeeSharp.Cli`
finds it.

A tag on a commit of `dev` publishes that commit; usually tag `main` after merging.

## Local package

```
dotnet pack src/SeeSharp.Cli -c Release -o nupkg
dotnet tool install --tool-path <scratch folder> --add-source nupkg SeeSharp.Cli
```

`nupkg/` is git-ignored. NuGet caches a package by id and version in
`~/.nuget/packages/seesharp.cli/<version>`: after repacking the *same* version, delete that folder
(or pack with `-p:Version=0.1.1-dev1`) or an install/update keeps the old build. The IDE plugins'
setting *extra package source* takes such a folder (`--add-source`) so *Set up SeeSharp* works
before a nuget.org release.
