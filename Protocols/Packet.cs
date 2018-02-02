using System;

namespace Smart3.Protocols
{
    // Packets are data containers that encapsulate all layers of data concerning a serial communication.

    /// <summary>
    /// Base abstract class for all packet types.
    /// </summary>
    internal abstract class Packet
    {
        /// <summary>
        /// All derived types must map their private data into serially transmittable sequence of bytes.
        /// </summary>
        /// <returns>Byte array of serial packet data.</returns>
        internal abstract byte[] GetBytes();
        /// <summary>
        /// Check if byte matches any control byte of this protocol.
        /// </summary>
        /// <param name="value">Byte to check.</param>
        /// <returns>True if control byte, false otherwise.</returns>
        internal static bool IsControlByte(byte value)
        {
            switch (value)
            {
                case (byte)ControlBytes.STX_Postamble:
                case (byte)ControlBytes.ETX_Terminator:
                case (byte)ControlBytes.EOT_Preamble:
                case (byte)ControlBytes.ENQ_Enquiry:
                case (byte)ControlBytes.ACK_Acknowledged:
                case (byte)ControlBytes.BEL_PaperOut:
                case (byte)ControlBytes.DLE_StatusRequest:
                case (byte)ControlBytes.DC1_StatusRequest:
                case (byte)ControlBytes.NAK_NotAcknowledged:
                case (byte)ControlBytes.SYN_Synchronize:
                case (byte)ControlBytes.CAN_OperationAborted:
                    return true;
                default:
                    break;
            }
            return false;
        }
    }

    /// <summary>
    /// Special broadcast packet type containing raw byte data in a special non-addressed format.
    /// </summary>
    internal abstract class BroadcastPacket : Packet
    {
        internal BroadcastPacket(params byte[] byteSequence)
        {
            if (byteSequence == null) throw new ArgumentNullException(nameof(byteSequence));
            if (byteSequence.Length == 0) throw new ArgumentException("Broadcast packet cannot be empty.", nameof(byteSequence));
        }
        protected byte[] packetData;
        internal override byte[] GetBytes()
        {
            return packetData;
        }
    }
    internal sealed class BroadcastPacket232 : BroadcastPacket
    {
        internal BroadcastPacket232(params byte[] byteSequence) : base(byteSequence)
        {
            byte parity = 0; // CRC for broadcast packets is calculated using addition instead of XOR.
            packetData = new byte[byteSequence.Length + 5];
            // Unaddressed format bytes: EOT, 0xC0 (null address), length, data bytes, STX, CRC, ETX.
            packetData[0] = (byte)MessageControlBytes.EOT_Preamble;
            packetData[1] = (byte)(packetData.Length + 0x28);
            parity = (byte)(packetData[0] + packetData[1]);
            int index;
            for (index = 0; index < byteSequence.Length; index++)
            {
                packetData[2 + index] = byteSequence[index];
                parity += byteSequence[index];
            }
            packetData[2 + index] = (byte)MessageControlBytes.STX_Postamble;
            parity += (byte)MessageControlBytes.STX_Postamble;
            parity &= 0x7f; // Set the most significant bit to zero using bit mask (01111111).
            parity += 0x28; // Add 0x28.
            packetData[3 + index] = parity; // CRC field.
            packetData[4 + index] = (byte)MessageControlBytes.ETX_Terminator;
        }
    }
    internal sealed class BroadcastPacket485 : BroadcastPacket
    {
        internal BroadcastPacket485(params byte[] byteSequence) : base(byteSequence)
        {
            byte parity = 0; // CRC for broadcast packets is calculated using addition instead of XOR.
            packetData = new byte[byteSequence.Length + 6];
            // Unaddressed format bytes: EOT, universal address, length, data bytes, STX, CRC, ETX.
            packetData[0] = (byte)MessageControlBytes.EOT_Preamble;
            packetData[1] = (byte)RS485Addresses.ALL;
            packetData[2] = (byte)(packetData.Length + 0x28);
            parity = (byte)(packetData[0] + packetData[1] + packetData[2]);
            int index;
            for (index = 0; index < byteSequence.Length; index++)
            {
                packetData[3 + index] = byteSequence[index];
                parity += byteSequence[index];
            }
            packetData[3 + index] = (byte)MessageControlBytes.STX_Postamble;
            parity += (byte)MessageControlBytes.STX_Postamble;
            parity &= 0x7f; // Set the most significant bit to zero using bit mask (01111111).
            parity += 0x28; // Add 0x28.
            packetData[4 + index] = parity; // CRC field.
            packetData[5 + index] = (byte)MessageControlBytes.ETX_Terminator;
        }
    }

