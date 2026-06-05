using System;
using System.Collections.Generic;
using System.Linq;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Network
{
    public static class UpdateReleaseSelector
    {
        public static UpdateReleaseInfo SelectClientUpdate(
            IEnumerable<UpdateReleaseCandidate> releases,
            UpdateReleaseInfo currentRelease,
            bool checkForBetaUpdates)
        {
            var releaseInfos = CreateReleaseInfos(releases, true).ToList();
            var latestStableRelease = releaseInfos
                .Where(release => !release.IsPrerelease)
                .Aggregate((UpdateReleaseInfo)null, GetNewerRelease);
            var latestBetaRelease = releaseInfos
                .Where(release => release.IsPrerelease)
                .Aggregate((UpdateReleaseInfo)null, GetNewerRelease);

            if (checkForBetaUpdates &&
                latestBetaRelease != null &&
                IsBetaUpdateAvailable(latestBetaRelease, latestStableRelease, currentRelease))
            {
                return latestBetaRelease;
            }

            if (IsReleaseNewerThanCurrent(latestStableRelease, currentRelease))
            {
                return latestStableRelease;
            }

            return null;
        }

        public static UpdateReleaseInfo SelectAutoUpdaterDownload(
            IEnumerable<UpdateReleaseCandidate> releases,
            UpdateReleaseInfo currentRelease,
            bool allowBeta,
            string targetTag)
        {
            if (!string.IsNullOrWhiteSpace(targetTag))
            {
                var targetRelease = CreateReleaseInfos(releases, true)
                    .FirstOrDefault(release => TagsEqual(release.TagName, targetTag));
                if (targetRelease != null)
                {
                    return targetRelease;
                }
            }

            var includePrereleases = allowBeta || (currentRelease != null && currentRelease.IsPrerelease);
            var latestRelease = CreateReleaseInfos(releases, includePrereleases)
                .Where(release => includePrereleases || !release.IsPrerelease)
                .Aggregate((UpdateReleaseInfo)null, GetNewerRelease);

            return IsReleaseNewerThanCurrent(latestRelease, currentRelease)
                ? latestRelease
                : null;
        }

        public static UpdateReleaseInfo CreateCurrentReleaseInfo(string versionValue, string releaseTag)
        {
            Version version;
            if (!TryParseReleaseVersion(releaseTag, out version))
            {
                version = Version.Parse(versionValue);
            }

            return new UpdateReleaseInfo
            {
                TagName = releaseTag,
                Version = version,
                DisplayVersion = GetDisplayVersion(releaseTag, version),
                IsPrerelease = !string.IsNullOrWhiteSpace(releaseTag) &&
                               releaseTag.IndexOf("-", StringComparison.Ordinal) >= 0
            };
        }

        public static bool IsReleaseNewerThanCurrent(UpdateReleaseInfo candidate, UpdateReleaseInfo current)
        {
            if (candidate == null)
            {
                return false;
            }

            if (current == null)
            {
                return true;
            }

            return CompareReleaseOrder(candidate, current) > 0;
        }

        public static bool TryParseReleaseVersion(string tagName, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return false;
            }

            var normalized = NormalizeTag(tagName);
            if (Version.TryParse(normalized, out version))
            {
                return true;
            }

            var suffixIndex = normalized.IndexOfAny(new[] {'-', '+'});
            if (suffixIndex > 0)
            {
                normalized = normalized.Substring(0, suffixIndex);
            }

            return Version.TryParse(normalized, out version);
        }

        public static string GetDisplayVersion(string tagName, Version version)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return version.ToString();
            }

            return NormalizeTag(tagName);
        }

        private static IEnumerable<UpdateReleaseInfo> CreateReleaseInfos(
            IEnumerable<UpdateReleaseCandidate> releases,
            bool includePrereleases)
        {
            if (releases == null)
            {
                yield break;
            }

            foreach (var release in releases)
            {
                UpdateReleaseInfo releaseInfo;
                if (TryCreateReleaseInfo(release, includePrereleases, out releaseInfo))
                {
                    yield return releaseInfo;
                }
            }
        }

        private static bool TryCreateReleaseInfo(
            UpdateReleaseCandidate release,
            bool includePrereleases,
            out UpdateReleaseInfo releaseInfo)
        {
            releaseInfo = null;

            if (release == null ||
                release.IsDraft ||
                (release.IsPrerelease && !includePrereleases))
            {
                return false;
            }

            Version releaseVersion;
            if (!TryParseReleaseVersion(release.TagName, out releaseVersion))
            {
                return false;
            }

            var asset = release.Assets?.FirstOrDefault(IsReleaseZipAsset);
            if (asset == null)
            {
                return false;
            }

            releaseInfo = new UpdateReleaseInfo
            {
                TagName = release.TagName,
                HtmlUrl = release.HtmlUrl,
                AssetName = asset.Name,
                AssetDownloadUrl = asset.BrowserDownloadUrl,
                Version = releaseVersion,
                DisplayVersion = GetDisplayVersion(release.TagName, releaseVersion),
                IsPrerelease = release.IsPrerelease
            };
            return true;
        }

        private static UpdateReleaseInfo GetNewerRelease(UpdateReleaseInfo current, UpdateReleaseInfo candidate)
        {
            if (current == null || CompareReleaseOrder(candidate, current) > 0)
            {
                return candidate;
            }

            return current;
        }

        private static bool IsBetaUpdateAvailable(
            UpdateReleaseInfo betaRelease,
            UpdateReleaseInfo stableRelease,
            UpdateReleaseInfo currentRelease)
        {
            return IsReleaseNewerThanCurrent(betaRelease, currentRelease) &&
                   (stableRelease == null ||
                    betaRelease.Version > stableRelease.Version ||
                    !IsReleaseNewerThanCurrent(stableRelease, currentRelease));
        }

        private static int CompareReleaseOrder(UpdateReleaseInfo left, UpdateReleaseInfo right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var versionCompare = left.Version.CompareTo(right.Version);
            if (versionCompare != 0)
            {
                return versionCompare;
            }

            if (left.IsPrerelease != right.IsPrerelease)
            {
                return left.IsPrerelease ? -1 : 1;
            }

            return ComparePrereleaseTags(left.TagName, right.TagName);
        }

        private static int ComparePrereleaseTags(string leftTag, string rightTag)
        {
            var leftPrerelease = GetPrereleasePart(leftTag);
            var rightPrerelease = GetPrereleasePart(rightTag);

            if (leftPrerelease == null && rightPrerelease == null)
            {
                return string.CompareOrdinal(NormalizeTag(leftTag), NormalizeTag(rightTag));
            }

            if (leftPrerelease == null)
            {
                return 1;
            }

            if (rightPrerelease == null)
            {
                return -1;
            }

            var leftParts = leftPrerelease.Split('.');
            var rightParts = rightPrerelease.Split('.');
            var length = Math.Max(leftParts.Length, rightParts.Length);

            for (var i = 0; i < length; i++)
            {
                if (i >= leftParts.Length)
                {
                    return -1;
                }

                if (i >= rightParts.Length)
                {
                    return 1;
                }

                int leftNumber;
                int rightNumber;
                var leftIsNumber = int.TryParse(leftParts[i], out leftNumber);
                var rightIsNumber = int.TryParse(rightParts[i], out rightNumber);

                if (leftIsNumber && rightIsNumber)
                {
                    var numericCompare = leftNumber.CompareTo(rightNumber);
                    if (numericCompare != 0)
                    {
                        return numericCompare;
                    }

                    continue;
                }

                if (leftIsNumber != rightIsNumber)
                {
                    return leftIsNumber ? -1 : 1;
                }

                var textCompare = string.CompareOrdinal(leftParts[i], rightParts[i]);
                if (textCompare != 0)
                {
                    return textCompare;
                }
            }

            return 0;
        }

        private static bool IsReleaseZipAsset(UpdateReleaseAsset asset)
        {
            return asset != null &&
                   !string.IsNullOrWhiteSpace(asset.Name) &&
                   !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl) &&
                   asset.Name.StartsWith("IL2-SimpleRadioStandalone", StringComparison.OrdinalIgnoreCase) &&
                   asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TagsEqual(string left, string right)
        {
            return string.Equals(NormalizeTag(left), NormalizeTag(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeTag(string tagName)
        {
            return (tagName ?? string.Empty).Trim().TrimStart('v', 'V');
        }

        private static string GetPrereleasePart(string tagName)
        {
            var normalized = NormalizeTag(tagName);
            var suffixIndex = normalized.IndexOf('-');
            if (suffixIndex < 0 || suffixIndex + 1 >= normalized.Length)
            {
                return null;
            }

            var buildIndex = normalized.IndexOf('+', suffixIndex + 1);
            return buildIndex > suffixIndex
                ? normalized.Substring(suffixIndex + 1, buildIndex - suffixIndex - 1)
                : normalized.Substring(suffixIndex + 1);
        }
    }

    public class UpdateReleaseCandidate
    {
        public string TagName { get; set; }
        public string HtmlUrl { get; set; }
        public bool IsDraft { get; set; }
        public bool IsPrerelease { get; set; }
        public List<UpdateReleaseAsset> Assets { get; set; } = new List<UpdateReleaseAsset>();
    }

    public class UpdateReleaseAsset
    {
        public string Name { get; set; }
        public string BrowserDownloadUrl { get; set; }
    }

    public class UpdateReleaseInfo
    {
        public string TagName { get; set; }
        public string HtmlUrl { get; set; }
        public string AssetName { get; set; }
        public string AssetDownloadUrl { get; set; }
        public Version Version { get; set; }
        public string DisplayVersion { get; set; }
        public bool IsPrerelease { get; set; }
    }
}
