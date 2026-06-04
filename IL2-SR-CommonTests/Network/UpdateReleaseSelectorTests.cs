using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Network
{
    [TestClass]
    public class UpdateReleaseSelectorTests
    {
        [TestMethod]
        public void StableClientWithBetaDisabledIgnoresPrereleases()
        {
            var current = Current("1.0.4.3", "1.0.4.3");
            var releases = Releases(
                Stable("v1.0.4.3"),
                Beta("v1.0.4.5-beta.1"));

            var selected = UpdateReleaseSelector.SelectClientUpdate(releases, current, false);

            Assert.IsNull(selected);
        }

        [TestMethod]
        public void StableClientWithBetaEnabledDetectsLatestBeta()
        {
            var current = Current("1.0.4.3", "1.0.4.3");
            var releases = Releases(
                Stable("v1.0.4.3"),
                Beta("v1.0.4.5-beta.1"),
                Beta("v1.0.4.5-beta.2"));

            var selected = UpdateReleaseSelector.SelectClientUpdate(releases, current, true);

            Assert.AreEqual("v1.0.4.5-beta.2", selected.TagName);
            Assert.IsTrue(selected.IsPrerelease);
        }

        [TestMethod]
        public void BetaClientDetectsNewerBeta()
        {
            var current = Current("1.0.4.5", "1.0.4.5-beta.1");
            var releases = Releases(
                Stable("v1.0.4.3"),
                Beta("v1.0.4.5-beta.1"),
                Beta("v1.0.4.5-beta.2"));

            var selected = UpdateReleaseSelector.SelectClientUpdate(releases, current, true);

            Assert.AreEqual("v1.0.4.5-beta.2", selected.TagName);
        }

        [TestMethod]
        public void LatestBetaClientHasNoUpdate()
        {
            var current = Current("1.0.4.5", "1.0.4.5-beta.2");
            var releases = Releases(
                Stable("v1.0.4.3"),
                Beta("v1.0.4.5-beta.1"),
                Beta("v1.0.4.5-beta.2"));

            var selected = UpdateReleaseSelector.SelectClientUpdate(releases, current, true);

            Assert.IsNull(selected);
        }

        [TestMethod]
        public void StableReleaseWithSameVersionSupersedesBeta()
        {
            var current = Current("1.0.4.5", "1.0.4.5-beta.2");
            var releases = Releases(
                Stable("v1.0.4.5"),
                Beta("v1.0.4.5-beta.2"));

            var selected = UpdateReleaseSelector.SelectClientUpdate(releases, current, true);

            Assert.AreEqual("v1.0.4.5", selected.TagName);
            Assert.IsFalse(selected.IsPrerelease);
        }

        [TestMethod]
        public void ExactTagDownloadSelectsRequestedRelease()
        {
            var current = Current("1.0.4.5", "1.0.4.5-beta.1");
            var releases = Releases(
                Stable("v1.0.4.3"),
                Beta("v1.0.4.5-beta.1"),
                Beta("v1.0.4.5-beta.2"));

            var selected = UpdateReleaseSelector.SelectAutoUpdaterDownload(
                releases,
                current,
                false,
                "v1.0.4.5-beta.2");

            Assert.AreEqual("v1.0.4.5-beta.2", selected.TagName);
            Assert.AreEqual("https://downloads/1.0.4.5-beta.2.zip", selected.AssetDownloadUrl);
        }

        [TestMethod]
        public void DirectStableUpdaterDownloadsLatestStableOnly()
        {
            var current = Current("1.0.4.2", "1.0.4.2");
            var releases = Releases(
                Stable("v1.0.4.3"),
                Beta("v1.0.4.5-beta.1"));

            var selected = UpdateReleaseSelector.SelectAutoUpdaterDownload(
                releases,
                current,
                false,
                null);

            Assert.AreEqual("v1.0.4.3", selected.TagName);
            Assert.IsFalse(selected.IsPrerelease);
        }

        [TestMethod]
        public void DirectBetaUpdaterDownloadsLatestBeta()
        {
            var current = Current("1.0.4.5", "1.0.4.5-beta.1");
            var releases = Releases(
                Stable("v1.0.4.3"),
                Beta("v1.0.4.5-beta.1"),
                Beta("v1.0.4.5-beta.2"));

            var selected = UpdateReleaseSelector.SelectAutoUpdaterDownload(
                releases,
                current,
                false,
                null);

            Assert.AreEqual("v1.0.4.5-beta.2", selected.TagName);
        }

        [TestMethod]
        public void DirectLatestBetaUpdaterHasNoUpdate()
        {
            var current = Current("1.0.4.5", "1.0.4.5-beta.2");
            var releases = Releases(
                Stable("v1.0.4.3"),
                Beta("v1.0.4.5-beta.1"),
                Beta("v1.0.4.5-beta.2"));

            var selected = UpdateReleaseSelector.SelectAutoUpdaterDownload(
                releases,
                current,
                false,
                null);

            Assert.IsNull(selected);
        }

        [TestMethod]
        public void InvalidExactTagFallsBackSafely()
        {
            var current = Current("1.0.4.2", "1.0.4.2");
            var releases = Releases(
                Stable("v1.0.4.3"),
                Beta("v1.0.4.5-beta.1"));

            var selected = UpdateReleaseSelector.SelectAutoUpdaterDownload(
                releases,
                current,
                false,
                "v9.9.9.9-missing");

            Assert.AreEqual("v1.0.4.3", selected.TagName);
        }

        [TestMethod]
        public void NumericPrereleaseIdentifiersAreComparedNumerically()
        {
            var current = Current("1.0.4.5", "1.0.4.5-beta.2");
            var releases = Releases(
                Beta("v1.0.4.5-beta.2"),
                Beta("v1.0.4.5-beta.10"));

            var selected = UpdateReleaseSelector.SelectAutoUpdaterDownload(
                releases,
                current,
                true,
                null);

            Assert.AreEqual("v1.0.4.5-beta.10", selected.TagName);
        }

        [TestMethod]
        public void ReleasesWithoutZipAssetAreIgnored()
        {
            var current = Current("1.0.4.2", "1.0.4.2");
            var release = Stable("v1.0.4.3");
            release.Assets.Clear();

            var selected = UpdateReleaseSelector.SelectAutoUpdaterDownload(
                Releases(release),
                current,
                false,
                null);

            Assert.IsNull(selected);
        }

        private static UpdateReleaseInfo Current(string version, string tag)
        {
            return UpdateReleaseSelector.CreateCurrentReleaseInfo(version, tag);
        }

        private static List<UpdateReleaseCandidate> Releases(params UpdateReleaseCandidate[] releases)
        {
            return new List<UpdateReleaseCandidate>(releases);
        }

        private static UpdateReleaseCandidate Stable(string tag)
        {
            return Release(tag, false);
        }

        private static UpdateReleaseCandidate Beta(string tag)
        {
            return Release(tag, true);
        }

        private static UpdateReleaseCandidate Release(string tag, bool prerelease)
        {
            return new UpdateReleaseCandidate
            {
                TagName = tag,
                HtmlUrl = "https://github/releases/" + tag,
                IsDraft = false,
                IsPrerelease = prerelease,
                Assets = new List<UpdateReleaseAsset>
                {
                    new UpdateReleaseAsset
                    {
                        Name = "IL2-SimpleRadioStandalone-" + tag.TrimStart('v') + ".zip",
                        BrowserDownloadUrl = "https://downloads/" + tag.TrimStart('v') + ".zip"
                    }
                }
            };
        }
    }
}
