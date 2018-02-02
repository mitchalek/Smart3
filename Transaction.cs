using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Smart3.Protocols;

namespace Smart3
{
    /// <summary>
    /// Class provides a safe mechanism for initiating, controlling and completing the sale on a cash register.
    /// </summary>
    public sealed class Transaction
    {
        #region Private fields.
        private static readonly object syncStatus = new object();
        private static readonly object syncKeepalive = new object();
        private readonly Smart3Service service;
        private readonly TaskScheduler eventScheduler;
        private readonly PLU[] sale;
        private readonly List<PLUInfo> continued;
        private readonly List<PLU> discontinued;
        private ManualResetEvent manualResetEvent;
        private KeepaliveOperation keepaliveOperation;
        private TransactionStatus status;
        private bool isCancellationRequested;
        private bool isKeepaliveStarted;
        private bool isKeepaliveFinished;
        #endregion
        #region Internal constructor.
        internal Transaction(Smart3Service service, IEnumerable<PLU> sale)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (sale == null) throw new ArgumentNullException(nameof(sale));
            var saleProc = ProcessSale(sale);
            if (saleProc.Length == 0) throw new ArgumentException("Enumeration yielded no results.", nameof(sale));
            this.sale = saleProc;
            continued = new List<PLUInfo>(saleProc.Length);
            discontinued = new List<PLU>(0);
            Continued = new ReadOnlyCollection<PLUInfo>(continued);
            Discontinued = new ReadOnlyCollection<PLU>(discontinued);
            this.service = service;
            eventScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        }
        #endregion
        #region Public properties.
        /// <summary>
        /// Gets or sets the buyer's payment value in cash that is used for completing this transaction.
        /// </summary>
        public decimal Payment { get; set; }
        /// <summary>
        /// Indicates the current total sale value of this transaction that must be covered with a payment.
        /// </summary>
        public decimal Total { get { return Continued?.GetTotal() ?? 0.0M; } }
        /// <summary>
        /// Indicates the difference between payment value and total value.
        /// </summary>
        public decimal Change { get { return Payment - Total; } }
        /// <summary>
        /// Read-only collection of known sale items containing detailed information found on a cash register when this transaction is opened.
        /// Individual sale items can be modified (e.g. setting a new price or changing the name) before closing this transaction.
        /// </summary>
        public ReadOnlyCollection<PLUInfo> Continued { get; }
        /// <summary>
        /// Read-only collection of sale items that were not found on a cash register and caused this transaction to be rejected.
        /// </summary>
        public ReadOnlyCollection<PLU> Discontinued { get; }
        /// <summary>
        /// Indicates whether this transaction is currently an active transaction blocking all other operations on a cash register.
        /// </summary>
        public bool IsActive { get { return service.GetIsActiveTransaction(this); } }
        /// <summary>
        /// Indicates the current status of this transaction.
        /// </summary>
        public TransactionStatus Status
        {
            get { return status; }
            private set
            {
                if (value != status)
                {
                    var args = new StatusChangedEventArgs(status, value);
                    status = value;
                    OnStatusChanged(args);
                }
            }
        }
        #endregion
        #region Status.Starting : Begin transaction.
        private async Task<bool> BeginTransactionTask(IProgress<ServiceProgressInfo> progress)
        {
            service.ActivateTransaction(this);
            try
            {
                // Safe change status.
                ThrowIfStatusChanged(TransactionStatus.Initialized, TransactionStatus.Starting);
                // Retrieve PLUInfos from cash register matching the sale items by Id.
                continued.Clear();
                discontinued.Clear();
                PLUInfo pluInfo;
                IEnumerable<PLUInfo> pluReading;
                int counter = 0;
                foreach (var plu in sale)
                {
                    // Read PLUInfo.
                    pluReading = await service.ReadPLUTask(plu, plu, null).ConfigureAwait(false);
                    pluInfo = pluReading.FirstOrDefault();
                    // Not found.
                    if (pluInfo == null)
                    {
                        discontinued.Add(plu);
                    }
                    // Found.
                    else
                    {
                        // Copy user provided quantity value.
                        pluInfo.Quantity = plu.Quantity;
                        // Reset changed flag because of the quantity we just set.
                        pluInfo.IsChanged = false;
                        continued.Add(pluInfo);
                    }
                    // Report progress.
                    progress?.Report(new ServiceProgressInfo(plu, ++counter, sale.Length, ServiceProgressType.Reading));
                    // Cancellation safe point.
                    ThrowIfCancellationRequested();
                }
                // Deactivate transaction if missing any PLUInfos.
                if (discontinued.Count > 0)
                {
                    // Cancel if requested or set final status.
                    ThrowIfCancellationRequested(TransactionStatus.Rejected);
                    service.DeactivateTransaction(this);
                    return false;
                }
                // Cancel if requested or set progressive status.
                ThrowIfCancellationRequested(TransactionStatus.Waiting);
                // Create event loop that will probe cash register every n milliseconds keeping the transaction physically open.
                // Transaction will be faulted when completing if serial communication becomes broken.
                KeepaliveStart();
                return true;
            }
            // Exception caused by cancellation.
            catch (OperationCanceledException)
            {
                // Status will be changed by cancellation request method we just deactivate the transaction.
                service.DeactivateTransaction(this);
                throw;
            }
            // Other exception.
            catch
            {
                // Force initial status and clear cancellation request, if any.
                DenyCancellationIfRequested(TransactionStatus.Initialized);
                service.DeactivateTransaction(this);
                // Transaction is now reusable if user handles the exception.
                throw;
            }
        }
        /// <summary>
        /// Start an asynchronous operation of opening this transaction.
        /// Includes reading detailed information of sale items from a cash register which are then stored in <see cref="Continued"/> or <see cref="Discontinued"/> collections.
        /// </summary>
        /// <returns>Task that started the asynchronous operation resulting in true if transaction has been opened and <see cref="Discontinued"/> collection is empty, false otherwise.</returns>
        /// <exception cref="InvalidOperationException">Operation is not compatible with current status.</exception>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        /// <exception cref="TaskCanceledException">Asynchronous operation is canceled by user request.</exception>
        public Task<bool> BeginTransactionAsync()
        {
            return BeginTransactionAsync(null);
        }
        /// <summary>
        /// Start an asynchronous operation of opening this transaction with progress reporting.
        /// Includes reading detailed information of sale items from a cash register which are then stored in <see cref="Continued"/> or <see cref="Discontinued"/> collections.
        /// </summary>
        /// <param name="progress">User defined provider for progress updates.</param>
        /// <returns>Task that started the asynchronous operation resulting in true if transaction has been opened and <see cref="Discontinued"/> collection is empty, false otherwise.</returns>
        /// <exception cref="InvalidOperationException">Operation is not compatible with current status.</exception>
        /// <exception cref="UnauthorizedAccessException">Access is denied to the serial port.</exception>
        /// <exception cref="IOException">Serial port cannot be opened.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="TransactionOpenException">Transaction is in progress.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterTicketOpenException">Fiscal ticket is open on a cash register.</exception>
        /// <exception cref="CashRegisterKeyStrikingStartedException">User input is in progress on a cash register.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        /// <exception cref="TaskCanceledException">Asynchronous operation is canceled by user request.</exception>
        public Task<bool> BeginTransactionAsync(IProgress<ServiceProgressInfo> progress)
        {
            return BeginTransactionTask(progress);
        }
        #endregion
        #region Status.Waiting : Private keepalive transaction starter, stopper, and event loop.
        // Keepalive operation starter.
        private void KeepaliveStart()
        {
            lock (syncKeepalive)
            {
                if (!isKeepaliveStarted)
                {
                    manualResetEvent = new ManualResetEvent(false);
                    keepaliveOperation = new KeepaliveOperation();
                    keepaliveOperation.OperationCompleted += KeepaliveOperation_OperationCompleted;
                    service.OperationManager.EnqueueOperation(keepaliveOperation);
                    isKeepaliveFinished = false;
                    isKeepaliveStarted = true;
                }
            }
        }
        // Keepalive operation stopper.
        private void KeepaliveStop()
        {
            // Signal the event handler to stop on current wait timeout or next iteration.
            manualResetEvent?.Set();
            lock (syncKeepalive)
            {
                if (isKeepaliveStarted)
                {
                    // Event loop is still running which is expected if we signaled before the event.
                    if (!isKeepaliveFinished)
                    {
                        // Release the lock and wait for the event handler pulse signal.
                        Monitor.Wait(syncKeepalive);
                    }
                    // Record operation exception and dispose.
                    var exception = keepaliveOperation.OperationException;
                    manualResetEvent.Dispose();
                    manualResetEvent = null;
                    keepaliveOperation.OperationCompleted -= KeepaliveOperation_OperationCompleted;
                    keepaliveOperation = null;
                    isKeepaliveStarted = false;
                    // Throw exception if any.
                    if (exception != null) throw exception;
                }
            }
        }
        // Keepalive operation completed event loop.
        private void KeepaliveOperation_OperationCompleted(object sender, OperationCompletedEventArgs e)
        {
            lock (syncKeepalive)
            {
                var o = (KeepaliveOperation)sender;
                // Continue event loop after a timeout.
                if (o.OperationException == null && !manualResetEvent.WaitOne(1000))
                {
                    service.OperationManager.EnqueueOperation(o);
                }
                // Signaled to stop or faulted.
                else
                {
                    isKeepaliveFinished = true;
                    Monitor.Pulse(syncKeepalive);
                }
            }
        }
        #endregion
        #region Status.Completing : End transaction.
        private async Task EndTransactionTask(IProgress<ServiceProgressInfo> progress)
        {
            // Safe change status.
            ThrowIfStatusChanged(TransactionStatus.Waiting, TransactionStatus.Completing);
            try
            {
                // Copy public property values to ensure consistency throughout the procedure.
                var payment = Payment;
                var total = Total;
                // Make collection elements immutable.
                Continued.SetReadOnly(true);
                // Break the keepalive event loop asynchronously from a thread pool so we don't block the caller while synchronizing.
                // Following task will throw recorded exception if keepalive operation was faulted (e.g. communication timed out).
                await Task.Factory.StartNew(KeepaliveStop, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default).ConfigureAwait(false);
                // Check payment.
                if (payment <= 0.0M || payment < total) throw new InvalidOperationException("Insufficient payment.");
                // Cancellation safe point before we start writing.
                ThrowIfCancellationRequested();
                // Write PLUInfos changed by the user.
                if (Continued.Any(p => p.IsChanged))
                {
                    await service.WritePLUTask(Continued.Where(p => p.IsChanged), progress).ConfigureAwait(false);
                    // Cancellation safe point after writing and before opening a ticket on a cash register.
                    ThrowIfCancellationRequested();
                }
                // No cancellation past this point.
                // Create transact operation and wait for sale completion.
                var operation = new TransactOperation(Continued, payment);
                if (progress != null)
                {
                    operation.ProgressChanged += (o, e) =>
                    {
                        progress.Report(new ServiceProgressInfo(e.CurrentItem, e.CurrentAmount, e.TotalAmount, ServiceProgressType.Selling));
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
                service.OperationManager.EnqueueOperation(operation);
                await tcs.Task.ConfigureAwait(false);
                // Force final status and clear cancellation request, if any.
                DenyCancellationIfRequested(TransactionStatus.Completed);
            }
            // Exception caused by cancellation.
            catch (OperationCanceledException)
            {
                throw;
            }
            // Other exception.
            catch
            {
                DenyCancellationIfRequested(TransactionStatus.Faulted);
            }
            finally
            {
                // Make collection elements mutable again.
                Continued.SetReadOnly(false);
                // Clear active transaction whether it was successful or not.
                service.DeactivateTransaction(this);
            }
        }
        /// <summary>
        /// Start an asynchronous operation of closing this transaction.
        /// Includes writing of modified sale items from a <see cref="Continued"/> collection, <see cref="Payment"/> processing and fiscal ticket closing on a cash register.
        /// </summary>
        /// <returns>Task that started the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Operation is not compatible with current status or insufficient payment.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        /// <exception cref="TaskCanceledException">Asynchronous operation is canceled by user request.</exception>
        public Task EndTransactionAsync()
        {
            return EndTransactionAsync(null);
        }
        /// <summary>
        /// Start an asynchronous operation of closing this transaction with progress reporting.
        /// Includes writing of modified sale items from a <see cref="Continued"/> collection, <see cref="Payment"/> processing and fiscal ticket closing on a cash register.
        /// </summary>
        /// <param name="progress">User defined provider for progress updates.</param>
        /// <returns>Task that started the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Operation is not compatible with current status or insufficient payment.</exception>
        /// <exception cref="TimeoutException">Cash register did not respond to a request.</exception>
        /// <exception cref="ProtocolException">Internal protocol error occured.</exception>
        /// <exception cref="CashRegisterOperatingErrorException">Cash register operating error occured.</exception>
        /// <exception cref="CashRegisterHardwareFaultException">Cash register hardware fault occured.</exception>
        /// <exception cref="TaskCanceledException">Asynchronous operation is canceled by user request.</exception>
        public Task EndTransactionAsync(IProgress<ServiceProgressInfo> progress)
        {
            return EndTransactionTask(progress);
        }
        #endregion
        #region Cancel transaction.
        /// <summary>
        /// Cancel this transaction synchronously.
        /// </summary>
        /// <returns>True if canceled or already canceled, false otherwise.</returns>
        public bool CancelTransaction()
        {
            lock (syncStatus)
            {
                switch (Status)
                {
                    // This is the default status so we can just change it.
                    case TransactionStatus.Initialized:
                        Status = TransactionStatus.Canceled;
                        return true;
                    // Signal running tasks (begin/end transaction tasks) and let them handle the request at nearest cancellation point.
                    case TransactionStatus.Starting:
                    case TransactionStatus.Completing:
                        // Request cancellation of a task.
                        isCancellationRequested = true;
                        // Wait for a task to obtain the lock, do what is neccessary and then pulse.
                        Monitor.Wait(syncStatus);
                        // Now that we reacquired the lock check if request was accepted.
                        if (isCancellationRequested)
                        {
                            Status = TransactionStatus.Canceled;
                            return true;
                        }
                        break;
                    // Break keepalive event loop and cancel.
                    case TransactionStatus.Waiting:
                        try
                        {
                            KeepaliveStop();
                        }
                        // Swallow all exceptions from keepalive task because they are only relevant if user tries to end the transaction.
                        catch { }
                        Status = TransactionStatus.Canceled;
                        service.DeactivateTransaction(this);
                        return true;
                    // Already canceled.
                    case TransactionStatus.Canceled:
                        return true;
                    // Finalized in any other way.
                    case TransactionStatus.Rejected:
                    case TransactionStatus.Faulted:
                    case TransactionStatus.Completed:
                    default:
                        break;
                }
                return false;
            }
        }
        /// <summary>
        /// Start an asynchronous operation of canceling this transaction.
        /// </summary>
        /// <returns>Task that started the asynchronous operation resulting in true if transaction has been canceled, false otherwise.</returns>
        public Task<bool> CancelTransactionAsync()
        {
            return Task.Factory.StartNew(CancelTransaction, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
        }
        #endregion
        #region Private helper methods.
        // Get array of processed sale items that were supplied by the user.
        private PLU[] ProcessSale(IEnumerable<PLU> sale)
        {
            return sale
                // Filter out null sale items.
                .Where(plu => plu != null)
                // Create groups of unique Id having only quantity as a member.
                .GroupBy(plu => plu.Id, plu => plu.Quantity)
                // Project each group into a new PLU with a sum quantity.
                .Select(grp => new PLU(grp.Key, grp.Sum(q => q)))
                // Get results of a query.
                .ToArray();
        }
        // Fault the caller async task if current status does not meet the requirements and optionally change status if requirements are met.
        private void ThrowIfStatusChanged(TransactionStatus expectedStatus, TransactionStatus? newStatusIfNotChanged = null)
        {
            lock (syncStatus)
            {
                if (status != expectedStatus) throw new InvalidOperationException($"Operation is not compatible with current status: {{{status}}}. Expected status: {{{expectedStatus}}}.");
                if (newStatusIfNotChanged.HasValue)
                {
                    Status = newStatusIfNotChanged.Value;
                }
            }
        }
        // Cancel the caller async task if cancellation is requested and optionally change status if not canceled.
        private void ThrowIfCancellationRequested(TransactionStatus? newStatusIfNotRequested = null)
        {
            lock (syncStatus)
            {
                if (isCancellationRequested)
                {
                    // Cancel transaction method is waiting for a pulse after posting the request.
                    Monitor.Pulse(syncStatus);
                    // This will cancel the caller task resulting in System.Threading.Tasks.TaskCanceledException.
                    throw new OperationCanceledException();
                }
                else if (newStatusIfNotRequested.HasValue)
                {
                    Status = newStatusIfNotRequested.Value;
                }
            }
        }
        // Prevent cancellation if requested and optionally change status.
        private void DenyCancellationIfRequested(TransactionStatus? newStatus = null)
        {
            lock (syncStatus)
            {
                if (newStatus.HasValue)
                {
                    Status = newStatus.Value;
                }
                if (isCancellationRequested)
                {
                    isCancellationRequested = false;
                    Monitor.Pulse(syncStatus);
                }
            }
        }
        #endregion
        #region Status changed event.
        /// <summary>
        /// An event reporting change of a transaction status.
        /// </summary>
        public event EventHandler<StatusChangedEventArgs> StatusChanged;
        private void OnStatusChanged(StatusChangedEventArgs args)
        {
            // Invoke event handler from synchronization context captured when this object was created.
            Task.Factory.StartNew(() => StatusChanged?.Invoke(this, args), CancellationToken.None, TaskCreationOptions.None, eventScheduler);
        }
        #endregion
    }

    /// <summary>
    /// Represents the current stage in the lifecycle of a <see cref="Transaction"/>.
    /// </summary>
    public enum TransactionStatus
    {
        /// <summary>
        /// Transaction has been initialized and is ready to begin.
        /// </summary>
        Initialized, // Default status after object construction.
        /// <summary>
        /// Transaction is in the process of receiving price look-up codes (<see cref="PLUInfo"/>) from a cash register.
        /// </summary>
        Starting, // Progressive status 1 of 3.
        /// <summary>
        /// Transaction is waiting for a user to review or modify received <see cref="PLUInfo"/>s and initiate a payment.
        /// </summary>
        Waiting, // Progressive status 2 of 3.
        /// <summary>
        /// Transaction is in the process of updating modified <see cref="PLUInfo"/>s and completing the sale on a cash register.
        /// </summary>
        Completing, // Progressive status 3 of 3.
        /// <summary>
        /// Transaction has been canceled by user request.
        /// </summary>
        Canceled, // Final status.
        /// <summary>
        /// Transaction did not start because some <see cref="PLUInfo"/>s could not be received.
        /// </summary>
        Rejected, // Final status.
        /// <summary>
        /// Transaction did not complete because an exception occured.
        /// </summary>
        Faulted, // Final status.
        /// <summary>
        /// Transaction completed successfully.
        /// </summary>
        Completed // Final status.
    }

    public class StatusChangedEventArgs : EventArgs
    {
        public TransactionStatus OldStatus { get; }
        public TransactionStatus NewStatus { get; }
        public StatusChangedEventArgs(TransactionStatus oldStatus, TransactionStatus newStatus)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
        }
    }
}