    // Indicator packets carry one byte signals that are used in a lower level non-protocolled type of communication (e.g. acknowledging that protocolled packet has been received).

    /// <summary>
    /// Base abstract class for all indicator packet types.
    /// </summary>
    internal abstract class IndicatorPacket : Packet
    {
        /// <summary>
        /// Check if byte is a valid packet start indicator of <see cref="IndicatorPacket"/> non-protocolled types.
        /// </summary>
        /// <param name="value">Byte to check.</param>
        /// <returns>True if byte is a start indicator, false otherwise.</returns>
        internal static bool IsPreambleByte(byte value)
        {
            switch (value)
            {
                case (byte)ControlBytes.ENQ_Enquiry:
                case (byte)ControlBytes.ACK_Acknowledged:
                case (byte)ControlBytes.BEL_PaperOut:
                case (byte)ControlBytes.DLE_StatusRequest:
                case (byte)ControlBytes.DC1_StatusRequest:
                case (byte)ControlBytes.NAK_NotAcknowledged:
                case (byte)ControlBytes.SYN_Synchronize:
                case (byte)ControlBytes.CAN_OperationAborted:
                    return true;
                default:
                    break;
            }
            return false;
        }
    }
    /// <summary>
    /// Packet containing control signal data for RS-232 type of communication.
    /// </summary>
    internal class IndicatorPacket232 : IndicatorPacket
    {
        protected IndicatorControlBytes indicator;
        internal IndicatorControlBytes Indicator { get { return indicator; } }
        /// <summary>
        /// Create non-protocoled packet using a specified control byte indicator.
        /// </summary>
        /// <param name="indicator">Control byte indicator.</param>
        internal IndicatorPacket232(IndicatorControlBytes indicator)
        {
            this.indicator = indicator;
        }
        /// <summary>
        /// Maps packet data into serially transmittable sequence of bytes.
        /// </summary>
        /// <returns>Serially transmittable sequence of bytes.</returns>
        internal override byte[] GetBytes()
        {
            return new byte[] { (byte)indicator };
        }
    }
    /// <summary>
    /// Packet containing control signal data as well as the address of destined cash register for RS-485 type of communication.
    /// </summary>
    internal sealed class IndicatorPacket485 : IndicatorPacket232
    {
        private RS485Addresses address;
        internal RS485Addresses Address { get { return address; } }
        internal IndicatorPacket485(IndicatorControlBytes indicator, RS485Addresses address) : base(indicator)
        {
            this.address = address;
        }
        internal override byte[] GetBytes()
        {
            //System.Diagnostics.Debug.WriteLine($"Indicator {(byte)indicator}, Address {(byte)address}");
            return new byte[] { (byte)indicator, (byte)address, (byte)address };
        }
    }

    // Message packets are fully protocolled means they carry indicators, actual data and the extra validation data.

    /// <summary>
    /// Base abstract class for all message packet types.
    /// </summary>
    internal abstract class MessagePacket : Packet
    {
        /// <summary>
        /// Check if byte is a valid packet start indicator of <see cref="MessagePacket"/> protocolled types.
        /// </summary>
        /// <param name="value">Byte to check.</param>
        /// <returns>True if byte is a start indicator, false otherwise.</returns>
        internal static bool IsPreambleByte(byte value)
        {
            switch (value)
            {
                case (byte)ControlBytes.EOT_Preamble:
                    return true;
                default:
                    break;
            }
            return false;
        }
    }
    /// <summary>
    /// Packet containing message data.
    /// </summary>
    internal class MessagePacket232 : MessagePacket
    {
        // Protocoled packet extracted data.
        protected MessageData message;
        protected byte sequence = 0;
        protected byte crn = 0;
        internal MessageData Message { get { return message; } }
        internal byte Sequence { get { return sequence; } }
        internal byte CRN { get { return crn; } }

