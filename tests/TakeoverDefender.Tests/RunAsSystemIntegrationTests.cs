using System;
using System.IO;
using System.Security.Principal;
using Xunit;
using TakeoverDefender.Utilities;

namespace TakeoverDefender.Tests
{
    /// <summary>
    /// End-to-end check of the SYSTEM-via-scheduled-task path (the audit's CRITICAL fix:
    /// XML escaping + COM run + result verification + cleanup). Harmless: it only writes
    /// a marker file, it never touches Windows Defender.
    /// Requires the test host to run elevated (Administrator); otherwise it self-skips.
    /// </summary>
    public class RunAsSystemIntegrationTests
    {
        private static bool IsElevated()
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        [Fact]
        public void RunAsSystem_EchoMarker_WritesMarkerFile()
        {
            if (!IsElevated())
            {
                // Skipped: needs an elevated test host to register a SYSTEM task.
                return;
            }

            string marker = Path.Combine(Path.GetTempPath(), "td_marker_" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                File.Delete(marker);
            }
            catch { }

            CommandExecutor.RunAsSystem($"echo TD_OK > \"{marker}\"");

            Assert.True(File.Exists(marker), "SYSTEM task did not create the marker file.");
            Assert.Contains("TD_OK", File.ReadAllText(marker));

            try { File.Delete(marker); } catch { }
        }

        [Fact]
        public void RunAsSystem_FailingCommand_Throws()
        {
            if (!IsElevated())
                return;

            Assert.ThrowsAny<Exception>(() =>
                CommandExecutor.RunAsSystem("exit 3"));
        }
    }
}
