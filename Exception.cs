using System;

namespace Smart3
{
    /// <summary>
    /// Represents errors that occur during packetization of received bytes.
    /// </summary>
    public class PacketValidationException : Exception
    {
        public PacketValidationException() { }
        public PacketValidationException(string message) : base(message) { }
        public PacketValidationException(string message, Exception inner) : base(message, inner) { }
    }
    /// <summary>
    /// Represents Smart3 communication protocol errors.
    /// </summary>
    public class ProtocolException : Exception
    {
        public ProtocolException() { }
        public ProtocolException(string message) : base(message) { }
        public ProtocolException(string message, Exception inner) : base(message, inner) { }
    }
    /// <summary>
    /// Base exception class for all cash register errors.
    /// </summary>
    public class CashRegisterException : Exception
    {
        public CashRegisterException() { }
        public CashRegisterException(string message) : base(message) { }
        public CashRegisterException(string message, Exception inner) : base(message, inner) { }
    }
    /// <summary>
    /// Indicates that cash register operating error has occured.
    /// </summary>
    public class CashRegisterOperatingErrorException : CashRegisterException
    {
        public CashRegisterOperatingErrorException() { }
        public CashRegisterOperatingErrorException(string message) : base(message) { }
        public CashRegisterOperatingErrorException(string message, Exception inner) : base(message, inner) { }
    }
    /// <summary>
    /// Indicates that cash register operation cannot be performed because sale is in progress.
    /// </summary>
    public class CashRegisterTicketOpenException : CashRegisterException
    {
        public CashRegisterTicketOpenException() { }
        public CashRegisterTicketOpenException(string message) : base(message) { }
        public CashRegisterTicketOpenException(string message, Exception inner) : base(message, inner) { }
    }
    /// <summary>
    /// Indicates that cash register operation cannot be performed because user input is in progress.
    /// </summary>
    public class CashRegisterKeyStrikingStartedException : CashRegisterException
    {
        public CashRegisterKeyStrikingStartedException() { }
        public CashRegisterKeyStrikingStartedException(string message) : base(message) { }
        public CashRegisterKeyStrikingStartedException(string message, Exception inner) : base(message, inner) { }
    }
    /// <summary>
    /// Indicates that cash register hardware fault has occured.
    /// </summary>
    public class CashRegisterHardwareFaultException : CashRegisterException
    {
        public CashRegisterHardwareFaultException() { }
        public CashRegisterHardwareFaultException(string message) : base(message) { }
        public CashRegisterHardwareFaultException(string message, Exception inner) : base(message, inner) { }
    }
    /// <summary>
    /// Indicates that cash register fiscal memory error has occured.
    /// </summary>
    public class CashRegisterFiscalMemoryErrorException : CashRegisterException
    {
        public CashRegisterFiscalMemoryErrorException() { }
        public CashRegisterFiscalMemoryErrorException(string message) : base(message) { }
        public CashRegisterFiscalMemoryErrorException(string message, Exception inner) : base(message, inner) { }
    }
    /// <summary>
    /// Indicates that cash register fiscal memory is full.
    /// </summary>
    public class CashRegisterFiscalMemoryFullException : CashRegisterException
    {
        public CashRegisterFiscalMemoryFullException() { }
        public CashRegisterFiscalMemoryFullException(string message) : base(message) { }
        public CashRegisterFiscalMemoryFullException(string message, Exception inner) : base(message, inner) { }
    }
    /// <summary>
    /// Indicates that current operation is invalid because fiscal day is not closed.
    /// </summary>
    public class FiscalDayOpenException : InvalidOperationException
    {
        public FiscalDayOpenException() { }
        public FiscalDayOpenException(string message) : base(message) { }
        public FiscalDayOpenException(string message, Exception inner) : base(message, inner) { }
    }
    /// <summary>
    /// Indicates that current operation is invalid because transaction is in progress.
    /// </summary>
    public class TransactionOpenException : InvalidOperationException
    {
        public TransactionOpenException() { }
        public TransactionOpenException(string message) : base(message) { }
        public TransactionOpenException(string message, Exception inner) : base(message, inner) { }
    }

    //public sealed class ExceptionDispatchInfo
    //{
    //    readonly Exception _exception;
    //    readonly object _source;
    //    readonly string _stackTrace;

    //    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    //    static readonly FieldInfo RemoteStackTrace = typeof(Exception).GetField("_remoteStackTraceString", PrivateInstance);
    //    static readonly FieldInfo Source = typeof(Exception).GetField("_source", PrivateInstance);
    //    static readonly MethodInfo InternalPreserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace", PrivateInstance);

    //    private ExceptionDispatchInfo(Exception source)
    //    {
    //        _exception = source;
    //        _stackTrace = _exception.StackTrace + Environment.NewLine;
    //        _source = Source.GetValue(_exception);
    //    }

    //    public Exception SourceException { get { return _exception; } }

    //    public static ExceptionDispatchInfo Capture(Exception source)
    //    {
    //        if (source == null)
    //            throw new ArgumentNullException("source");

    //        return new ExceptionDispatchInfo(source);
    //    }

    //    public void Throw()
    //    {
    //        try
    //        {
    //            throw _exception;
    //        }
    //        catch
    //        {
    //            InternalPreserveStackTrace.Invoke(_exception, new object[0]);
    //            RemoteStackTrace.SetValue(_exception, _stackTrace);
    //            Source.SetValue(_exception, _source);
    //            throw;
    //        }
    //    }
    //}
}
