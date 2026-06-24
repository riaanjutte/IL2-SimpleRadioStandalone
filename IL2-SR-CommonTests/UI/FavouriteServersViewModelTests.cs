using System.Collections.Generic;
using System.Linq;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Preferences;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Favourites;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DCS_SR_CommonTests.UI
{
    [TestClass]
    public class FavouriteServersViewModelTests
    {
        [TestMethod]
        public void NewAddressCommandAddsAndSavesAddress()
        {
            var store = new CapturingFavouriteServerStore();
            var viewModel = new FavouriteServersViewModel(store)
            {
                NewName = "Combat Box",
                NewAddress = "srs.combatbox.net"
            };

            viewModel.NewAddressCommand.Execute(null);

            Assert.AreEqual(1, viewModel.Addresses.Count);
            Assert.AreEqual("Combat Box", viewModel.Addresses[0].Name);
            Assert.AreEqual("srs.combatbox.net", viewModel.Addresses[0].Address);
            Assert.IsTrue(viewModel.Addresses[0].IsDefault);
            Assert.AreEqual(1, store.SaveCount);
            Assert.AreEqual("srs.combatbox.net", store.LastSaved.Single().Address);
        }

        [TestMethod]
        public void NewAddressCommandUsesAddressAsNameWhenNameIsBlank()
        {
            var viewModel = new FavouriteServersViewModel(new CapturingFavouriteServerStore())
            {
                NewName = " ",
                NewAddress = "127.0.0.1"
            };

            viewModel.NewAddressCommand.Execute(null);

            Assert.AreEqual("127.0.0.1", viewModel.Addresses[0].Name);
        }

        [TestMethod]
        public void NewAddressCommandIgnoresBlankAddress()
        {
            var store = new CapturingFavouriteServerStore();
            var viewModel = new FavouriteServersViewModel(store)
            {
                NewName = "Blank",
                NewAddress = " "
            };

            viewModel.NewAddressCommand.Execute(null);

            Assert.AreEqual(0, viewModel.Addresses.Count);
            Assert.AreEqual(0, store.SaveCount);
        }

        private class CapturingFavouriteServerStore : IFavouriteServerStore
        {
            public int SaveCount { get; private set; }
            public List<ServerAddress> LastSaved { get; private set; }

            public IEnumerable<ServerAddress> LoadFromStore()
            {
                return Enumerable.Empty<ServerAddress>();
            }

            public bool SaveToStore(IEnumerable<ServerAddress> addresses)
            {
                SaveCount++;
                LastSaved = addresses.ToList();
                return true;
            }
        }
    }
}
