using System;

namespace Smart3.Protocols
{
    internal sealed class Connectability
    {
        [Flags]
        internal enum CommunicationFlags
        {
            None = 0,
            // Level 1.
            UseModem = 1 << 0,
            AllowRS485 = 1 << 1,
            ExtendedProtocol = 1 << 2,
            ImmediateHistoryTransmission = 1 << 3,
            AutoHistoryTransmission = 1 << 4,
            AutoHistorySynchronization = 1 << 5,
            RS232 = 1 << 6,
            // Level 2.
            AllowExternalCommand = 1 << 7,
            AllowImmediateReportTransmission = 1 << 8
        }

        internal bool BCC { get; private set; }
        internal int HelloTimeSecondsNormal { get; private set; }
        internal int HelloTimeSecondsFast { get; private set; }
        internal int HelloTimeSecondsSlow { get; private set; }
        internal int TimeoutMilliseconds { get; private set; }
        internal bool BeepOnTimeout { get; private set; }
        internal int Retransmissions { get; private set; }
        internal int InteractivityLevel { get; private set; }
        internal int HistoryTransmissionLevel { get; private set; }
        internal int CRN { get; private set; }
        internal int HistoryPages { get; private set; }
        internal int BaudRate { get; private set; }
        internal int PLUPages { get; private set; }
        internal int PLUPerPage { get; private set; }
        internal int PLUMax { get; private set; }
        internal int CustomerPages { get; private set; }
        internal int CustomerPerPage { get; private set; }
        internal int CustomerMax { get; private set; }
        internal RS485Addresses Address { get; private set; }
        internal CommunicationFlags Communication { get; private set; }

        internal void Read(MessageData message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (message.Type != MessageTypes.C24_TransmissionLocalConnectabilityProgramming) throw new ArgumentException($"Invalid message type {message.Type}. Expected {MessageTypes.C24_TransmissionLocalConnectabilityProgramming}.", nameof(message));
            try
            {
                // C24 indexes: |0  |1      |2   |3  |4     |5     |6     |7      |8   |9   |10    |11    |12 |13    |14  |15      |16        |17    |18       |19         |20     |21     |22        |23(EXT)  |24(EXT)   |25(EXT)   |26/23     |27/24     |
                // C24 message: |C24:ADDRESS:PAGE:BCC:HELLO1:HELLO2:HELLO3:TIMEOUT:BEEP:RETR:ILEVEL:HLEVEL:CRN:HPAGES:BAUD:PLUPAGES:PLUPERPAGE:PLUMAX:CUSTPAGES:CUSTPERPAGE:CUSTMAX:ADDRESS:COMMFLAGS1:COMFLAGS2:UNKNOWN(1):UNKNOWN(1):UNKNOWN(0):UNKNOWN(0)|
                // Standard message example (25 field count): |C24:ZZZZ:ZZ:0:999:999:999:10:1:3:3:0:0:1:38400:47:287:12949:2:92:176:1:224:0:0|
                // Extended message example (28 field count): |C24:ZZZZ:ZZ:0:999:999:999:10:1:3:3:0:0:1:38400:47:287:12949:2:92:176:1:232:128:1:1:0:0|
                int fieldCount = message.Fields.Count;
                BCC = (byte.Parse(message.Fields[3]) != 0);
                HelloTimeSecondsNormal = ushort.Parse(message.Fields[4]);
                HelloTimeSecondsFast = ushort.Parse(message.Fields[5]);
                HelloTimeSecondsSlow = ushort.Parse(message.Fields[6]);
                TimeoutMilliseconds = ushort.Parse(message.Fields[7]) * 100;
                BeepOnTimeout = (byte.Parse(message.Fields[8]) != 0);
                Retransmissions = byte.Parse(message.Fields[9]);
                InteractivityLevel = byte.Parse(message.Fields[10]);
                HistoryTransmissionLevel = byte.Parse(message.Fields[11]);
                CRN = byte.Parse(message.Fields[12]);
                HistoryPages = ushort.Parse(message.Fields[13]);
                BaudRate = ushort.Parse(message.Fields[14]);
                PLUPages = ushort.Parse(message.Fields[15]);
                PLUPerPage = ushort.Parse(message.Fields[16]);
                PLUMax = ushort.Parse(message.Fields[17]);
                CustomerPages = ushort.Parse(message.Fields[18]);
                CustomerPerPage = ushort.Parse(message.Fields[19]);
                CustomerMax = ushort.Parse(message.Fields[20]);
                Address = ((int)byte.Parse(message.Fields[21])).ToRS485Address();
                // Communication indicators use 1 byte (2 if extended protocol) but not all bits are used.
                int comm1 = byte.Parse(message.Fields[22]);
                int comm2 = (fieldCount == 28) ? byte.Parse(message.Fields[23]) : 0; // Extended protocol only.
                // Comm1 indicator bits: 76543x10
                // Comm2 Indicator bits: 76xxxxxx
                // Int32 bit reference:      |76543210765432107654321076543210|
                // Comm1 2 rightmost bits:   |                        xxxxxx10|
                // Comm1 5 leftmost bits:     |                        76543xxx| >> 1
                // Comm2 2 leftmost bits:   |                        76xxxxxx| << 1
                // Set unused/overlapping bits (x) to zero, do the sifting and blend them together with XOR.
                int communication = (comm1 & 0x03) | ((comm1 & 0xf8) >> 1) | ((comm2 & 0xc0) << 1);
                Communication = (CommunicationFlags)communication;
            }
            catch (Exception)
            {
                throw new ProtocolException($"Could not parse message of type {message.Type}.");
            }
        }
    }
}