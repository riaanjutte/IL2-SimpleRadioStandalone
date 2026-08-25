using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers
{
    public static class AirfieldCodeLookup
    {
        private static readonly Regex CoalitionPrefix = new Regex(
            @"^(?:axis|allied|allies|german|soviet)\s+(?:af|airfield)\s+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex HistoricalPrefix = new Regex(
            @"^(?:[aby]\s*-?\s*\d+|b\d+)\s+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex OperationalSuffix = new Regex(
            @"(?:\s+(?:bsp|fsp|fwd|jfb|airfield)|\s+\d{4})+$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Lazy<LookupData> Data = new Lazy<LookupData>(LoadData);

        public static int KnownEntryCount => Data.Value.EntryCount;

        public static int KnownAliasCount => Data.Value.Aliases.Count;

        public static string GetCode(string airfieldName)
        {
            var normalized = Normalize(airfieldName);
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            string code;
            if (Data.Value.Aliases.TryGetValue(normalized, out code))
            {
                return code;
            }

            return normalized.Length >= 3
                ? normalized.Substring(0, 3)
                : normalized.PadRight(3, 'X');
        }

        internal static string Normalize(string airfieldName)
        {
            if (string.IsNullOrWhiteSpace(airfieldName))
            {
                return string.Empty;
            }

            var value = airfieldName.Trim();
            if (value.StartsWith("ef_", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(3);
            }

            value = value.Replace('_', ' ');
            value = CoalitionPrefix.Replace(value, string.Empty);
            value = HistoricalPrefix.Replace(value, string.Empty);
            value = OperationalSuffix.Replace(value, string.Empty);

            var decomposed = value.Normalize(NormalizationForm.FormD);
            var normalized = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (character == 'ß')
                {
                    normalized.Append("SS");
                }
                else if (char.IsLetterOrDigit(character))
                {
                    normalized.Append(char.ToUpperInvariant(character));
                }
            }

            return normalized.ToString();
        }

        private static LookupData LoadData()
        {
            var assembly = typeof(AirfieldCodeLookup).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .Single(name => name.EndsWith(".Data.AirfieldCodes.json", StringComparison.Ordinal));

            AirfieldCodeTable table;
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidDataException("The embedded airfield-code table could not be opened.");
                }

                using (var reader = new StreamReader(stream))
                {
                    table = JsonConvert.DeserializeObject<AirfieldCodeTable>(reader.ReadToEnd());
                }
            }

            if (table?.SchemaVersion != 1 || table.Entries == null || table.Entries.Count == 0)
            {
                throw new InvalidDataException("The embedded airfield-code table has an unsupported schema or is empty.");
            }

            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var mapCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in table.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Map) ||
                    !Regex.IsMatch(entry.Code ?? string.Empty, "^[A-Z]{3}$"))
                {
                    throw new InvalidDataException("The embedded airfield-code table contains an invalid map or code.");
                }

                if (!mapCodes.Add(entry.Map.Trim() + "\0" + entry.Code))
                {
                    throw new InvalidDataException(
                        $"Airfield code '{entry.Code}' is duplicated on map '{entry.Map}'.");
                }

                foreach (var name in entry.Names ?? Enumerable.Empty<string>())
                {
                    var normalized = Normalize(name);
                    if (normalized.Length == 0)
                    {
                        continue;
                    }

                    string existingCode;
                    if (aliases.TryGetValue(normalized, out existingCode) && existingCode != entry.Code)
                    {
                        throw new InvalidDataException(
                            $"Airfield alias '{name}' maps to both {existingCode} and {entry.Code}.");
                    }

                    aliases[normalized] = entry.Code;
                }
            }

            return new LookupData(table.Entries.Count, aliases);
        }

        private sealed class LookupData
        {
            public LookupData(int entryCount, IReadOnlyDictionary<string, string> aliases)
            {
                EntryCount = entryCount;
                Aliases = aliases;
            }

            public int EntryCount { get; }

            public IReadOnlyDictionary<string, string> Aliases { get; }
        }

        private sealed class AirfieldCodeTable
        {
            [JsonProperty("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonProperty("entries")]
            public List<AirfieldCodeEntry> Entries { get; set; }
        }

        private sealed class AirfieldCodeEntry
        {
            [JsonProperty("code")]
            public string Code { get; set; }

            [JsonProperty("map")]
            public string Map { get; set; }

            [JsonProperty("names")]
            public List<string> Names { get; set; }
        }
    }
}
