using System;
using System.IO;
using System.ServiceProcess;
using Xunit;
using TakeoverDefender.Utilities;

namespace TakeoverDefender.Tests
{
    public class WindowsCompatibilityTests
    {
        [Fact]
        public void OsVersion_DetectsWin8Plus()
        {
            var version = Environment.OSVersion.Version;
            bool isWin8Plus = version.Major > 6 || (version.Major == 6 && version.Minor >= 2);
            Assert.True(isWin8Plus, $"Expected Windows 8+, got {version}");
        }

        [Fact]
        public void Is64BitOS_MatchesEnvironment()
        {
            Assert.Equal(Environment.Is64BitOperatingSystem, PathLocator.Is64BitOS);
        }

        [Fact]
        public void Service_WinDefend_StatusReadable()
        {
            try
            {
                using var sc = new ServiceController("WinDefend");
                var status = sc.Status;
                Assert.True(Enum.IsDefined(typeof(ServiceControllerStatus), status));
            }
            catch (InvalidOperationException)
            {
                // WinDefend not present on this Windows SKU (e.g. Server Core without Defender).
            }
        }

        [Fact]
        public void WindowsDefenderDirectory_Found()
        {
            string dir = PathLocator.WindowsDefenderDir;
            Assert.True(Directory.Exists(dir), $"Defender directory not found at: {dir}");
        }

        [Fact]
        public void GetDefenderExeTargets_ReturnsNonEmpty()
        {
            var targets = PathLocator.GetDefenderExeTargets();
            Assert.NotEmpty(targets);
            Assert.All(targets, t =>
            {
                Assert.NotNull(t.Normal);
                Assert.NotNull(t.Block);
            });
        }
    }
}
