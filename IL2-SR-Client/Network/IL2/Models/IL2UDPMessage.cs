using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2.Models
{
   
    public abstract class IL2UDPMessage
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static long _lastMalformedPacketLogTicks;

        public enum MessageType
        {
            SRV_ADDR = 10,
            SRV_TITLE = 11,
            SRS_ADDRESS = 12,
            CLIENT_DATA = 13,
            CTRL_DATA = 14,
        }

        public static List<IL2UDPMessage> Process(byte[] message)
        {
            Logger.Debug( "UDP Data IL2");

            var list = new List<IL2UDPMessage>();

            if (message == null || message.Length < 11)
            {
                LogMalformedPacket(message == null ? 0 : message.Length, "packet is shorter than the IL-2 telemetry header");
                return list;
            }

            Stream stream = new MemoryStream(message);

            if (!TrySkip(stream, 10, message.Length, "IL-2 telemetry header"))
            {
                return list;
            }

            //number of indicator structs
            int indicatorCount;
            if (!TryReadByte(stream, message.Length, "indicator count", out indicatorCount))
            {
                return list;
            }

            for (int i = 0; i < indicatorCount; i++)
            {
                if (!TrySkip(stream, 2, message.Length, "indicator header"))
                {
                    return list;
                }

                //indicator count
                int indicators;
                if (!TryReadByte(stream, message.Length, "indicator value count", out indicators))
                {
                    return list;
                }

                if (!TrySkip(stream, 4 * indicators, message.Length, "indicator values"))
                {
                    return list;
                }
            }

            //skip to event offset
            int eventCount;
            if (!TryReadByte(stream, message.Length, "event count", out eventCount))
            {
                return list;
            }
           
            for (int i =0; i < eventCount; i++)
            {
                int part1;
                int part2;
                int eventSize;
                if (!TryReadByte(stream, message.Length, "event message type byte 1", out part1) ||
                    !TryReadByte(stream, message.Length, "event message type byte 2", out part2) ||
                    !TryReadByte(stream, message.Length, "event payload size", out eventSize))
                {
                    return list;
                }

                int msgTypeInt = BitConverter.ToUInt16(new[] {(byte)part1, (byte)part2}, 0);
                Logger.Debug($"UDP DATA {msgTypeInt}");

                var payloadOffset = (int)stream.Position;
                if (!HasBytes(stream, eventSize, message.Length))
                {
                    LogMalformedPacket(message.Length, $"event payload for type {msgTypeInt} declares {eventSize} byte(s), only {message.Length - payloadOffset} available");
                    return list;
                }
                
                try
                {
                    
                    MessageType msgType = (MessageType)msgTypeInt;

                    // Type float corresponds to float IEEE 754 floating point type;
                    // Type DWORD corresponds to LSB unsigned integer(4 bytes)
                    // Type WORD corresponds to LSB unsigned short integer(2 bytes)
                    // Type BYTE corresponds to LSB unsigned char value(1 byte)
                    // Type STRING consists of sequence: String Length(1 byte), following string ASCII data
                    switch (msgType)
                    {
                        case MessageType.SRV_ADDR:
                            if (!IsValidStringPayload(message, payloadOffset, eventSize, msgType))
                            {
                                break;
                            }
                            list.Add(new ServerAddressMessage(message, (int)stream.Position,(int)eventSize));
                            break;
                        case MessageType.SRV_TITLE:
                            if (!IsValidStringPayload(message, payloadOffset, eventSize, msgType))
                            {
                                break;
                            }
                            list.Add(new ServerTitleMessage(message, (int)stream.Position, (int)eventSize));
                            break;
                        case MessageType.SRS_ADDRESS:
                            if (!IsValidStringPayload(message, payloadOffset, eventSize, msgType))
                            {
                                break;
                            }
                            list.Add(new SRSAddressMessage(message, (int)stream.Position, (int)eventSize));
                            break;
                        case MessageType.CLIENT_DATA:
                            if (!HasEventPayloadSize(eventSize, 40, msgType))
                            {
                                break;
                            }
                            list.Add(new ClientDataMessage(message, (int)stream.Position));
                            break;
                        case MessageType.CTRL_DATA:
                            if (!HasEventPayloadSize(eventSize, 6, msgType))
                            {
                                break;
                            }
                            list.Add(new ControlDataMessage(message,(int)stream.Position));
                            break;
                        default:
                            break;

                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex,"Error processing IL2 Data");
                }

                if (!TrySkip(stream, eventSize, message.Length, $"event payload for type {msgTypeInt}"))
                {
                    return list;
                }

            }

            stream.Close();

            return list;
        }

        private static bool TryReadByte(Stream stream, int messageLength, string fieldName, out int value)
        {
            value = stream.ReadByte();
            if (value >= 0)
            {
                return true;
            }

            LogMalformedPacket(messageLength, $"missing {fieldName}");
            return false;
        }

        private static bool TrySkip(Stream stream, int bytes, int messageLength, string fieldName)
        {
            if (!HasBytes(stream, bytes, messageLength))
            {
                LogMalformedPacket(messageLength, $"cannot skip {bytes} byte(s) for {fieldName}; only {messageLength - stream.Position} available");
                return false;
            }

            stream.Seek(bytes, SeekOrigin.Current);
            return true;
        }

        private static bool HasBytes(Stream stream, int bytes, int messageLength)
        {
            return bytes >= 0 &&
                   stream.Position <= messageLength &&
                   bytes <= messageLength - stream.Position;
        }

        private static bool HasEventPayloadSize(int eventSize, int requiredSize, MessageType msgType)
        {
            if (eventSize >= requiredSize)
            {
                return true;
            }

            LogMalformedPacket(0, $"{msgType} event payload declares {eventSize} byte(s), requires at least {requiredSize}");
            return false;
        }

        private static bool IsValidStringPayload(byte[] message, int offset, int eventSize, MessageType msgType)
        {
            if (eventSize < 1 || offset >= message.Length)
            {
                LogMalformedPacket(message.Length, $"{msgType} string event has no length byte");
                return false;
            }

            var stringLength = message[offset];
            if (stringLength <= eventSize - 1 && offset + 1 + stringLength <= message.Length)
            {
                return true;
            }

            LogMalformedPacket(message.Length, $"{msgType} string length {stringLength} exceeds event payload size {eventSize}");
            return false;
        }

        private static void LogMalformedPacket(int messageLength, string reason)
        {
            var now = DateTime.UtcNow.Ticks;
            var lastLog = Interlocked.Read(ref _lastMalformedPacketLogTicks);
            if (new TimeSpan(now - lastLog).TotalSeconds < 30)
            {
                return;
            }

            Interlocked.Exchange(ref _lastMalformedPacketLogTicks, now);
            Logger.Warn($"Ignoring malformed IL-2 telemetry packet ({messageLength} byte(s)): {reason}");
        }

    }
}
