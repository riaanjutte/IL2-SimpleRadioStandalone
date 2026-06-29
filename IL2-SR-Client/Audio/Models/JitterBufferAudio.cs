namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Audio
{
    public class JitterBufferAudio
    {
        public byte[] Audio { get; set; }

        public ulong PacketNumber { get; set; }

        public int RadioId { get; set; }

        public string ClientGuid { get; set; }
    }
}
