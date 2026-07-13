using System;
using System.IO;
using System.Text;
using System.Threading;
using Xunit;
using TakeoverDefender.Utilities;

namespace TakeoverDefender.Tests
{
    /// <summary>
    /// Destructive, on-demand verification of the real disable/restore flow against the
    /// live Windows Defender on this machine. Self-skips unless BOTH the host is elevated
    /// AND the environment variable TD_DESTRUCTIVE=1 is set, so it never runs as part of a
    /// normal test pass. Leaves Defender DISABLED at the end.
    /// </summary>
    public class DefenderFlowIntegrationTests
    {
        private const string LogPath = @"C:\Users\jorge\AppData\Local\Temp\opencode\td_flow.log";
        private readonly StringBuilder _sb = new StringBuilder();

        private void L(string line)
        {
            _sb.AppendLine(line);
            File.WriteAllText(LogPath, _sb.ToString(), Encoding.UTF8);
        }

        private static bool ShouldRun()
        {
            if (Environment.GetEnvironmentVariable("TD_DESTRUCTIVE") != "1") return false;
            using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(id)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private static DefenderManager.DefenderState PollState(int seconds)
        {
            DefenderManager.DefenderState state = DefenderManager.DefenderState.Unknown;
            for (int i = 0; i < seconds; i++)
            {
                state = DefenderManager.GetCurrentState();
                if (state != DefenderManager.DefenderState.Unknown) break;
                Thread.Sleep(1000);
            }
            return state;
        }

        private int Disable()
        {
            int total = 0;
            bool tamperOn = DefenderManager.IsTamperProtectionEnabled();
            L($"  tamper initially enabled (via API): {tamperOn}");
            if (tamperOn)
            {
                bool ok = DefenderManager.DisableTamperProtection();
                L($"  tamper disable result: {ok}");
            }

            int a = DefenderManager.DeactivatePart1_Services(); total += a; L($"  [1/6] services: {a} failure(s)");
            int b = DefenderManager.DeactivatePart2_Registry(); total += b; L($"  [2/6] registry: {b} failure(s)");
            bool c = DefenderManager.DeactivatePart3_MpPreference(); L($"  [3/6] mppref: {(c ? "OK" : "FAILED")}");
            int d = DefenderManager.DeactivatePart4_BlockExecutables(); total += d; L($"  [4/6] block exe: {d} failure(s)");
            DefenderManager.DeactivatePart5_CleanFolders(); L("  [5/6] clean folders: done");
            int f = DefenderManager.DeactivatePart6_SmartScreen(); total += f; L($"  [6/6] smartscreen: {f} failure(s)");
            return total;
        }

        private int Restore()
        {
            int total = 0;
            DefenderManager.RestoreTamperIfDisabled(); L("  tamper: restored-if-disabled");

            int a = DefenderManager.ActivatePart1_RestoreExecutables(); total += a; L($"  [1/5] restore exe: {a} failure(s)");
            int b = DefenderManager.ActivatePart2_Services(); total += b; L($"  [2/5] services: {b} failure(s)");
            int c = DefenderManager.ActivatePart3_Registry(); total += c; L($"  [3/5] registry: {c} failure(s)");
            bool d = DefenderManager.ActivatePart4_MpPreference(); L($"  [4/5] mppref: {(d ? "OK" : "FAILED")}");
            int e = DefenderManager.ActivatePart5_SmartScreen(); total += e; L($"  [5/5] smartscreen: {e} failure(s)");
            return total;
        }

        [Fact]
        public void DisableThenRestoreThenDisable_RealDefender()
        {
            if (!ShouldRun()) return;

            try
            {
                L($"=== Defender flow integration @ {DateTime.Now:O} ===");

                L("-- PHASE 1: DISABLE --");
                int fail1 = Disable();
                DefenderManager.DefenderState afterDisable = PollState(15);
                L($"  >> state after disable: {afterDisable}");

                L("-- PHASE 2: RESTORE --");
                int fail2 = Restore();
                DefenderManager.DefenderState afterRestore = PollState(60);
                L($"  >> state after restore: {afterRestore}");

                L("-- PHASE 3: DISABLE (leave off) --");
                int fail3 = Disable();
                DefenderManager.DefenderState finalState = PollState(15);
                L($"  >> final state: {finalState}");
                L($"TOTALS disable1={fail1} restore={fail2} disable2={fail3}");

                Assert.Equal(DefenderManager.DefenderState.Inactive, afterDisable);
                Assert.Equal(DefenderManager.DefenderState.Active, afterRestore);
                Assert.Equal(DefenderManager.DefenderState.Inactive, finalState);
            }
            catch (Exception ex)
            {
                L($"!! EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }
    }
}
