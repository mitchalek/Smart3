using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Security;
using Smart3.Protocols;

namespace Smart3
{
    /// <summary>
    /// Class provides an interface for communicating with a "WINCOR-NIXDORF SMART III" cash register via serial port.
    /// </summary>
    public sealed class Smart3Service
    {
        private const string portNamePrefix = "COM";
        private const string portNameError = "The port name does not begin with \"COM\".";
        private const string baudRateError = "Smart3 protocol supports only bauds of 9600, 19200, or 38400.";
        private static readonly object syncTransaction = new object();
        private static Transaction transaction;
        private readonly OperationManager operationManager;
        internal OperationManager OperationManager { get { return operationManager; } }

        #region Public properties.
        /// <summary>
        /// Gets or sets the name of serial port (for example, COM1).
        /// </summary>
        /// <exception cref="ArgumentNullException">PortName is null.</exception>
        /// <exception cref="ArgumentException">PortName does not begin with "COM".</exception>
        public string PortName
        {
            get { return operationManager.PortName; }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (!value.StartsWith(portNamePrefix, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(portNameError, nameof(value));
                operationManager.PortName = value;
            }
        }
        /// <summary>
        /// Gets or sets the baud rate of serial port (either 9600, 19200, or 38400).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">BaudRate is not supported.</exception>
        public int BaudRate
        {
            get { return operationManager.BaudRate; }
            set
            {
                if (value != 38400 && value != 19200 && value != 9600) throw new ArgumentOutOfRangeException(nameof(value), baudRateError);
                operationManager.BaudRate = value;
            }
        }
        #endregion

        #region Constructors.
        /// <summary>
        /// Create a new instance of the <see cref="Smart3Service"/> class using the specified port name and baud rate via RS-232 serial interface.
        /// </summary>
        /// <param name="portName">The port to use (for example, COM1).</param>
        /// <param name="baudRate">The baud rate (either 9600, 19200, or 38400).</param>
        /// <exception cref="ArgumentNullException">PortName is null.</exception>
        /// <exception cref="ArgumentException">PortName does not begin with "COM".</exception>
        /// <exception cref="ArgumentOutOfRangeException">BaudRate is not supported.</exception>
        public Smart3Service(string portName, int baudRate)
        {
            if (portName == null) throw new ArgumentNullException(nameof(portName));
            if (!portName.StartsWith(portNamePrefix, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(portNameError, nameof(portName));
            if (baudRate != 38400 && baudRate != 19200 && baudRate != 9600) throw new ArgumentOutOfRangeException(nameof(baudRate), baudRateError);
            operationManager = new OperationManager232(portName, baudRate);
        }
        /// <summary>
        /// Create a new instance of the <see cref="Smart3Service"/> class using the specified port name, baud rate and cash register address via RS-485 serial interface.
        /// </summary>
        /// <param name="portName">The port to use (for example, COM1).</param>
        /// <param name="baudRate">The baud rate (either 9600, 19200, or 38400).</param>
        /// <param name="address">The RS-485 address of a cash register (1 through 16).</param>
        /// <exception cref="ArgumentNullException">PortName is null.</exception>
        /// <exception cref="ArgumentException">PortName does not begin with "COM".</exception>
        /// <exception cref="ArgumentOutOfRangeException">BaudRate is not supported or address is outside of allowable range.</exception>
        public Smart3Service(string portName, int baudRate, int address)
        {
            if (portName == null) throw new ArgumentNullException(nameof(portName));
            if (!portName.StartsWith(portNamePrefix, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(portNameError, nameof(portName));
            if (baudRate != 38400 && baudRate != 19200 && baudRate != 9600) throw new ArgumentOutOfRangeException(nameof(baudRate), baudRateError);
            operationManager = new OperationManager485(portName, baudRate, address.ToRS485Address());
        }
        #endregion

        #region Read PLU.
        internal Task<IEnumerable<PLUInfo>> ReadPLUTask(PLU pluFrom, PLU pluTo, IProgress<ServiceProgressInfo> progress)
        {
            // Declare new operation.
            var operation = new ReadPLUInfoOperation(pluFrom, pluTo);
            // Optional progress report.
            if (progress != null)
            {
                operation.ProgressChanged += (o, e) =>
                {
                    progress.Report(new ServiceProgressInfo(e.CurrentItem, e.CurrentAmount, e.TotalAmount, ServiceProgressType.Reading));
                };
            }
            // Create task object.
            var tcs = new TaskCompletionSource<IEnumerable<PLUInfo>>();
            operation.OperationCompleted += (o, e) =>
            {
                var exception = ((Operation)o).OperationException;
                if (exception != null)
                {
                    tcs.SetException(exception);
                }
                else
                {
                    tcs.SetResult((IEnumerable<PLUInfo>)e.Result);
                }
            };
            // Place operation on processing queue.
            operationManager.EnqueueOperation(operation);

            return tcs.Task;
        }
        /// <summary>
        /// Start an asynchronous operation of reading range of price look-up codes from a cash register with progress reporting.
        /// </summary>
        /// <param name="pluFrom">Price look-up code at which to begin reading.</param>
        /// <param name="pluTo">Price look-up code at which to end reading.</param>
        /// <param name="progress">User defined provider for progress updates.</param>
        /// <returns>Task that started the asynchronous operation resulting in price look-up codes that were read from a cash register.</returns>
        /// <exception cref="ArgumentNullException">Either pluFrom or pluTo is null.</exception>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        public Task<IEnumerable<PLUInfo>> ReadPLUAsync(PLU pluFrom, PLU pluTo, IProgress<ServiceProgressInfo> progress)
        {
            lock (syncTransaction)
            {
                CheckTransaction();
                return ReadPLUTask(pluFrom, pluTo, progress);
            }
        }
        /// <summary>
        /// Start an asynchronous operation of reading all price look-up codes from a cash register with progress reporting.
        /// </summary>
        /// <param name="progress">User defined provider for progress updates.</param>
        /// <returns>Task that started the asynchronous operation resulting in price look-up codes that were read from a cash register.</returns>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        public Task<IEnumerable<PLUInfo>> ReadPLUAsync(IProgress<ServiceProgressInfo> progress)
        {
            return ReadPLUAsync(new PLU(" "), new PLU("zzzzzzzzzzzzz"), progress);
        }
        /// <summary>
        /// Start an asynchronous operation of reading range of price look-up codes from a cash register.
        /// </summary>
        /// <param name="pluFrom">Price look-up code at which to begin reading.</param>
        /// <param name="pluTo">Price look-up code at which to end reading.</param>
        /// <returns>Task that started the asynchronous operation resulting in price look-up codes that were read from a cash register.</returns>
        /// <exception cref="ArgumentNullException">Either pluFrom or pluTo is null.</exception>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        public Task<IEnumerable<PLUInfo>> ReadPLUAsync(PLU pluFrom, PLU pluTo)
        {
            return ReadPLUAsync(pluFrom, pluTo, null);
        }
        /// <summary>
        /// Start an asynchronous operation of reading all price look-up codes from a cash register.
        /// </summary>
        /// <returns>Task that started the asynchronous operation resulting in price look-up codes that were read from a cash register.</returns>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        public Task<IEnumerable<PLUInfo>> ReadPLUAsync()
        {
            return ReadPLUAsync(progress: null);
        }
        /// <summary>
        /// Start an asynchronous operation of reading price look-up code from a cash register.
        /// </summary>
        /// <param name="plu">Price look-up code to read.</param>
        /// <returns>Task that started the asynchronous operation resulting in price look-up code that was read from a cash register or null if not found.</returns>
        /// <exception cref="ArgumentNullException">Plu is null.</exception>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        public async Task<PLUInfo> ReadPLUAsync(PLU plu)
        {
            var list = await ReadPLUAsync(plu, plu, null).ConfigureAwait(false);
            var pluInfo = list.FirstOrDefault();
            return pluInfo;
        }
        #endregion

        #region Write PLU.
        internal Task WritePLUTask(IEnumerable<PLUInfo> pluInfos, IProgress<ServiceProgressInfo> progress)
        {
            var operation = new WritePLUInfoOperation(pluInfos);
            if (progress != null)
            {
                operation.ProgressChanged += (o, e) =>
                {
                    progress.Report(new ServiceProgressInfo(e.CurrentItem, e.CurrentAmount, e.TotalAmount, ServiceProgressType.Writing));
                };
            }
            var tcs = new TaskCompletionSource<object>();
            operation.OperationCompleted += (o, e) =>
            {
                
                var exception = ((Operation)o).OperationException;
                if (exception != null)
                {
                    tcs.SetException(exception);
                }
                else
                {
                    tcs.SetResult(e.Result);
                }
            };
            operationManager.EnqueueOperation(operation);
            return tcs.Task;
        }
        /// <summary>
        /// Start an asynchronous operation of writing price look-up codes to a cash register with progress reporting.
        /// </summary>
        /// <param name="pluInfos">Price look-up codes to write.</param>
        /// <param name="progress">User defined provider for progress updates.</param>
        /// <returns>Task that started the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">PluInfos is null.</exception>
        /// <exception cref="ArgumentException">PluInfos contains no elements.</exception>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        public Task WritePLUAsync(IEnumerable<PLUInfo> pluInfos, IProgress<ServiceProgressInfo> progress)
        {
            lock (syncTransaction)
            {
                CheckTransaction();
                return WritePLUTask(pluInfos, progress);
            }
        }
        /// <summary>
        /// Start an asynchronous operation of writing price look-up codes to a cash register.
        /// </summary>
        /// <param name="pluInfos">Price look-up codes to write.</param>
        /// <returns>Task that started the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">PluInfos is null.</exception>
        /// <exception cref="ArgumentException">PluInfos contains no elements.</exception>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        public Task WritePLUAsync(IEnumerable<PLUInfo> pluInfos)
        {
            return WritePLUAsync(pluInfos, null);
        }
        /// <summary>
        /// Start an asynchronous operation of writing price look-up code to a cash register.
        /// </summary>
        /// <param name="pluInfo">Price look-up code to write.</param>
        /// <returns>Task that started the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">PluInfo is null.</exception>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        public Task WritePLUAsync(PLUInfo pluInfo)
        {
            if (pluInfo == null) throw new ArgumentNullException(nameof(pluInfo));
            return WritePLUAsync(new PLUInfo[] { pluInfo }, null);
        }
        #endregion

        #region Broadcast PLU.
        internal Task BroadcastPLUTask(IEnumerable<PLUInfo> pluInfos, IProgress<ServiceProgressInfo> progress)
        {
            var operation = new BroadcastPLUInfoOperation(pluInfos);
            if (progress != null)
            {
                operation.ProgressChanged += (o, e) =>
                {
                    progress.Report(new ServiceProgressInfo(e.CurrentItem, e.CurrentAmount, e.TotalAmount, ServiceProgressType.Writing));
                };
            }
            var tcs = new TaskCompletionSource<object>();
            operation.OperationCompleted += (o, e) =>
            {
                var exception = ((Operation)o).OperationException;
                if (exception != null)
                {
                    tcs.SetException(exception);
                }
                else
                {
                    tcs.SetResult(e.Result);
                }
            };
            operationManager.EnqueueOperation(operation);
            return tcs.Task;
        }
        /// <summary>
        /// Start an asynchronous operation of fast initial loading of price look-up codes to a cash register with progress reporting.
        /// </summary>
        /// <param name="pluInfos">Price look-up codes to initialize cash register with.</param>
        /// <param name="progress">User defined provider for progress updates.</param>
        /// <returns>Task that started the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">PluInfos is null.</exception>
        /// <exception cref="ArgumentException">PluInfos contains no elements.</exception>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="FiscalDayOpenException">Fiscal day is open on a cash register.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        public async Task BroadcastPLUAsync(IEnumerable<PLUInfo> pluInfos, IProgress<ServiceProgressInfo> progress)
        {
            // Receive fiscal report first to make sure that fiscal day is closed.
            Task<FinancialReport> financialReportTask;
            lock (syncTransaction)
            {
                CheckTransaction();
                financialReportTask = FinancialReportTask();
            }
            var financialReport = await financialReportTask.ConfigureAwait(false);
            if (financialReport.TicketsIssued > 0) throw new FiscalDayOpenException("Fiscal day is not closed.");
            // Proceed with fast loading.
            Task broadcastPLUTask;
            lock (syncTransaction)
            {
                CheckTransaction();
                broadcastPLUTask = BroadcastPLUTask(pluInfos, progress);
            }
            await broadcastPLUTask.ConfigureAwait(false);
        }
        /// <summary>
        /// Start an asynchronous operation of fast initial loading of price look-up codes to a cash register.
        /// </summary>
        /// <param name="pluInfos">Price look-up codes to initialize cash register with.</param>
        /// <returns>Task that started the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">PluInfos is null.</exception>
        /// <exception cref="ArgumentException">PluInfos contains no elements.</exception>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="FiscalDayOpenException">Fiscal day is open on a cash register.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        public Task BroadcastPLUAsync(IEnumerable<PLUInfo> pluInfos)
        {
            return BroadcastPLUAsync(pluInfos, null);
        }
        #endregion

        #region Financial report.
        internal Task<FinancialReport> FinancialReportTask()
        {
            var operation = new FinancialReportOperation();
            var tcs = new TaskCompletionSource<FinancialReport>();
            operation.OperationCompleted += (o, e) =>
            {
                var exception = ((Operation)o).OperationException;
                if (exception != null)
                {
                    tcs.SetException(exception);
                }
                else
                {
                    tcs.SetResult((FinancialReport)e.Result);
                }
            };
            operationManager.EnqueueOperation(operation);
            return tcs.Task;
        }
        /// <summary>
        /// Start an asynchronous operation of receiving financial report from a cash register.
        /// </summary>
        /// <returns>Task that started the asynchronous operation resulting in cash register's financial report.</returns>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        public Task<FinancialReport> FinancialReportAsync()
        {
            lock (syncTransaction)
            {
                CheckTransaction();
                return FinancialReportTask();
            }
        }
        #endregion

        #region Fiscal closing.
        internal Task FiscalClosingTask()
        {
            var operation = new FiscalClosingOperation();
            var tcs = new TaskCompletionSource<object>();
            operation.OperationCompleted += (o, e) =>
            {
                var exception = ((Operation)o).OperationException;
                if (exception != null)
                {
                    tcs.SetException(exception);
                }
                else
                {
                    tcs.SetResult(e.Result);
                }
            };
            operationManager.EnqueueOperation(operation);
            return tcs.Task;
        }
        /// <summary>
        /// Start an asynchronous operation of closing a fiscal day on a cash register.
        /// </summary>
        /// <returns>Task that started the asynchronous operation.</returns>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        /// <exception cref="CashRegisterFiscalMemoryErrorException">Cash register fiscal memory error occured.</exception>
        /// <exception cref="CashRegisterFiscalMemoryFullException">Cash register fiscal memory is full.</exception>
        public Task FiscalClosingAsync()
        {
            lock (syncTransaction)
            {
                CheckTransaction();
                return FiscalClosingTask();
            }
        }
        #endregion

        #region Transactions.
        /// <summary>
        /// Creates a new transaction object which can be used to safely perform a sale on a cash register.
        /// </summary>
        /// <param name="sale">Enumerable collection of sale items having defined price look-up codes and quantities.</param>
        /// <returns>New transaction object.</returns>
        /// <exception cref="ArgumentNullException">Sale is null.</exception>
        /// <exception cref="ArgumentException">Sale contains no elements.</exception>
        public Transaction CreateTransaction(IEnumerable<PLU> sale)
        {
            return new Transaction(this, sale);
        }
        // Throw if any transaction is active.
        private void CheckTransaction()
        {
            if (!ReferenceEquals(transaction, null)) throw new TransactionOpenException("Transaction in progress.");
        }
        // Set an active transaction if none or throw.
        internal void ActivateTransaction(Transaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            lock (syncTransaction)
            {
                CheckTransaction();
                Smart3Service.transaction = transaction;
            }
        }
        // Clear transaction if active.
        internal void DeactivateTransaction(Transaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            lock (syncTransaction)
            {
                if (ReferenceEquals(Smart3Service.transaction, transaction))
                {
                    Smart3Service.transaction = null;
                }
            }
        }
        // Check if transaction is active.
        internal bool GetIsActiveTransaction(Transaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            lock (syncTransaction)
            {
                return ReferenceEquals(Smart3Service.transaction, transaction);
            }
        }
        #endregion

        #region Static PLU delimited text file import/export.
        /// <summary>
        /// Export the <see cref="PLUInfo"/> collection to a delimited text file.
        /// </summary>
        /// <remarks>
        /// Null or duplicate collection elements are ignored.
        /// Collection elements are sorted by Id field in ascending order.
        /// Delimited field order: Name, Id, Price, Tax, Department, and Macro.
        /// If the target file already exists, it is overwritten.
        /// Produced file is compatible with legacy "Smart3 Interface v1.0.4" by Saša Jandrić.
        /// </remarks>
        /// <param name="source"><see cref="PLUInfo"/> collection to export.</param>
        /// <param name="path">Delimited text file path.</param>
        /// <param name="delimiter">Field separator character.</param>
        /// <exception cref="ArgumentNullException">Either source or path is null.</exception>
         /// <exception cref="ArgumentException">Delimiter is not an ASCII character or invalid path string.</exception>
        /// <exception cref="PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length.</exception>
        /// <exception cref="DirectoryNotFoundException">The specified path is invalid.</exception>
        /// <exception cref="IOException">An I/O error occurred while opening the file.</exception>
        /// <exception cref="UnauthorizedAccessException">Path specified a directory, file that is read-only, or caller does not have the required permission.</exception>
        /// <exception cref="NotSupportedException">Path format is not supported.</exception>
        /// <exception cref="SecurityException">The caller does not have the required permission.</exception>
        public static void ExportPLU(IEnumerable<PLUInfo> source, string path, Delimiter delimiter = Delimiter.Auto)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (path == null) throw new ArgumentNullException(nameof(path));
            string separator = (delimiter == Delimiter.Auto) ? ((char)Delimiter.Comma).ToString() : ((char)delimiter).ToString();
            var lines = source
                .Where(pi => pi != null)
                .Distinct(new PLUComparer<PLUInfo>())
                .OrderBy(pi => pi.Id)
                .Select(pi => string.Join(
                    separator,
                    pi.Name,
                    pi.Id,
                    pi.Price.ToString("F", CultureInfo.InvariantCulture),
                    pi.Tax.ToString(),
                    pi.Department.ToString(),
                    pi.Macro.ToString()));

            File.WriteAllLines(path, lines);
        }
        /// <summary>
        /// Import the <see cref="PLUInfo"/> collection from a delimited text file.
        /// </summary>
        /// <remarks>
        /// Expected delimited field order: Name, Id, Price, Tax, Department, and Macro.
        /// Text records that cannot be parsed or validated are ignored.
        /// </remarks>
        /// <param name="path">Delimited text file path.</param>
        /// <param name="delimiter">Field separator character.</param>
        /// <returns><see cref="PLUInfo"/> collection containing imported elements, including duplicates.</returns>
        /// <exception cref="ArgumentNullException">Path is null.</exception>
         /// <exception cref="ArgumentException">Delimiter is not an ASCII character or invalid path string.</exception>
        /// <exception cref="PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length.</exception>
        /// <exception cref="DirectoryNotFoundException">The specified path is invalid.</exception>
        /// <exception cref="IOException">An I/O error occurred while opening the file.</exception>
        /// <exception cref="UnauthorizedAccessException">Path specified a directory or caller does not have the required permission.</exception>
        /// <exception cref="FileNotFoundException">The file specified in path was not found.</exception>
        /// <exception cref="NotSupportedException">Path format is not supported.</exception>
        /// <exception cref="SecurityException">The caller does not have the required permission.</exception>
        public static IEnumerable<PLUInfo> ImportPLU(string path, Delimiter delimiter = Delimiter.Auto)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            // Read lines of text from a file.
            var lineArray = File.ReadAllLines(path);
            // Create a list to store imported records.
            var pluInfos = new List<PLUInfo>(lineArray.Length);
            // Delimiter to divide the fields with.
            char separator;
            // Detect delimiter.
            if (delimiter == Delimiter.Auto)
            {
                // Char picking algorithm is simple: collect all non-letter and non-digit characters from all lines then pick one with most occurences.
                // This comes after the fact that 5 separator characters are expected in a line that is roughly 40 characters long (that is a delimiter being 12.5% of all characters in a file).
                separator = lineArray
                .SelectMany(str => str.Where(c => !char.IsLetterOrDigit(c)))
                .GroupBy(c => c)
                .OrderByDescending(cc => cc.Count())
                .Select(cc => cc.Key)
                .FirstOrDefault();
                // The result that would indicate that the file was empty or in an invalid format.
                if (separator == 0 || separator > 127)
                {
                    return pluInfos;
                }
            }
            // Use provided delimiter.
            else
            {
                separator = (char)delimiter;
            }
            // Parse and validate line by line.
            for (int i = 0; i < lineArray.Length; i++)
            {
                // Create an array of fields.
                var fields = lineArray[i].Split(separator);
                // Skip parsing this line if not enough fields produced.
                if (fields.Length < 6) continue;
                try
                {
                    var name = fields[0];
                    var id = fields[1];
                    var price = decimal.Parse(fields[2]);
                    var tax = int.Parse(fields[3]);
                    var department = int.Parse(fields[4]);
                    var macro = int.Parse(fields[5]);
                    var pluInfo = new PLUInfo(id, name, price, department, tax, macro);
                    pluInfos.Add(pluInfo);
                }
                catch { continue; }
            }

            return pluInfos;
        }
        #endregion
    }

    public enum Delimiter
    {
        Auto,
        Tab = 9,
        Comma = 44,
        Colon = 58,
        Semicolon = 59
    }
}