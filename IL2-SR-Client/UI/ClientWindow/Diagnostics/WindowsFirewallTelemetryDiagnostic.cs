using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Diagnostics
{
    [Flags]
    internal enum WindowsFirewallProfile
    {
        None = 0,
        Domain = 1,
        Private = 2,
        Public = 4
    }

    internal sealed class WindowsFirewallRuleSnapshot
    {
        public bool Enabled { get; set; }
        public bool Allow { get; set; }
        public bool Inbound { get; set; }
        public WindowsFirewallProfile Profiles { get; set; }
        public int Protocol { get; set; }
        public string ApplicationPath { get; set; }
        public string LocalPorts { get; set; }
    }

    internal sealed class WindowsFirewallPolicySnapshot
    {
        public WindowsFirewallPolicySnapshot()
        {
            EnabledProfiles = WindowsFirewallProfile.None;
            DefaultInboundAllowProfiles = WindowsFirewallProfile.None;
            Rules = new List<WindowsFirewallRuleSnapshot>();
        }

        public bool Available { get; set; }
        public string Error { get; set; }
        public WindowsFirewallProfile ActiveProfiles { get; set; }
        public WindowsFirewallProfile EnabledProfiles { get; set; }
        public WindowsFirewallProfile DefaultInboundAllowProfiles { get; set; }
        public IList<WindowsFirewallRuleSnapshot> Rules { get; private set; }
    }

    internal static class WindowsFirewallTelemetryDiagnostic
    {
        private const int FirewallActionAllow = 1;
        private const int FirewallDirectionInbound = 1;
        private const int ProtocolUdp = 17;
        private const int ProtocolAny = 256;

        public static IEnumerable<TelemetryDiagnosticItem> DiagnoseCurrentProcess(int telemetryPort)
        {
            string executablePath;
            try
            {
                executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            }
            catch
            {
                executablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IL2-SR-ClientRadio.exe");
            }

            return Evaluate(ReadPolicy(), executablePath, telemetryPort);
        }

        internal static IList<TelemetryDiagnosticItem> Evaluate(
            WindowsFirewallPolicySnapshot policy,
            string executablePath,
            int telemetryPort)
        {
            List<TelemetryDiagnosticItem> items = new List<TelemetryDiagnosticItem>();
            if (policy == null || !policy.Available)
            {
                items.Add(new TelemetryDiagnosticItem(
                    TelemetryDiagnosticSeverity.Info,
                    "Windows Firewall check unavailable",
                    string.IsNullOrWhiteSpace(policy?.Error)
                        ? "SRS could not inspect Windows Firewall. Check that IL2-SR-ClientRadio.exe is allowed for both Private and Public networks."
                        : policy.Error));
                return items;
            }

            WindowsFirewallProfile activeEnabledProfiles = policy.ActiveProfiles & policy.EnabledProfiles;
            if (activeEnabledProfiles == WindowsFirewallProfile.None)
            {
                items.Add(new TelemetryDiagnosticItem(
                    TelemetryDiagnosticSeverity.Info,
                    "Windows Firewall disabled",
                    "Windows Firewall is not enabled on the currently active network profile."));
                return items;
            }

            foreach (WindowsFirewallProfile profile in EnumerateProfiles(activeEnabledProfiles))
            {
                List<WindowsFirewallRuleSnapshot> matchingRules = policy.Rules
                    .Where(rule => RuleApplies(rule, profile, executablePath, telemetryPort))
                    .ToList();

                if (matchingRules.Any(rule => !rule.Allow))
                {
                    items.Add(new TelemetryDiagnosticItem(
                        TelemetryDiagnosticSeverity.Warning,
                        "SRS blocked by Windows Firewall (" + ProfileName(profile) + ")",
                        "An enabled inbound block rule applies to IL2-SR-ClientRadio.exe or UDP port "
                        + telemetryPort
                        + ". Remove the block rule, allow IL2-SR-ClientRadio.exe for both Private and Public networks, then restart IL-2 and SRS."));
                    continue;
                }

                if (matchingRules.Any(rule => rule.Allow)
                    || (policy.DefaultInboundAllowProfiles & profile) != 0)
                {
                    items.Add(new TelemetryDiagnosticItem(
                        TelemetryDiagnosticSeverity.Ok,
                        "Windows Firewall access (" + ProfileName(profile) + ")",
                        "IL2-SR-ClientRadio.exe is allowed to receive IL-2 telemetry on UDP port " + telemetryPort + "."));
                    continue;
                }

                items.Add(new TelemetryDiagnosticItem(
                    TelemetryDiagnosticSeverity.Warning,
                    "Windows Firewall access missing (" + ProfileName(profile) + ")",
                    "IL2-SR-ClientRadio.exe is not allowed on the active "
                    + ProfileName(profile)
                    + " network profile. In Windows Defender Firewall, choose \"Allow an app through firewall\", enable IL2-SRS for both Private and Public networks, then restart IL-2 and SRS."));
            }

            return items;
        }

        private static WindowsFirewallPolicySnapshot ReadPolicy()
        {
            WindowsFirewallPolicySnapshot snapshot = new WindowsFirewallPolicySnapshot();
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                snapshot.Error = "Windows Firewall diagnostics are only available on Windows.";
                return snapshot;
            }

            object policyObject = null;
            try
            {
                Type policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
                if (policyType == null)
                {
                    snapshot.Error = "The Windows Firewall policy service is unavailable.";
                    return snapshot;
                }

                policyObject = Activator.CreateInstance(policyType);
                dynamic policy = policyObject;
                snapshot.ActiveProfiles = (WindowsFirewallProfile)(int)policy.CurrentProfileTypes;

                foreach (WindowsFirewallProfile profile in EnumerateProfiles(snapshot.ActiveProfiles))
                {
                    int profileValue = (int)profile;
                    if ((bool)policy.FirewallEnabled[profileValue])
                    {
                        snapshot.EnabledProfiles |= profile;
                    }

                    if ((int)policy.DefaultInboundAction[profileValue] == FirewallActionAllow)
                    {
                        snapshot.DefaultInboundAllowProfiles |= profile;
                    }
                }

                foreach (dynamic rule in policy.Rules)
                {
                    try
                    {
                        snapshot.Rules.Add(new WindowsFirewallRuleSnapshot
                        {
                            Enabled = (bool)rule.Enabled,
                            Allow = (int)rule.Action == FirewallActionAllow,
                            Inbound = (int)rule.Direction == FirewallDirectionInbound,
                            Profiles = (WindowsFirewallProfile)(int)rule.Profiles,
                            Protocol = (int)rule.Protocol,
                            ApplicationPath = Convert.ToString(rule.ApplicationName),
                            LocalPorts = Convert.ToString(rule.LocalPorts)
                        });
                    }
                    catch
                    {
                        // A malformed or policy-managed rule should not prevent the remaining rules being checked.
                    }
                }

                snapshot.Available = true;
            }
            catch (Exception ex)
            {
                snapshot.Error = "SRS could not inspect Windows Firewall: " + ex.Message;
            }
            finally
            {
                if (policyObject != null && Marshal.IsComObject(policyObject))
                {
                    Marshal.FinalReleaseComObject(policyObject);
                }
            }

            return snapshot;
        }

        private static bool RuleApplies(
            WindowsFirewallRuleSnapshot rule,
            WindowsFirewallProfile profile,
            string executablePath,
            int telemetryPort)
        {
            if (rule == null
                || !rule.Enabled
                || !rule.Inbound
                || (rule.Profiles & profile) == 0
                || (rule.Protocol != ProtocolAny && rule.Protocol != ProtocolUdp))
            {
                return false;
            }

            bool applicationMatches = PathsEqual(rule.ApplicationPath, executablePath);
            bool hasApplication = !string.IsNullOrWhiteSpace(rule.ApplicationPath);
            bool portMatches = !string.IsNullOrWhiteSpace(rule.LocalPorts)
                               && PortListContains(rule.LocalPorts, telemetryPort);

            return applicationMatches || (!hasApplication && portMatches);
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            try
            {
                string normalizedLeft = Path.GetFullPath(
                    Environment.ExpandEnvironmentVariables(left.Trim().Trim('"')));
                string normalizedRight = Path.GetFullPath(
                    Environment.ExpandEnvironmentVariables(right.Trim().Trim('"')));
                return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left.Trim().Trim('"'), right.Trim().Trim('"'), StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool PortListContains(string localPorts, int telemetryPort)
        {
            if (string.IsNullOrWhiteSpace(localPorts) || localPorts.Trim() == "*")
            {
                return true;
            }

            foreach (string entry in localPorts.Split(','))
            {
                string value = entry.Trim();
                int singlePort;
                if (int.TryParse(value, out singlePort) && singlePort == telemetryPort)
                {
                    return true;
                }

                string[] range = value.Split('-');
                int first;
                int last;
                if (range.Length == 2
                    && int.TryParse(range[0].Trim(), out first)
                    && int.TryParse(range[1].Trim(), out last)
                    && telemetryPort >= first
                    && telemetryPort <= last)
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<WindowsFirewallProfile> EnumerateProfiles(WindowsFirewallProfile profiles)
        {
            WindowsFirewallProfile[] knownProfiles =
            {
                WindowsFirewallProfile.Domain,
                WindowsFirewallProfile.Private,
                WindowsFirewallProfile.Public
            };

            return knownProfiles.Where(profile => (profiles & profile) != 0);
        }

        private static string ProfileName(WindowsFirewallProfile profile)
        {
            return profile.ToString();
        }
    }
}
