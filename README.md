# Takeover Defender

Take full control of Windows Defender — disable and re-enable at will.

Works on Windows 8 and later (x86 and x64). Windows 7 and earlier are not supported. Tamper Protection handling requires Windows 10+.

## What it does

**TAKE OVER** disables Defender's protection end to end: services, registry policies,
real-time/behavior/IOAV scanning, and SmartScreen. It will not scan your files and
will not interfere with building or running software. **RESTORE** puts everything
back exactly as it was.

A `MsMpEng.exe` process may stay loaded (idle, ~0% CPU) after TAKE OVER — this is a
Windows OS protection, not a bug, and it's harmless. See
[docs/technical-details.md](docs/technical-details.md) for the full list of what
changes, why the process lingers, and how to stop it entirely via Safe Mode.

## Antivirus and browser warnings (expected)

Takeover Defender is an **unsigned executable whose only job is to disable Windows
Defender**, so antivirus engines and browsers **will flag it** as `HackTool`/`PUA`
or block the download — that's an expected false positive, not a sign of infection.
See the [VirusTotal report](https://www.virustotal.com/gui/file/0e09359d4dded06f457dc1949a2a5eaf95c8e645b7f0e58d3fd52a89243fa16c) for this build.

To run it anyway:

1. Download the `.exe`. In the browser warning pick *Keep* → *Keep anyway*.
2. If Defender quarantines it first, add an exclusion: *Windows Security → Virus &
   threat protection → Manage settings → Exclusions → Add an exclusion → File* →
   select `TakeoverDefender.exe`.
3. Run it as Administrator.

## Usage

1. Run `TakeoverDefender.exe` as **Administrator**
2. Click **TAKE OVER** — disables Defender
3. Click **RESTORE** — re-enables Defender

## Requirements

- Windows 8 or later
- Administrator privileges
- .NET Framework 4.8

## Getting the app

Download the latest build from [Releases](https://github.com/YayoRazo/takeover-defender/releases), or build it yourself — see [docs/building.md](docs/building.md).

Maintainers: see [docs/releasing.md](docs/releasing.md) for the versioning and release process.

## License

MIT — see [LICENSE](LICENSE).