        /// <summary>
        /// Create protocoled packet using a specified message, sequence number and a cash register number.
        /// </summary>
        /// <param name="message">Message string containing only printable ASCII characters and no more than 200 characters long.</param>
        /// <param name="sequence">Sequence number. When replying to a message it must have the same sequence.</param>
        /// <param name="crn">Cash register number between 0 and 99 (inclusive).</param>
        internal MessagePacket232(MessageData message, byte sequence, byte crn)
        {
            // Check message argument.
            if (message == null) throw new ArgumentNullException(nameof(message));
            // Check cash register number argument.
            if (crn > 99) throw new ArgumentOutOfRangeException(nameof(crn), "Value exceeded allowable range: 0–99 inclusive.");
            // Store arguments.
            this.message = message;
            this.sequence = sequence;
            this.crn = crn;
        }
        internal override byte[] GetBytes()
        {
            // Begin forging the packet.
            int length = message.Length + 7; // Message length plus 7 mandatory fields.
            byte parity = 0; // CRC check byte.
            byte[] serialData = new byte[length];
            // Insert first 4 fields.
            serialData[0] = (byte)MessageControlBytes.EOT_Preamble; // Preamble.
            serialData[1] = (byte)(length + 0x28); // Packet length + 0x28.
            serialData[2] = (byte)(sequence % 0x60 + 0x20); // Packet sequence within range 0x20–0x7f inclusive.
            serialData[3] = (byte)(crn + 0x20); // Cash register number + 0x20.
            // Calculate parity for first 4 fields using XOR logic.
            parity = (byte)(serialData[0] ^ serialData[1] ^ serialData[2] ^ serialData[3]);
            // Insert message bytes and continue calculating parity.
            int messageIndex;
            byte messageByte;
            for (messageIndex = 0; messageIndex < message.Length; messageIndex++)
            {
                messageByte = (byte)message[messageIndex];
                serialData[4 + messageIndex] = messageByte;
                parity ^= messageByte;
            }
            // Insert postamble (last byte to be parity checked).
            serialData[4 + messageIndex] = (byte)MessageControlBytes.STX_Postamble; // Postamble.
            parity ^= (byte)MessageControlBytes.STX_Postamble;
            // Perform neccessary operations on a parity byte.
            parity &= 0x7f; // Set the most significant bit to zero using bit mask (01111111).
            parity += 0x28; // Add 0x28.
            // Insert parity and terminator.
            serialData[5 + messageIndex] = parity; // CRC field.
            serialData[6 + messageIndex] = (byte)MessageControlBytes.ETX_Terminator; // Terminator.
            return serialData;
        }
    }
    /// <summary>
    /// Packet containing message data as well as the address of destined cash register.
    /// </summary>
    internal sealed class MessagePacket485 : MessagePacket232
    {
        private RS485Addresses address;
        internal RS485Addresses Address { get { return address; } }
        internal MessagePacket485(MessageData message, byte sequence, byte crn, RS485Addresses address) : base(message, sequence, crn)
        {
            this.address = address;
        }
        internal override byte[] GetBytes()
        {
            int length = message.Length + 8;
            byte parity = 0;
            byte[] serialData = new byte[length];
            serialData[0] = (byte)MessageControlBytes.EOT_Preamble;
            serialData[1] = (byte)address;
            serialData[2] = (byte)(length + 0x28);
            serialData[3] = (byte)(sequence % 0x60 + 0x20);
            serialData[4] = (byte)(crn + 0x20);
            parity = (byte)(serialData[0] ^ serialData[1] ^ serialData[2] ^ serialData[3] ^ serialData[4]);
            int messageIndex;
            byte messageByte;
            for (messageIndex = 0; messageIndex < message.Length; messageIndex++)
            {
                messageByte = (byte)message[messageIndex];
                serialData[5 + messageIndex] = messageByte;
                parity ^= messageByte;
            }
            serialData[5 + messageIndex] = (byte)MessageControlBytes.STX_Postamble;
            parity ^= (byte)MessageControlBytes.STX_Postamble;
            parity &= 0x7f;
            parity += 0x28;
            serialData[6 + messageIndex] = parity;
            serialData[7 + messageIndex] = (byte)MessageControlBytes.ETX_Terminator;
            return serialData;
        }
    }
}