using System.Linq;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class WindowsFirewallTelemetryDiagnosticTests
    {
        private const string SrsPath = @"C:\Program Files\IL2-SRS\IL2-SR-ClientRadio.exe";

        [TestMethod]
        public void MissingRuleOnActiveProfileProducesWarning()
        {
            WindowsFirewallPolicySnapshot policy = ActivePrivatePolicy();

            TelemetryDiagnosticItem result =
                WindowsFirewallTelemetryDiagnostic.Evaluate(policy, SrsPath, 4322).Single();

            Assert.AreEqual(TelemetryDiagnosticSeverity.Warning, result.Severity);
            StringAssert.Contains(result.Title, "access missing");
            StringAssert.Contains(result.Detail, "both Private and Public");
        }

        [TestMethod]
        public void MatchingApplicationAllowRulePasses()
        {
            WindowsFirewallPolicySnapshot policy = ActivePrivatePolicy();
            policy.Rules.Add(new WindowsFirewallRuleSnapshot
            {
                Enabled = true,
                Allow = true,
                Inbound = true,
                Profiles = WindowsFirewallProfile.Private,
                Protocol = 256,
                ApplicationPath = SrsPath,
                LocalPorts = "*"
            });

            TelemetryDiagnosticItem result =
                WindowsFirewallTelemetryDiagnostic.Evaluate(policy, SrsPath, 4322).Single();

            Assert.AreEqual(TelemetryDiagnosticSeverity.Ok, result.Severity);
        }

        [TestMethod]
        public void BlockRuleTakesPrecedenceOverAllowRule()
        {
            WindowsFirewallPolicySnapshot policy = ActivePrivatePolicy();
            policy.Rules.Add(Rule(true));
            policy.Rules.Add(Rule(false));

            TelemetryDiagnosticItem result =
                WindowsFirewallTelemetryDiagnostic.Evaluate(policy, SrsPath, 4322).Single();

            Assert.AreEqual(TelemetryDiagnosticSeverity.Warning, result.Severity);
            StringAssert.Contains(result.Title, "blocked");
        }

        [TestMethod]
        public void InactiveProfileDoesNotCauseWarning()
        {
            WindowsFirewallPolicySnapshot policy = ActivePrivatePolicy();
            policy.Rules.Add(Rule(true));

            TelemetryDiagnosticItem result =
                WindowsFirewallTelemetryDiagnostic.Evaluate(policy, SrsPath, 4322).Single();

            Assert.AreEqual(TelemetryDiagnosticSeverity.Ok, result.Severity);
            Assert.IsFalse(result.Title.Contains("Public"));
        }

        [TestMethod]
        public void GenericUdpPortRulePasses()
        {
            WindowsFirewallPolicySnapshot policy = ActivePrivatePolicy();
            policy.Rules.Add(new WindowsFirewallRuleSnapshot
            {
                Enabled = true,
                Allow = true,
                Inbound = true,
                Profiles = WindowsFirewallProfile.Private,
                Protocol = 17,
                ApplicationPath = string.Empty,
                LocalPorts = "4300-4330"
            });

            TelemetryDiagnosticItem result =
                WindowsFirewallTelemetryDiagnostic.Evaluate(policy, SrsPath, 4322).Single();

            Assert.AreEqual(TelemetryDiagnosticSeverity.Ok, result.Severity);
        }

        [TestMethod]
        public void UnscopedRuleWithoutApplicationOrPortDoesNotPass()
        {
            WindowsFirewallPolicySnapshot policy = ActivePrivatePolicy();
            policy.Rules.Add(new WindowsFirewallRuleSnapshot
            {
                Enabled = true,
                Allow = true,
                Inbound = true,
                Profiles = WindowsFirewallProfile.Private,
                Protocol = 256,
                ApplicationPath = string.Empty,
                LocalPorts = string.Empty
            });

            TelemetryDiagnosticItem result =
                WindowsFirewallTelemetryDiagnostic.Evaluate(policy, SrsPath, 4322).Single();

            Assert.AreEqual(TelemetryDiagnosticSeverity.Warning, result.Severity);
            StringAssert.Contains(result.Title, "access missing");
        }

        private static WindowsFirewallPolicySnapshot ActivePrivatePolicy()
        {
            return new WindowsFirewallPolicySnapshot
            {
                Available = true,
                ActiveProfiles = WindowsFirewallProfile.Private,
                EnabledProfiles = WindowsFirewallProfile.Private
            };
        }

        private static WindowsFirewallRuleSnapshot Rule(bool allow)
        {
            return new WindowsFirewallRuleSnapshot
            {
                Enabled = true,
                Allow = allow,
                Inbound = true,
                Profiles = WindowsFirewallProfile.Private,
                Protocol = 256,
                ApplicationPath = SrsPath,
                LocalPorts = "*"
            };
        }
    }
}
