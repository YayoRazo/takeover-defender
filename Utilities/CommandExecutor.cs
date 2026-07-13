using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;

namespace TakeoverDefender.Utilities
{
    internal static class CommandExecutor
    {
        private const int DefaultTimeoutMs = 60000;
        private const int ShortTimeoutMs = 30000;
        private const int SystemTaskPollAttempts = 90;
        private const int SystemTaskPollIntervalMs = 1000;
        internal const string SystemTaskPrefix = "Tkd_";

        internal static int RunPowerShell(string script)
        {
            return RunPowerShellCore(script, out _);
        }

        internal static string RunPowerShellWithOutput(string script)
        {
            RunPowerShellCore(script, out string output);
            return output;
        }

        private static int RunPowerShellCore(string script, out string output)
        {
            output = string.Empty;
            if (string.IsNullOrEmpty(PathLocator.PowerShell))
            {
                Debug.WriteLine("RunPowerShell: no PowerShell executable found.");
                return -1;
            }

            ProcessStartInfo psi = NewHiddenProcess(PathLocator.PowerShell,
                "-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command -",
                redirectInput: true, capture: true);

            return Run(psi, out output, DefaultTimeoutMs, script);
        }

        internal static void RunCommand(string command, string arguments = "")
        {
            int code = RunCommandWithExit(command, arguments);
            if (code != 0)
                Debug.WriteLine($"Command '{command}' exited with code {code}.");
        }

        internal static string RunCommandWithOutput(string command, string arguments = "")
        {
            ProcessStartInfo psi = BuildShellPsi(command, arguments, capture: true, redirectInput: false);
            Run(psi, out string output, ShortTimeoutMs);
            return output.Trim();
        }

        internal static int RunCommandWithExit(string command, string arguments = "")
        {
            ProcessStartInfo psi = BuildShellPsi(command, arguments, capture: false, redirectInput: false);
            return Run(psi, out _, DefaultTimeoutMs);
        }

        private static ProcessStartInfo BuildShellPsi(string command, string arguments, bool capture, bool redirectInput)
        {
            if (string.IsNullOrEmpty(arguments))
                return NewHiddenProcess("cmd.exe", $"/c {command}", redirectInput, capture);
            return NewHiddenProcess(command, arguments, redirectInput, capture);
        }

        private static ProcessStartInfo NewHiddenProcess(string fileName, string arguments, bool redirectInput, bool capture)
        {
            return new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = redirectInput,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }

        private static int Run(ProcessStartInfo psi, out string output, int timeoutMs, string stdinText = null)
        {
            StringBuilder stdout = new StringBuilder();
            StringBuilder stderr = new StringBuilder();

            using (Process p = new Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.Append(e.Data + Environment.NewLine); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.Append(e.Data + Environment.NewLine); };

                try
                {
                    p.Start();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to start '{psi.FileName}': {ex.Message}");
                    output = string.Empty;
                    return -1;
                }

                if (stdinText != null)
                {
                    p.StandardInput.Write(stdinText);
                    p.StandardInput.Close();
                }

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                bool exited = p.WaitForExit(timeoutMs);
                if (!exited)
                {
                    try { p.Kill(); } catch { }
                    Debug.WriteLine($"Process '{psi.FileName}' timed out after {timeoutMs}ms and was killed.");
                }
                p.WaitForExit();

                output = stdout.ToString();
                if (stderr.Length > 0)
                    Debug.WriteLine($"[{psi.FileName}] {stderr}");
                return p.HasExited ? p.ExitCode : -1;
            }
        }

        internal static void RunAsSystem(string command)
        {
            string taskName = SystemTaskPrefix + Guid.NewGuid().ToString("N").Substring(0, 8);
            string xmlPath = Path.Combine(Path.GetTempPath(), taskName + ".xml");

            try
            {
                string xmlTask = BuildTaskXml(command);
                File.WriteAllText(xmlPath, xmlTask, Encoding.Unicode);

                int create = RunCommandWithExit($"schtasks /create /tn \"{taskName}\" /xml \"{xmlPath}\" /f");
                if (create != 0)
                    throw new InvalidOperationException(
                        $"Could not create SYSTEM task '{taskName}' (schtasks exit {create}).");

                int run = RunCommandWithExit($"schtasks /run /tn \"{taskName}\"");
                if (run != 0)
                    throw new InvalidOperationException(
                        $"Could not start SYSTEM task '{taskName}' (schtasks exit {run}).");

                WaitForSystemTask(taskName);
                VerifySystemTaskResult(taskName);
            }
            finally
            {
                RunCommand($"schtasks /delete /tn \"{taskName}\" /f");
                try { File.Delete(xmlPath); } catch { }
            }
        }

        private static void WaitForSystemTask(string taskName)
        {
            for (int i = 0; i < SystemTaskPollAttempts; i++)
            {
                Thread.Sleep(SystemTaskPollIntervalMs);
                string status = RunCommandWithOutput($"schtasks /query /tn \"{taskName}\" /fo csv /nh");
                if (string.IsNullOrEmpty(status) || !status.Contains("\"Running\""))
                    return;
            }
            Debug.WriteLine($"SYSTEM task '{taskName}' still running after polling; relying on ExecutionTimeLimit + finally.");
        }

        private static void VerifySystemTaskResult(string taskName)
        {
            string details = RunCommandWithOutput($"schtasks /query /tn \"{taskName}\" /v /fo list");
            foreach (string line in details.Split('\n'))
            {
                string trimmed = line.Trim('\r', ' ');
                int idx = trimmed.LastIndexOf(':');
                if (idx < 0) continue;
                string value = trimmed.Substring(idx + 1).Trim();
                if (value.Length >= 3 && value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    if (!value.Equals("0x0", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"SYSTEM task '{taskName}' exited with result {value}.");
                    return;
                }
            }
            Debug.WriteLine($"Could not determine last result of SYSTEM task '{taskName}'.");
        }

        internal static void CleanupStaleSystemTasks()
        {
            try
            {
                string listing = RunCommandWithOutput("schtasks /query /fo csv /nh");
                foreach (string raw in listing.Split('\n'))
                {
                    string first = raw.Split(',')[0].Trim('"', '\r', ' ');
                    if (first.StartsWith(SystemTaskPrefix, StringComparison.OrdinalIgnoreCase))
                        RunCommand($"schtasks /delete /tn \"{first}\" /f");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Stale task cleanup failed: {ex.Message}");
            }
        }

        internal static string BuildTaskXml(string command)
        {
            string escaped = SecurityElement.Escape(command ?? string.Empty);
            return
$@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <Principals>
    <Principal id=""Author"">
      <UserId>S-1-5-18</UserId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>true</Hidden>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <ExecutionTimeLimit>PT2M</ExecutionTimeLimit>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>cmd.exe</Command>
      <Arguments>/c {escaped}</Arguments>
    </Exec>
  </Actions>
</Task>";
        }
    }
}
