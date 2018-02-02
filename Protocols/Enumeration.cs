using System;

namespace Smart3.Protocols
{
    #region Control byte enumeration.
    /// <summary>
    /// All control bytes of this protocol.
    /// </summary>
    internal enum ControlBytes : byte
    {
        STX_Postamble = 0x2,
        ETX_Terminator = 0x3,
        EOT_Preamble = 0x4,
        ENQ_Enquiry = 0x5,
        ACK_Acknowledged = 0x6,
        BEL_PaperOut = 0x7,
        DLE_StatusRequest = 0x10,
        DC1_StatusRequest = 0x11,
        NAK_NotAcknowledged = 0x15,
        SYN_Synchronize = 0x16,
        CAN_OperationAborted = 0x18
    }
    /// <summary>
    /// Message control bytes of this protocol.
    /// </summary>
    internal enum MessageControlBytes : byte
    {
        STX_Postamble = ControlBytes.STX_Postamble,
        ETX_Terminator = ControlBytes.ETX_Terminator,
        EOT_Preamble = ControlBytes.EOT_Preamble
    }
    /// <summary>
    /// Indicator control bytes of this protocol.
    /// </summary>
    internal enum IndicatorControlBytes : byte
    {
        ENQ_Enquiry = ControlBytes.ENQ_Enquiry,
        ACK_Acknowledged = ControlBytes.ACK_Acknowledged,
        BEL_PaperOut = ControlBytes.BEL_PaperOut,
        DLE_StatusRequest = ControlBytes.DLE_StatusRequest,
        DC1_StatusRequest = ControlBytes.DC1_StatusRequest,
        NAK_NotAcknowledged = ControlBytes.NAK_NotAcknowledged,
        SYN_Synchronize = ControlBytes.SYN_Synchronize,
        CAN_OperationAborted = ControlBytes.CAN_OperationAborted
    }
    #endregion

    #region Cash register address enumeration (RS-485 only).
    internal enum RS485Addresses : byte
    {
        _01 = 0xA0,
        _02 = 0xA1,
        _03 = 0xA2,
        _04 = 0xA3,
        _05 = 0xA4,
        _06 = 0xA5,
        _07 = 0xA6,
        _08 = 0xA7,
        _09 = 0xA8,
        _10 = 0xA9,
        _11 = 0xAA,
        _12 = 0xAB,
        _13 = 0xAC,
        _14 = 0xAD,
        _15 = 0xAE,
        _16 = 0xAF,
        ALL = 0xC0
    }
    internal static class RS485AddressConversionExtensions
    {
        //internal static int ToInt(this RS485Addresses address)
        //{
        //    if (!Enum.IsDefined(typeof(RS485Addresses), (int)address)) throw new InvalidEnumArgumentException(nameof(address), (int)address, typeof(RS485Addresses));
        //    return (int)address - 0x9F;
        //}
        internal static RS485Addresses ToRS485Address(this int address)
        {
            if (address < 1 || address > 16) throw new ArgumentOutOfRangeException(nameof(address), address, "Value exceeded allowable range: 1–16 inclusive.");
            return (RS485Addresses)(address + 0x9F);
        }
    }
    #endregion

    #region Keyboard simulation key enumeration.
    public enum KeyboardSimulationKeys
    {
        _KEY = 1,
        _CLEAR = 3,
        _RETURN = 27,
        _MULTIPLY = 42,
        _DECIMAL = 43,
        _MINUS = 44,
        _000 = 46,
        _00 = 47,
        _0 = 48,
        _1 = 49,
        _2 = 50,
        _3 = 51,
        _4 = 52,
        _5 = 53,
        _6 = 54,
        _7 = 55,
        _8 = 56,
        _9 = 57,
        _PLU = 62,
        _SHIFT = 95,
        _SUBTOTAL = 101,
        _TOTAL = 102,
        _KEYBOARD = 109
    }
    #endregion
}
