using System;
using System.Xml;
using Xunit;
using TakeoverDefender.Utilities;

namespace TakeoverDefender.Tests
{
    public class RegistryHelpTests
    {
        [Fact]
        public void GetValue_ExistingDword_ReturnsCorrectValue()
        {
            var result = RegistryHelp.GetValue<int>(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "CurrentBuild", 0);

            Assert.True(result > 0, $"Expected build number > 0, got {result}");
        }

        [Fact]
        public void GetValue_NonExistingKey_ReturnsDefault()
        {
            var result = RegistryHelp.GetValue<string>(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\NonExistentKey",
                "NonExistentValue", "default");

            Assert.Equal("default", result);
        }

        [Fact]
        public void GetValue_NullableInt_NonExisting_ReturnsNull()
        {
            var result = RegistryHelp.GetValue<int?>(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\NonExistentKey",
                "NonExistentValue", null);

            Assert.Null(result);
        }
    }

    public class PathLocatorTests
    {
        [Fact]
        public void FindExecutable_CmdExe_ReturnsPath()
        {
            string path = PathLocator.FindExecutable("cmd.exe");

            Assert.False(string.IsNullOrEmpty(path), "cmd.exe should be found in PATH");
            Assert.EndsWith("cmd.exe", path, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void FindExecutable_NonExistent_ReturnsEmpty()
        {
            string path = PathLocator.FindExecutable("nonexistent_xyz123.exe");

            Assert.True(string.IsNullOrEmpty(path));
        }

        [Fact]
        public void FindExecutable_PowerShell_ReturnsPath()
        {
            string path = PathLocator.FindExecutable("powershell.exe");

            Assert.False(string.IsNullOrEmpty(path), "powershell.exe should be found");
            Assert.EndsWith("powershell.exe", path, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetDefenderExeTargets_IsCached()
        {
            var first = PathLocator.GetDefenderExeTargets();
            var second = PathLocator.GetDefenderExeTargets();

            Assert.Same(first, second);
        }
    }

    public class CommandExecutorTests
    {
        [Fact]
        public void RunCommandWithOutput_Echo_ReturnsEchoedText()
        {
            string output = CommandExecutor.RunCommandWithOutput("echo HelloTest");

            Assert.Contains("HelloTest", output);
        }

        [Fact]
        public void RunCommandWithOutput_Whoami_ReturnsNonEmpty()
        {
            string output = CommandExecutor.RunCommandWithOutput("whoami");

            Assert.False(string.IsNullOrEmpty(output));
        }

        [Fact]
        public void BuildTaskXml_Ampersand_IsEscaped()
        {
            string xml = CommandExecutor.BuildTaskXml("sc stop \"WinDefend\" & sc config \"WinDefend\" start= disabled");

            Assert.Contains("&amp;", xml);
            Assert.DoesNotContain(" & ", xml);
        }

        [Fact]
        public void BuildTaskXml_LessThan_IsEscaped()
        {
            string xml = CommandExecutor.BuildTaskXml("echo a < b");

            Assert.Contains("&lt;", xml);
            Assert.DoesNotContain(" < ", xml);
        }

        [Fact]
        public void BuildTaskXml_ProducesValidXml()
        {
            string command = "reg add \"HKLM\\SOFTWARE\\X\" /v A /t REG_DWORD /d 0 /f & reg add \"HKLM\\SOFTWARE\\X\" /v B /t REG_DWORD /d 1 /f";
            string xml = CommandExecutor.BuildTaskXml(command);

            var doc = new XmlDocument();
            doc.LoadXml(xml);

            XmlNamespaceManager ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("t", "http://schemas.microsoft.com/windows/2004/02/mit/task");
            XmlNode args = doc.SelectSingleNode("//t:Arguments", ns);

            Assert.NotNull(args);
            Assert.Contains("/c ", args.InnerText);
        }

        [Fact]
        public void BuildTaskXml_IncludesExecutionTimeLimit()
        {
            string xml = CommandExecutor.BuildTaskXml("echo hi");

            Assert.Contains("<ExecutionTimeLimit>", xml);
        }
    }

    public class DefenderManagerStateTests
    {
        [Fact]
        public void EvaluateState_ServiceRunning_NoFlags_Active()
        {
            Assert.Equal(DefenderManager.DefenderState.Active,
                DefenderManager.EvaluateState(null, null, true));
        }

        [Fact]
        public void EvaluateState_DisableRealtimeFlag_Inactive()
        {
            Assert.Equal(DefenderManager.DefenderState.Inactive,
                DefenderManager.EvaluateState(1, null, true));
        }

        [Fact]
        public void EvaluateState_DisableAntiSpywareFlag_Inactive()
        {
            Assert.Equal(DefenderManager.DefenderState.Inactive,
                DefenderManager.EvaluateState(null, 1, true));
        }

        [Fact]
        public void EvaluateState_ServiceNotRunning_Inactive()
        {
            Assert.Equal(DefenderManager.DefenderState.Inactive,
                DefenderManager.EvaluateState(null, null, false));
        }

        [Fact]
        public void EvaluateState_ZeroFlags_NotRunning_Inactive()
        {
            Assert.Equal(DefenderManager.DefenderState.Inactive,
                DefenderManager.EvaluateState(0, 0, false));
        }
    }

    public class DotNetFrameworkGuardTests
    {
        [Theory]
        [InlineData(0, false)]
        [InlineData(461814, false)]      // .NET Framework 4.7.2
        [InlineData(528039, false)]      // one below the 4.8 floor
        [InlineData(528040, true)]       // 4.8 on Windows 10 1903/1909
        [InlineData(528049, true)]       // 4.8 on all other OS versions
        [InlineData(528449, true)]       // 4.8 on Windows 11
        [InlineData(533325, true)]       // 4.8.1, still satisfies the 4.8 floor
        public void IsInstalled_ComparesAgainstReleaseFloor(int release, bool expected)
        {
            Assert.Equal(expected, DotNetFrameworkGuard.IsInstalled(release));
        }
    }
}
