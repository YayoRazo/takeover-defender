# Versioning and releasing

The version is a single 3-digit semver (`x.y.z`) kept in `Properties/AssemblyInfo.cs` (`AssemblyVersion`, `AssemblyFileVersion`, `AssemblyInformationalVersion`).

- The **patch** (3rd digit) is owned by the pre-commit hook and set automatically to `git commit count + 1`. Enable it once per clone:
  ```powershell
  git config core.hooksPath .githooks
  ```
- **major / minor** are bumped explicitly:
  ```powershell
  .\scripts\bump-version.ps1 minor            # 1.2.7 -> 1.3.0
  .\scripts\bump-version.ps1 major            # 1.2.7 -> 2.0.0
  .\scripts\bump-version.ps1 2.5.0 -DryRun    # preview
  ```
  Then update `CHANGELOG.md`, commit (`chore(release): bump version to x.y.z`).

## Releasing

1. `.\scripts\bump-version.ps1 minor` (or `major`).
2. Move `## Unreleased` entries in `CHANGELOG.md` under a dated `## x.y.z` section.
3. Commit, then tag and push:
   ```powershell
   git commit -am "chore(release): bump version to x.y.z"
   git tag vx.y.z
   git push origin main --tags
   ```
4. The `release` workflow builds x64 + x86, runs tests, and publishes a GitHub Release with `TakeoverDefender-x64.exe` and `TakeoverDefender-x86.exe`.
