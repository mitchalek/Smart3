using System;
using System.Globalization;

namespace Smart3.Protocols
{
    /// <summary>
    /// Cash Register's status and error indicator data.
    /// </summary>
    internal sealed class CashRegister
    {
        #region Status flags and modes enumeration.
        /// <summary>
        /// Operating modes.
        /// </summary>
        internal enum OperatingModes : byte
        {
            Inactive,
            Registering,
            Reading,
            Closing,
            Programming
        }
        /// <summary>
        /// Operating status and error flags.
        /// </summary>
        [Flags]
        internal enum StatusFlags
        {
            None = 0,
            // Status flags.
            TicketOpen = 1 << 0,
            NonFiscalTicketOpen = 1 << 1,
            KeyStrikingStarted = 1 << 2,
            Reconnection = 1 << 3,
            KeyboardLockedByHost = 1 << 4,
            // Error flags 1.
            RetransmissionLimitAttained = 1 << 5,
            SequenceInvalid = 1 << 6,
            SyntaxInvalid = 1 << 7,
            TimedOut = 1 << 8,
            CommandIncompatibleWithStatus = 1 << 9,
            CommandUnacceptable = 1 << 10,
            OperatingError = 1 << 11,
            // Error flags 2.
            HardwareFault = 1 << 12,
            MemoryReset = 1 << 13,
            FiscalMemoryError = 1 << 14,
            PowerLossOperationInterruption = 1 << 15,
            FiscalMemoryFull = 1 << 16,
            FiscalClosingThresholdAttained = 1 << 17,
            Fiscalized = 1 << 18,
            EuroFiscalized = 1 << 19,
            // Error flags 3 (extended protocol only).
            RemoteMode = 1 << 20,
            GenericPrinterError = 1 << 21,
            GenericError = 1 << 22
        }
        #endregion
        #region Properties.
        /// <summary>
        /// Cash Register's operating mode.
        /// </summary>
        internal OperatingModes OperatingMode { get; private set; }
        /// <summary>
        /// Cash Register's operating status and error flags.
        /// </summary>
        internal StatusFlags Status { get; private set; }
        /// <summary>
        /// Cash Register's date and time of the event.
        /// </summary>
        internal DateTime Timestamp { get; private set; } = default(DateTime);
        /// <summary>
        /// Cash Register's name.
        /// </summary>
        internal string Name { get; private set; } = string.Empty;
        /// <summary>
        /// Cash Register's serial number.
        /// </summary>
        internal string SerialNumber { get; private set; } = string.Empty;
        #endregion

        /// <summary>
        /// Read status information from a <see cref="MessageData"/> having type <see cref="MessageTypes.A01_HelloMessage"/>.
        /// </summary>
        /// <param name="message">Message to read status information from.</param>
        internal void Read(MessageData message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (message.Type != MessageTypes.A01_HelloMessage) throw new ArgumentException($"Invalid message type {message.Type}. Expected {MessageTypes.A01_HelloMessage}.", nameof(message));
            try
            {
                // This is an example of how one hello message could look like in a real scenario:
                // Extended indexes:  |0  |1  |2  |3  |4  |5         |6       |7      |8       |
                // Extended message:  |A01:068:128:192:000:3112991159:SMARTIII:R000001:        |
                // Standard indexes:  |0  |1  |2  |3  |4         |5       |6      |7       |
                // Standard message:  |A01:068:128:192:3112991159:SMARTIII:R000001:        |
                // Parse [1-4] status indicator field values.
                int fieldCount = message.Fields.Count;
                int field1 = byte.Parse(message.Fields[1]);
                int field2 = byte.Parse(message.Fields[2]);
                int field3 = byte.Parse(message.Fields[3]);
                int field4 = (fieldCount == 9) ? byte.Parse(message.Fields[4]) : 0; // Extended protocol only.

                // Read operating mode value (first 3 bits) by zeroing out flag bits that come after.
                OperatingMode = (OperatingModes)(field1 & 0x07); // 00000111

                // Order all flag bits from 4 fields into one Int32 leaving out any non-flag bits.
                // We shift the field bits like this (x bits are non-flag bits while number is a flag bit index in a byte):
                // Int32 (4 byte reference):    |76543210765432107654321076543210|
                // Field1 bits:                    |                        76543xxx| >> 3
                // Field2 bits:            |                        x6543210| << 5
                // Field3 bits:     |                        76543210| << 12
                // Field4 bits: |                        x654xxxx| << 16

                // Zero out field2 leftmost bit because it is always fixed at 1 and will be replaced with field3 rightmost bit when shifting.
                field2 &= 0x7F; // 01111111
                // Invert field3 leftmost bit because it uses reversed logic (0 when cash register is 'eurofiscalized', 1 otherwise).
                field3 ^= 0x80; // 10000000
                // Pack all relevant bits into single int32 according to our own status flags enumeration.
                int status = (field1 >> 3) | (field2 << 5) | (field3 << 12) | (field4 << 16);

                // Convert status value to our ordered enum flags representation.
                Status = (StatusFlags)status;

                // Read timestamp, name and a serial number.
                Timestamp = DateTime.ParseExact(message.Fields[fieldCount - 4], "ddMMyyHHmm", CultureInfo.InvariantCulture);
                Name = message.Fields[fieldCount - 3];
                SerialNumber = message.Fields[fieldCount - 2];
            }
            catch (Exception)
            {
                throw new ProtocolException($"Could not parse message of type {message.Type}.");
            }
        }
    }
}