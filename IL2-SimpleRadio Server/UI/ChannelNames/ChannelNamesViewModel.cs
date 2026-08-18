using System.Collections.Generic;
using Caliburn.Micro;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Settings;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.UI.ChannelNames
{
    public sealed class ChannelNamesViewModel : Screen
    {
        private readonly IEventAggregator _eventAggregator;

        public ChannelNamesViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            DisplayName = "Channel Names";

            var configuredNames = ServerSettingsStore.Instance.GetChannelNames();
            for (var channel = 1; channel <= ChannelNameSettings.MaximumChannel; channel++)
            {
                configuredNames.TryGetValue(channel, out var name);
                Channels.Add(new ChannelNameRowViewModel(channel, name));
            }
        }

        public BindableCollection<ChannelNameRowViewModel> Channels { get; } =
            new BindableCollection<ChannelNameRowViewModel>();

        public void Save()
        {
            var channelNames = new Dictionary<int, string>();
            foreach (var channel in Channels)
            {
                var name = ChannelNameSettings.NormalizeName(channel.Name);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    channelNames[channel.Channel] = name;
                }
            }

            ServerSettingsStore.Instance.SetChannelNames(channelNames);
            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
            TryClose();
        }

        public void Cancel()
        {
            TryClose();
        }
    }
}
