# Changelog

All versions are three-digit semver (`major.minor.patch`). The `patch` digit is set
automatically to the git commit count by the pre-commit hook; `major`/`minor` are
bumped via `scripts/bump-version.ps1`.

## 0.1.11 - 2026-07-12

- Initial release: C# WPF (.NET Framework 4.8) tool to disable and re-enable Windows Defender on Windows 8+.
- Audit-driven hardening: SYSTEM work runs through short-lived scheduled tasks with XML-escaped arguments and an ACL-restricted temp file; the task is driven via the `Schedule.Service` COM API (numeric state + `LastTaskResult`) so it is correct on non-English Windows.
- Tamper Protection detected via the Defender API (`Get-MpComputerStatus.IsTamperProtected`) instead of the unreliable registry value.
- Partial-failure reporting for services, registry policies, executable block/restore and SmartScreen; the UI surfaces per-step failure counts.
- Safe Mode scripts (`scripts/disable-defender-safemode.ps1`, `enable-defender-safemode.ps1`) to fully stop the `WinDefend` service on modern/Insider builds where it is PPL-protected.
- Honest documentation of the residual `MsMpEng.exe` process (idle, ~0% CPU) after TAKE OVER.
- 26 unit + integration tests, including an elevated end-to-end check of the SYSTEM-via-scheduled-task path.
