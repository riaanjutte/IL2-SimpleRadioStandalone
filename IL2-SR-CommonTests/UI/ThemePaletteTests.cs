using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Themes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class ThemePaletteTests
    {
        private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        [TestMethod]
        public void EveryPaletteDefinesTheSameKeysAsTheDefaultPalette()
        {
            // Windows use these keys through DynamicResource, which shows nothing (rather than failing)
            // when a palette leaves one out.
            var themes = FindThemesDirectory();
            var expected = ReadKeys(Path.Combine(themes, "MilitaryPalette.xaml"));
            var palettes = Directory.GetFiles(themes, "MilitaryPalette*.xaml");

            Assert.IsTrue(palettes.Any(path => path.EndsWith("MilitaryPaletteClassic.xaml", StringComparison.Ordinal)));
            foreach (var palette in palettes)
            {
                var keys = ReadKeys(palette);
                var missing = expected.Except(keys).ToList();
                var extra = keys.Except(expected).ToList();
                Assert.IsTrue(missing.Count == 0 && extra.Count == 0,
                    $"{Path.GetFileName(palette)}: missing [{string.Join(", ", missing)}], extra [{string.Join(", ", extra)}]");
            }
        }

        [TestMethod]
        public void ClassicThemeNameIsRecognisedWhateverItsCase()
        {
            Assert.IsTrue(UiTheme.IsClassic("Classic"));
            Assert.IsTrue(UiTheme.IsClassic(" classic "));
            // A hand-edited global.cfg can quote it; the palette loader already ignores the quotes.
            Assert.IsTrue(UiTheme.IsClassic("\"Classic\""));
            Assert.IsFalse(UiTheme.IsClassic("Bakelite"));
            Assert.IsFalse(UiTheme.IsClassic(null));
        }

        private static HashSet<string> ReadKeys(string path)
        {
            return new HashSet<string>(XDocument.Load(path)
                .Root
                .Elements()
                .Select(element => (string)element.Attribute(XamlNamespace + "Key"))
                .Where(key => key != null));
        }

        private static string FindThemesDirectory()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                var themes = Path.Combine(directory.FullName, "IL2-SR-Client", "Themes");
                if (Directory.Exists(themes))
                {
                    return themes;
                }

                directory = directory.Parent;
            }

            Assert.Inconclusive("IL2-SR-Client/Themes not found above the test output folder.");
            return null;
        }
    }
}
