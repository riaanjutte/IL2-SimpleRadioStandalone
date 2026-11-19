using Ciribob.IL2.SimpleRadio.Standalone.Client.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class UiScaleTests
    {
        [TestMethod]
        public void TheStoredPercentageBecomesAScale()
        {
            Assert.AreEqual(1.0, UiScale.Parse("100"));
            Assert.AreEqual(1.1, UiScale.Parse("110"));
            Assert.AreEqual(1.3, UiScale.Parse("130"));
        }

        [TestMethod]
        public void AHandEditedValueIsClampedAndSnappedToAStep()
        {
            Assert.AreEqual(1.0, UiScale.Parse("50"));
            Assert.AreEqual(1.3, UiScale.Parse("500"));
            Assert.AreEqual(1.2, UiScale.Parse("117"));
            Assert.AreEqual(1.0, UiScale.Parse("-130"));
        }

        [TestMethod]
        public void AnUnreadableValueIsTheNormalSize()
        {
            Assert.AreEqual(1.0, UiScale.Parse(null));
            Assert.AreEqual(1.0, UiScale.Parse(""));
            Assert.AreEqual(1.0, UiScale.Parse("big"));
            Assert.AreEqual(1.0, UiScale.Parse("NaN"));
            Assert.AreEqual(1.3, UiScale.Parse("Infinity"));
        }
    }
}
