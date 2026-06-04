using System;
using System.Collections.Generic;
using System.Linq;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Network
{
    public class UpdaterArguments
    {
        public const string BetaFlag = "-beta";
        public const string TagFlag = "-tag";

        public bool Beta { get; set; }
        public string ReleaseTag { get; set; }

        public static string Build(bool beta, string releaseTag)
        {
            var arguments = new List<string>();

            if (beta)
            {
                arguments.Add(BetaFlag);
            }

            if (!string.IsNullOrWhiteSpace(releaseTag))
            {
                arguments.Add(TagFlag);
                arguments.Add(QuoteArgument(releaseTag.Trim()));
            }

            return string.Join(" ", arguments);
        }

        public static UpdaterArguments Parse(IEnumerable<string> args)
        {
            var parsed = new UpdaterArguments();
            var arguments = args == null
                ? new string[0]
                : args.Where(arg => arg != null).ToArray();

            for (var i = 0; i < arguments.Length; i++)
            {
                var arg = arguments[i].Trim();
                if (arg.Equals(BetaFlag, StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--beta", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/beta", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.Beta = true;
                    continue;
                }

                if (IsTagFlag(arg))
                {
                    if (i + 1 < arguments.Length)
                    {
                        var value = arguments[i + 1].Trim();
                        if (!string.IsNullOrWhiteSpace(value) && !IsOption(value))
                        {
                            parsed.ReleaseTag = value;
                            i++;
                        }
                    }

                    continue;
                }

                var tagValue = GetTagValue(arg);
                if (!string.IsNullOrWhiteSpace(tagValue))
                {
                    parsed.ReleaseTag = tagValue.Trim();
                }
            }

            return parsed;
        }

        private static bool IsTagFlag(string arg)
        {
            return arg.Equals(TagFlag, StringComparison.OrdinalIgnoreCase) ||
                   arg.Equals("--tag", StringComparison.OrdinalIgnoreCase) ||
                   arg.Equals("/tag", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetTagValue(string arg)
        {
            return GetValue(arg, TagFlag + "=") ??
                   GetValue(arg, "--tag=") ??
                   GetValue(arg, "/tag=");
        }

        private static string GetValue(string arg, string prefix)
        {
            return arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? arg.Substring(prefix.Length)
                : null;
        }

        private static bool IsOption(string value)
        {
            return value.StartsWith("-", StringComparison.Ordinal) ||
                   value.StartsWith("/", StringComparison.Ordinal);
        }

        private static string QuoteArgument(string argument)
        {
            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }
    }
}
