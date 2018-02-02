using System;
using System.ComponentModel;

namespace Smart3.Protocols
{
    internal abstract class PacketBuilder
    {
        /// <summary>
        /// Denotes how many bytes are expected by <see cref="Packetize(byte)"/> method.
        /// This won't show the exact number of bytes required to complete the packet until packet length indicator is received.
        /// </summary>
        internal abstract int BytesExpected { get; }
        /// <summary>
        /// Denotes how many bytes have been received and validated by <see cref="Packetize(byte)"/>.
        /// </summary>
        internal abstract int BytesReceived { get; }
        /// <summary>
        /// Denotes how many bytes have been discarded due to <see cref="Packetize(byte)"/> invalidation.
        /// </summary>
        internal abstract int BytesDiscarded { get; }
        /// <summary>
        /// Reference to the packet that has been built or null.
        /// </summary>
        internal abstract Packet Packet { get; }
        // Derived classes must implement their own state machine.
        internal abstract Packet Packetize(byte item);
    }
    /// <summary>
    /// Packet builder for RS-232 serial protocol.
    /// </summary>
    /// <remarks>
    /// First byte received indicates the packet type being built.
    /// 
    /// <see cref="MessagePacket232"/> type:
    /// Each message is composed of 8 fields.
    /// 1. Preamble EOT (1 byte).
    /// 2. Record Length (1 byte) calculated as the total number of bytes + 0x28.
    /// 3. Sequence Number (1 byte) incremented at every message within: 0x20 to 0x7F (included).
    ///    The reply message is valid only if it has the same sequence number.
    /// 4. Cash Register Number (1 byte) + 0x20.
    /// 5. Actual message (n bytes).
    /// 6. Postamble STX (1 byte).
    /// 7. Horizontal Parity (1 byte) obtained through the following operations:
    ///    XOR logic (bit by bit) of all record bytes, from preamble to postamble included;
    ///    zeroing of the most significant bit of XOR result;
    ///    addition of 0x28 to XOR result.
    /// 8. Terminator ETX (1 byte).
    /// 
    /// <see cref="IndicatorPacket232"/> type:
    /// Indicators have no fields and are exactly 1 byte long.
    /// Byte 1: Indicator control byte.
    /// </remarks>
    internal sealed class PacketBuilder232 : PacketBuilder
    {
        // Exact number of bytes assembling an indicator packet type. Indicator packets are always of less size than message packets.
        private const int defaultIndicatorBytes = 1;
        // Minimum number of bytes assembling a message packet type.
        private const int defaultMessageMinBytes = 7;
        // Maximum number of bytes assembling a message packet type.
        private const int defaultMessageMaxBytes = 215;
        // Base class members implementation.
        internal override int BytesExpected { get { return bytesExpected; } }
        internal override int BytesReceived { get { return bytesReceived; } }
        internal override int BytesDiscarded { get { return bytesDiscarded; } }
        internal override Packet Packet { get { return packet; } }
        // Property backing data.
        private int bytesExpected = defaultIndicatorBytes;
        private int bytesReceived = 0;
        private int bytesDiscarded = 0;
        private Packet packet;
        // Packetize state enumeration.
        private enum PacketizeStates { ReadPreamble, ReadLength, ReadSequence, ReadCRNumber, ReadMessage, ReadParity, TerminateReady, TerminateWait, Terminated }
        private PacketizeStates packetizeState = PacketizeStates.ReadPreamble;
        // Packetize session data.
        private string error;
        private byte sequence;
        private byte crn;
        private byte parity;
        private byte messageIndex;
        private byte messageCapacity;
        private byte[] messageData;
        /// <summary>
        /// Forge the packet byte-by-byte using a strict state validation.
        /// </summary>
        /// <param name="value">Byte to packetize.</param>
        /// <returns><see cref="PacketBuilder.Packet"/> when all bytes are received and validated, null otherwise.</returns>
        internal override Packet Packetize(byte value)
        {
            bytesReceived++;
            switch (packetizeState)
            {
                case PacketizeStates.ReadPreamble:
                    // Indicator preamble received.
                    if (IndicatorPacket.IsPreambleByte(value))
                    {
                        packet = new IndicatorPacket232((IndicatorControlBytes)value);
                        packetizeState = PacketizeStates.Terminated;
                        bytesExpected = 0;
                    }
                    // Message preamble received.
                    else if (MessagePacket.IsPreambleByte(value))
                    {
                        packetizeState = PacketizeStates.ReadLength;
                        bytesExpected = 1;
                    }
                    // Unexpected byte received.
                    else
                    {
                        DiscardByte();
                    }
                    break;
                case PacketizeStates.ReadLength:
                    // Received byte should indicate length in bytes + 0x28.
                    int length = value - 0x28;
                    if (length < defaultMessageMinBytes || length > defaultMessageMaxBytes)
                    {
                        DiscardByte(string.Format("Invalid length indicator value received: 0x{0:X2}.", value));
                        packetizeState = PacketizeStates.TerminateWait;
                        break;
                    }
                    parity = (byte)((int)MessageControlBytes.EOT_Preamble ^ value);
                    messageIndex = 0;
                    messageCapacity = (byte)(length - defaultMessageMinBytes);
                    messageData = new byte[messageCapacity];
                    packetizeState = PacketizeStates.ReadSequence;
                    bytesExpected = length - bytesReceived;
                    break;
                case PacketizeStates.ReadSequence:
                    if (value < 0x20 || value > 0x7f)
                    {
                        DiscardByte(string.Format("Invalid sequence indicator value received: 0x{0:X2}.", value));
                        packetizeState = PacketizeStates.TerminateWait;
                        bytesExpected--;
                        break;
                    }
                    sequence = (byte)(value - 0x20);
                    parity ^= value;
                    packetizeState = PacketizeStates.ReadCRNumber;
                    bytesExpected--;
                    break;
                case PacketizeStates.ReadCRNumber:
                    if (value < 0x20 || value > 0x83)
                    {
                        DiscardByte(string.Format("Invalid cash register number indicator value received: 0x{0:X2}.", value));
                        packetizeState = PacketizeStates.TerminateWait;
                        bytesExpected--;
                        break;
                    }
                    crn = (byte)(value - 0x20);
                    parity ^= value;
                    packetizeState = PacketizeStates.ReadMessage;
                    bytesExpected--;
                    break;
                case PacketizeStates.ReadMessage:
                    // Fill message bytes until calculated capacity is reached.
                    if (messageIndex < messageCapacity)
                    {
                        // Message must not contain control bytes.
                        if (Packet.IsControlByte(value))
                        {
                            DiscardByte(string.Format("Invalid message character value received at index {0}: 0x{1:X2}.", messageIndex, value));
                            packetizeState = PacketizeStates.TerminateWait;
                            bytesExpected--;
                            break;
                        }
                        messageData[messageIndex++] = value;
                        parity ^= value;
                        bytesExpected--;
                        break;
                    }
                    // Message has to be finalized with a STX postamble.
                    else if (value != (int)MessageControlBytes.STX_Postamble)
                    {
                        DiscardByte(string.Format("Invalid postamble indicator value received: 0x{0:X2}.", value));
                        packetizeState = PacketizeStates.TerminateWait;
                        bytesExpected--;
                        break;
                    }
                    parity ^= value;
                    packetizeState = PacketizeStates.ReadParity;
                    bytesExpected--;
                    break;
                case PacketizeStates.ReadParity:
                    // Perform additional operations on locally calculated parity value.
                    parity &= 0x7f; // Set the most significant bit to zero using bit mask (01111111).
                    parity += 0x28; // Add 0x28.
                    if (parity != value)
                    {
                        DiscardByte("Data parity check (CRC) failed.");
                        packetizeState = PacketizeStates.TerminateWait;
                        bytesExpected--;
                        break;
                    }
                    packetizeState = PacketizeStates.TerminateReady;
                    bytesExpected--;
                    break;
                case PacketizeStates.TerminateReady:
                    // Terminate the packet preemptively.
                    packetizeState = PacketizeStates.Terminated;
                    bytesExpected = 0;
                    // Throw the exception if last expected byte is not a terminator indicator.
                    if (value != (int)MessageControlBytes.ETX_Terminator)
                    {
                        DiscardByte(string.Format("Invalid terminator indicator value received: 0x{0:X2}.", value));
                        throw new PacketValidationException(error);
                    }
                    // When all states passed validation we create a packet.
                    MessageData message = new MessageData(messageData);
                    packet = new MessagePacket232(message, sequence, crn);
                    break;
                case PacketizeStates.TerminateWait:
                    // In case of packet invalidation wait for the terminator indicator to terminate the packet and throw the exception.
                    DiscardByte();
                    if (bytesExpected > 1) bytesExpected--;
                    if (value == (int)MessageControlBytes.ETX_Terminator)
                    {
                        packetizeState = PacketizeStates.Terminated;
                        bytesExpected = 0;
                        throw new PacketValidationException(error);
                    }
                    break;
                case PacketizeStates.Terminated:
                    DiscardByte();
                    break;
                default:
                    throw new InvalidEnumArgumentException();
            }
            return packet;
        }
        /// <summary>
        /// Discard received byte and optionally store the error message explaining the reason.
        /// </summary>
        /// <param name="message">Error message to be stored, if any.</param>
        private void DiscardByte(string message = null)
        {
            // Keep the old message if none was passed.
            error = message ?? error;
            bytesReceived--;
            bytesDiscarded++;
        }
    }
    /// <summary>
    /// Packet builder for RS-485 serial protocol.
    /// </summary>
    /// <remarks>
    /// First byte received indicates the packet type being built.
    /// 
    /// <see cref="MessagePacket485"/> type:
    /// Each message is composed of 9 fields.
    /// 1. Preamble EOT (1 byte).
    /// 2. Cash Register Address (1 byte) 0xA0 to 0xAF.
    /// 3. Record Length (1 byte) calculated as the total number of bytes + 0x28.
    /// 4. Sequence Number (1 byte) incremented at every message within: 0x20 to 0x7F (included).
    ///    The reply message is valid only if it has the same sequence number.
    /// 5. Cash Register Number (1 byte) + 0x20.
    /// 6. Actual message (n bytes).
    /// 7. Postamble STX (1 byte).
    /// 8. Horizontal Parity (1 byte) obtained through the following operations:
    ///    XOR logic (bit by bit) of all record bytes, from preamble to postamble included;
    ///    zeroing of the most significant bit of XOR result;
    ///    addition of 0x28 to XOR result.
    /// 9. Terminator ETX (1 byte).
    /// 
    /// <see cref="IndicatorPacket485"/> type:
    /// Indicators have no fields and are exactly 3 bytes long.
    /// Byte 1: Indicator control byte.
    /// Byte 2: Cash Register Address.
    /// Byte 3: Cash Register Address (duplicate address for serial error checking purposes).
    /// </remarks>
    internal sealed class PacketBuilder485 : PacketBuilder
    {
        // Exact number of bytes assembling an indicator packet type. Indicator packets are always of less size than message packets.
        private const int defaultIndicatorBytes = 3;
        // Minimum number of bytes assembling a message packet type.
        private const int defaultMessageMinBytes = 8;
        // Maximum number of bytes assembling a message packet type.
        private const int defaultMessageMaxBytes = 215;
        // Base class members implementation.
        internal override int BytesExpected { get { return bytesExpected; } }
        internal override int BytesReceived { get { return bytesReceived; } }
        internal override int BytesDiscarded { get { return bytesDiscarded; } }
        internal override Packet Packet { get { return packet; } }
        // Property backing data.
        private int bytesExpected = defaultIndicatorBytes;
        private int bytesReceived = 0;
        private int bytesDiscarded = 0;
        private Packet packet;
        // Packetize state enumeration.
        private enum PacketizeStates { ReadPreamble, IndicatorReadAddress, IndicatorTerminateReady, ReadAddress, ReadLength, ReadSequence, ReadCRNumber, ReadMessage, ReadParity, TerminateReady, TerminateWait, Terminated }
        private PacketizeStates packetizeState = PacketizeStates.ReadPreamble;
        // Packetize session data.
        private string error;
        private byte indicator;
        private byte address;
        private byte sequence;
        private byte crn;
        private byte parity;
        private byte messageIndex;
        private byte messageCapacity;
        private byte[] messageData;
        /// <summary>
        /// Forge the packet byte-by-byte using a strict state validation.
        /// </summary>
        /// <param name="value">Byte to packetize.</param>
        /// <returns><see cref="PacketBuilder.Packet"/> when all bytes are received and validated, null otherwise.</returns>
        internal override Packet Packetize(byte value)
        {
            bytesReceived++;
            switch (packetizeState)
            {
                case PacketizeStates.ReadPreamble:
                    // Indicator preamble received.
                    if (IndicatorPacket.IsPreambleByte(value))
                    {
                        indicator = value;
                        packetizeState = PacketizeStates.IndicatorReadAddress;
                        bytesExpected--;
                    }
                    // Message preamble received.
                    else if (MessagePacket.IsPreambleByte(value))
                    {
                        packetizeState = PacketizeStates.ReadAddress;
                        bytesExpected--;
                    }
                    // Unexpected byte received.
                    else
                    {
                        DiscardByte();
                    }
                    break;
                case PacketizeStates.IndicatorReadAddress:
                    // We still must receive the second address indicator so just discard this one and store the error message.
                    if (value < (int)RS485Addresses._01 || value > (int)RS485Addresses._16)
                    {
                        DiscardByte(string.Format("Invalid first address indicator value received: 0x{0:X2}.", value));
                    }
                    address = value;
                    packetizeState = PacketizeStates.IndicatorTerminateReady;
                    bytesExpected--;
                    break;
                case PacketizeStates.IndicatorTerminateReady:
                    // Terminate preemptively.
                    packetizeState = PacketizeStates.Terminated;
                    bytesExpected = 0;
                    // Previous address byte was discarded so discard this one too silently and throw the exception.
                    if (bytesReceived != defaultIndicatorBytes)
                    {
                        DiscardByte();
                        throw new PacketValidationException(error);
                    }
                    // Address mismatch.
                    if (value != address)
                    {
                        DiscardByte(string.Format("Invalid second address indicator value received: 0x{0:X2}. Expected value: 0x{1:X2}", value, (int)address));
                        throw new PacketValidationException(error);
                    }
                    packet = new IndicatorPacket485((IndicatorControlBytes)indicator, (RS485Addresses)address);
                    break;
                case PacketizeStates.ReadAddress:
                    if (value < (int)RS485Addresses._01 || value > (int)RS485Addresses._16)
                    {
                        DiscardByte(string.Format("Invalid address indicator value received: 0x{0:X2}.", value));
                        packetizeState = PacketizeStates.TerminateWait;
                        bytesExpected = 1;
                        break;
                    }
                    address = value;
                    parity = (byte)((int)MessageControlBytes.EOT_Preamble ^ value);
                    packetizeState = PacketizeStates.ReadLength;
                    bytesExpected--;
                    break;
                case PacketizeStates.ReadLength:
                    // Received byte should indicate length in bytes + 0x28.
                    int length = value - 0x28;
                    if (length < defaultMessageMinBytes || length > defaultMessageMaxBytes)
                    {
                        DiscardByte(string.Format("Invalid length indicator value received: 0x{0:X2}.", value));
                        packetizeState = PacketizeStates.TerminateWait;
                        break;
                    }
                    parity ^= value;
                    messageIndex = 0;
                    messageCapacity = (byte)(length - defaultMessageMinBytes);
                    messageData = new byte[messageCapacity];
                    packetizeState = PacketizeStates.ReadSequence;
                    bytesExpected = length - bytesReceived;
                    break;
                case PacketizeStates.ReadSequence:
                    if (value < 0x20 || value > 0x7f)
                    {
                        DiscardByte(string.Format("Invalid sequence indicator value received: 0x{0:X2}.", value));
                        packetizeState = PacketizeStates.TerminateWait;
                        bytesExpected--;
                        break;
                    }
                    sequence = (byte)(value - 0x20);
                    parity ^= value;
                    packetizeState = PacketizeStates.ReadCRNumber;
                    bytesExpected--;
                    break;
                case PacketizeStates.ReadCRNumber:
                    if (value < 0x20 || value > 0x83)
                    {
                        DiscardByte(string.Format("Invalid cash register number indicator value received: 0x{0:X2}.", value));
                        packetizeState = PacketizeStates.TerminateWait;
                        bytesExpected--;
                        break;
                    }
                    crn = (byte)(value - 0x20);
                    parity ^= value;
                    packetizeState = PacketizeStates.ReadMessage;
                    bytesExpected--;
                    break;
                case PacketizeStates.ReadMessage:
                    // Fill message bytes until calculated capacity is reached.
                    if (messageIndex < messageCapacity)
                    {
                        // Message must not contain control bytes.
                        if (Packet.IsControlByte(value))
                        {
                            DiscardByte(string.Format("Invalid message character value received at index {0}: 0x{1:X2}.", messageIndex, value));
                            packetizeState = PacketizeStates.TerminateWait;
                            bytesExpected--;
                            break;
                        }
                        messageData[messageIndex++] = value;
                        parity ^= value;
                        bytesExpected--;
                        break;
                    }
                    // Message has to be finalized with a STX postamble.
                    else if (value != (int)MessageControlBytes.STX_Postamble)
                    {
                        DiscardByte(string.Format("Invalid postamble indicator value received: 0x{0:X2}.", value));
                        packetizeState = PacketizeStates.TerminateWait;
                        bytesExpected--;
                        break;
                    }
                    parity ^= value;
                    packetizeState = PacketizeStates.ReadParity;
                    bytesExpected--;
                    break;
                case PacketizeStates.ReadParity:
                    // Perform additional operations on locally calculated parity value.
                    parity &= 0x7f; // Set the most significant bit to zero using bit mask (01111111).
                    parity += 0x28; // Add 0x28.
                    // Compare parity.
                    if (parity != value)
                    {
                        DiscardByte("Data parity check (CRC) failed.");
                        packetizeState = PacketizeStates.TerminateWait;
                        bytesExpected--;
                        break;
                    }
                    packetizeState = PacketizeStates.TerminateReady;
                    bytesExpected--;
                    break;
                case PacketizeStates.TerminateReady:
                    // Terminate the packet preemptively.
                    packetizeState = PacketizeStates.Terminated;
                    bytesExpected = 0;
                    // Throw the exception if last expected byte is not a terminator indicator.
                    if (value != (int)MessageControlBytes.ETX_Terminator)
                    {
                        DiscardByte(string.Format("Invalid terminator indicator value received: 0x{0:X2}.", value));
                        throw new PacketValidationException(error);
                    }
                    // When all states passed validation we create a packet.
                    MessageData message = new MessageData(messageData);
                    packet = new MessagePacket485(message, sequence, crn, (RS485Addresses)address);
                    break;
                case PacketizeStates.TerminateWait:
                    // In case of packet invalidation wait for the terminator indicator to terminate the packet and throw the exception.
                    DiscardByte();
                    if (bytesExpected > 1) bytesExpected--;
                    if (value == (int)MessageControlBytes.ETX_Terminator)
                    {
                        packetizeState = PacketizeStates.Terminated;
                        bytesExpected = 0;
                        throw new PacketValidationException(error);
                    }
                    break;
                case PacketizeStates.Terminated:
                    DiscardByte();
                    break;
                default:
                    throw new InvalidEnumArgumentException();
            }
            return packet;
        }

        /// <summary>
        /// Discard received byte and optionally store the error message explaining the reason.
        /// </summary>
        /// <param name="message">Error message to be stored, if any.</param>
        private void DiscardByte(string message = null)
        {
            // Keep the old message if none was passed.
            error = message ?? error;
            bytesReceived--;
            bytesDiscarded++;
        }
    }
}