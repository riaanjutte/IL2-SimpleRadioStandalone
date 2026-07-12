using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests
{
    [TestClass]
    public class TestEnvironment
    {
        private static string _configPath;

        [AssemblyInitialize]
        public static void Initialize(TestContext context)
        {
            _configPath = Path.Combine(
                Path.GetTempPath(),
                "il2-srs-tests-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("IL2_SRS_CONFIG_DIR", _configPath);
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            Environment.SetEnvironmentVariable("IL2_SRS_CONFIG_DIR", null);
            try
            {
                if (!string.IsNullOrWhiteSpace(_configPath) && Directory.Exists(_configPath))
                {
                    Directory.Delete(_configPath, true);
                }
            }
            catch
            {
            }
        }
    }
}
