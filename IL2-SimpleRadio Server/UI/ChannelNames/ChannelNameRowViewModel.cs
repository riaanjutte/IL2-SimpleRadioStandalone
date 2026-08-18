using Caliburn.Micro;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.UI.ChannelNames
{
    public sealed class ChannelNameRowViewModel : PropertyChangedBase
    {
        private string _name;

        public ChannelNameRowViewModel(int channel, string name)
        {
            Channel = channel;
            _name = name ?? string.Empty;
        }

        public int Channel { get; }

        public string Name
        {
            get => _name;
            set
            {
                if (value == _name)
                {
                    return;
                }

                _name = value;
                NotifyOfPropertyChange(() => Name);
            }
        }
    }
}
