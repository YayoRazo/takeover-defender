# Technical details

## What TAKE OVER actually changes

Disabling is not a single switch. The **TAKE OVER** operation performs, in order:

1. **Tamper Protection** (Windows 10+) — temporarily set to OFF so the rest can take.
2. **Defender services** — stops and disables `WinDefend`, `SecurityHealthService`, `WdFilter`, `WdBoot`, `wscsvc`, and (on Win10+) the modern-protection/driver set.
3. **Registry policies** — writes `DisableAntiSpyware`/`DisableRealtimeMonitoring`/etc. under `SOFTWARE\Policies\Microsoft\Windows Defender` and related keys, plus Security Center `AntiVirusOverride`/`FirewallOverride`, and firewall-profile `DisableNotifications` for Domain/Private/Standard profiles.
4. **MpPreference** — `Set-MpPreference` to turn off real-time, behavior, IOAV, script, archive, IPS scanning and sample submission.
5. **Executable blocking** — takes ownership of `MsMpEng.exe`, `NisSrv.exe`, `MpCmdRun.exe`, `MpDefenderCoreService.exe`, `smartscreen.exe`, etc., and renames them so they cannot launch.
6. **Folder cleanup** — clears Defender scan history / workspace / support folders.
7. **SmartScreen** — disables SmartScreen for Explorer, Edge phishing filter, and web content evaluation.

**RESTORE** reverses all of the above, restoring each service to the start-type it had before TAKE OVER (captured at disable time) and re-enabling Tamper Protection only if TAKE OVER disabled it.

Privileged work (service control, ownership/rename, tamper registry writes) runs as **SYSTEM** through short-lived scheduled tasks named `Tkd_*` that are deleted immediately after use; any stale `Tkd_*` tasks are also cleaned up at startup.

## The residual MsMpEng.exe process

**TAKE OVER fully disables Defender's protection** — real-time scanning, behavior monitoring, IOAV, antivirus and antispyware engines are all OFF. It will **not** scan your files, and it will **not interfere with developing software or compiling code** (this is the part that used to slow things down).

On modern Windows 10/11 (and especially Insider builds), the **`MsMpEng.exe` (Antimalware Service Executable) process stays loaded** even after TAKE OVER. This is by OS design: `WinDefend` is a Protected Process Light (PPL) service, and the OS blocks `sc config`/`sc stop`/registry writes/ownership-takeover against it even when running as SYSTEM. What you get is:

- **CPU: ~0%** — the process is idle (all scanning engines are off).
- **RAM: ~80–100 MB** — just the cost of keeping the process loaded.
- **No interference** — no scanning, no real-time hooks, no slowdown when building/compiling.

So for the common goal ("stop Defender from interfering with my dev work"), TAKE OVER alone is sufficient and the lingering process is harmless.

## Safe Mode: stopping MsMpEng.exe entirely

To stop `WinDefend`/`MsMpEng.exe` from running at all (and survive a reboot), the service must be disabled from **Safe Mode** — the only context where the SCM allows reconfiguring the protected service. Two helper scripts are provided:

1. Boot into **Safe Mode** (e.g. `msconfig` → *Boot* → *Safe boot* → *Minimal* → restart).
2. Run as Administrator:
   ```powershell
   PowerShell -ExecutionPolicy Bypass -File .\scripts\disable-defender-safemode.ps1
   ```
   It sets every Defender service to `start= disabled`.
3. Undo Safe Mode boot (`msconfig` → uncheck *Safe boot*) and reboot normally. `WinDefend` will be **Stopped** and `MsMpEng.exe` will **not** run.

Reversible with `.\scripts\enable-defender-safemode.ps1` (also run in Safe Mode).

## Reducing antivirus/browser warnings

- **Self-sign** the executable with a code-signing certificate (removes most
  SmartScreen reputation warnings; AV heuristic detection usually remains).
- Submit the binary to Microsoft (and other AV vendors) as a false positive through
  their submission portals so the detection signatures get updated.
