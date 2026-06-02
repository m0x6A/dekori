---
name: verify-and-release
description: Run Dekori's definition-of-done verification (build + test + demo telemetry signals) and/or drive the MinVer tag-driven NuGet release. Use when asked to verify a change end-to-end, confirm the demo shows expected spans/metrics/logs, cut a release, or publish to NuGet.
---

# Verify & release Dekori

Two modes. **Verify** is the everyday definition-of-done gate. **Release** cuts a tagged version
that CI publishes to NuGet. Pick the mode from the request; default to **verify** when ambiguous.

---

## Mode: verify (definition of done)

Run the project's definition of done from `CLAUDE.md`:

1. **Build clean — 0 warnings** (CI runs `-warnaserror`, so any warning fails the build):
   ```bash
   dotnet build -warnaserror
   ```
   If warnings appear, treat the build as failed and report them.

2. **Tests fully green:**
   ```bash
   dotnet test
   ```
   Report failures with their output — do not claim success on a red suite.

3. **Demo shows the expected signals** (for behavioural changes):
   ```bash
   dotnet run --project samples/Dekori.Demo
   ```
   The `DemoDashboard` prints three ASCII tables — **SPANS** (Operation/Duration/Status/Exception),
   **METRICS** (Instrument/Operation/Value), **LOGS** (Category/Level/Message). Confirm the change's
   spans/metrics/logs appear with the expected names, statuses and tags. If the new behavior isn't
   exercised by the demo, add a call in `samples/Dekori.Demo/Program.cs` so it is observable.
   (The built-in `run` / `verify` skills can drive the app; this skill adds Dekori's signal checks.)

Report a concise pass/fail per step. This mirrors what `.github/workflows/ci.yml` enforces, so a
green local verify predicts a green CI run.

---

## Mode: release (MinVer tag → NuGet)

Versioning is **MinVer-driven**: the package version comes from the git **tag**, not the `.csproj`.
Publishing happens when a **GitHub Release is published**, which triggers
`.github/workflows/publish-nuget.yml` (build `-warnaserror` → test → `dotnet pack` →
`nuget push` with the `NUGET_API_KEY` secret). `.github/workflows/docs.yml` separately rebuilds the
DocFX site on push to `main`.

This mode tags and pushes — outward-facing and hard to reverse. **Preview first, confirm before any
tag/push/publish.**

1. **Pre-flight (read-only):**
   ```bash
   git rev-parse --abbrev-ref HEAD          # expect main
   git status --porcelain                    # expect clean tree
   git fetch --tags && git tag --list 'v*' --sort=-v:refname | head   # current highest tag
   ```
   Confirm `main` is up to date and CI on the latest commit is green
   (`gh run list --branch main --limit 1`). Run **Mode: verify** locally first.

2. **Choose the version.** MinVer tags are typically `v{Major}.{Minor}.{Patch}` (semver). Ask the
   user which bump (major/minor/patch) if not specified; derive the next tag from the highest
   existing one.

3. **Tag & push (after explicit confirmation):**
   ```bash
   git tag v{X.Y.Z}
   git push origin v{X.Y.Z}
   ```

4. **Publish the GitHub Release** (this is what triggers the NuGet push):
   ```bash
   gh release create v{X.Y.Z} --title "v{X.Y.Z}" --generate-notes
   ```

5. **Watch the pipeline:**
   ```bash
   gh run watch    # or: gh run list --workflow publish-nuget.yml --limit 1
   ```
   Confirm the package appears on NuGet. Report the published version and the run URL.

Never hand-edit a `<Version>` into `Dekori.csproj` — MinVer owns the version.
