# Audit Task: takeover-defender

## Project Context

**takeover-defender** is a C# WPF desktop app (.NET Framework 4.8, Windows 8+) that disables and re-enables Windows Defender. It operates with Administrator + SYSTEM privileges via scheduled tasks.

## Audit Objectives

Execute the following audit axes against the full codebase at `C:\dev\projects\takeover-defender`. For each finding, report file:line, severity (critical/high/medium/low), problem description, and concrete fix.

### 1. Security Sweep
- Are there any privilege escalation risks? (SYSTEM execution via scheduled tasks)
- Is the scheduled task XML sanitized? Injection risk in task name or command?
- Are registry writes validated? Could malformed paths or values corrupt the system?
- Are `takeown` / `icacls` operations safe? Could they be exploited if paths are manipulated?
- Is there any hardcoded credential, token, or key?
- Is `RunAsSystem` properly isolated? Could a failed task leave privileged artifacts?

### 2. Logic Bugs
- `DefenderManager.GetCurrentState()`: are the detection conditions correct and complete?
- Service control (`sc stop`, `sc config`): are exit codes checked? What if a service doesn't exist?
- Executable blocking: what if `takeown` fails? Does the rename cascade handle partial failures?
- `TamperProtection`: is the check for `val == 5` (default/unknown) correct on all Win10+ builds?
- `RunAsSystem`: what if `schtasks` fails silently? Timeout handling for stuck tasks?
- Are there race conditions between `taskkill` and `rename`?

### 3. Code Quality
- Magic numbers (e.g., `RegistryView.Registry64`, service start types 0/2/3/4)
- Naming conventions consistency across the project
- Exception handling: are errors swallowed silently where they shouldn't be?
- Duplicate code between `BlockDefenderExecutables` and `RestoreDefenderExecutables`
- Comment quality and missing XML doc

### 4. Test Gaps
- No tests for `DefenderManager` logic (state detection, service management)
- No tests for `CommandExecutor.RunAsSystem` (mocked or integration)
- No tests for Tamper Protection flow
- No tests for the WPF UI / button handlers
- No tests for edge cases: missing services, missing executables, permission denied

### 5. Performance
- `GetDefenderExeTargets()` allocates a new list on every call — could be cached
- Multiple `RunAsSystem` calls per operation — could they be batched?
- `RefreshStatus` runs on UI thread — could it block the UI?
- Foreach loops over service dictionary create string builders each time

### 6. Visual / UX Review
- Are the button colors (#e64553, #40a02b) accessible? (contrast ratio)
- Is the status indicator clear? (dot color + text)
- Is the log output readable and informative?
- Window sizing, minimum size, resize behavior
- Does it need a progress bar during long operations?

### 7. Dependency / Supply Chain
- .NET Framework 4.8 runtime requirement
- Is `System.Management` actually used? (referenced in csproj)
- xUnit test packages: are versions current?
- Any NuGet packages with known vulnerabilities?

### 8. Documentation
- Is the README accurate and complete?
- Are build instructions correct for all supported platforms?
- Is the compatibility matrix accurate?

## Instructions

1. Read every `.cs`, `.xaml`, `.csproj`, and `.json` file in the repo
2. For each audit axis, produce a list of findings
3. Each finding must include: `file:line`, severity, problem, fix
4. Skip praise — only report problems
5. Output format: markdown bullet list grouped by axis

## Repo Path

```
C:\dev\projects\takeover-defender
```
