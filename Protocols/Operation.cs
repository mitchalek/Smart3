using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace Smart3.Protocols
{
    /// <summary>
    /// Base class for all operations.
    /// </summary>
    internal abstract class Operation
    {
        // Operation manager calls and expects this method to perform operation specific task using the provided console interface.
        // Console interface allows customization of communication flow and data exchange adhering to Smart3 protocol standards.
        internal abstract void Execute(Console console);
        // Default empty command (can be replied with when cash register expects a command and there isn't any).
        private static readonly MessageData defaultCommand = new MessageData("0");
        protected MessageData DefaultCommand { get { return defaultCommand; } }
        // Cash register status and error indicators (parsed A01 hello message).
        internal static CashRegister CashRegister { get; } = new CashRegister();
        // Cash register connectability programming and indicators (parsed C24 message).
        internal static Connectability Connectability { get; } = new Connectability();
        // Exception that caused abortion of the operation, if any.
        internal Exception OperationException { get; private set; }
        // Captured exception that caused abortion of the operation, if any.
        //internal ExceptionDispatchInfo OperationExceptionDispatchInfo { get; private set; }
        // Operation manager may abort the operation at any time.
        internal void Abort(Exception exception)
        {
            OperationException = exception;
            OnAbort();
        }
        // Concrete operations may implement their own abort mechanism via this virtual method.
        protected virtual void OnAbort() { }

        // A01 hello message is a special type of message that contains status and error indicators.
        [MessageTypeContract(MessageTypes.A01_HelloMessage)]
        protected virtual MessageData HandleA01(MessageData message)
        {
            // Read status info.
            CashRegister.Read(message);
            Debug.WriteLine(CashRegister.OperatingMode.ToString() + " Mode (" + CashRegister.Status.ToString() + ").");
            // Common error detection.
            if (CashRegister.Status.HasFlag(CashRegister.StatusFlags.OperatingError))
            {
                throw new CashRegisterOperatingErrorException("Cash register operating error occured.");
            }
            if (CashRegister.Status.HasFlag(CashRegister.StatusFlags.HardwareFault))
            {
                throw new CashRegisterHardwareFaultException("Cash register hardware fault occured.");
            }
            return DefaultCommand;
        }

        // B23 interactivity message indicates a change of cash register's operating mode.
        [MessageTypeContract(MessageTypes.B23_InteractivityKeyTurningStart)]
        protected virtual MessageData HandleB23(MessageData message)
        {
            return DefaultCommand;
        }

        #region Events.
        // Operation completed.
        internal event EventHandler<OperationCompletedEventArgs> OperationCompleted;
        internal protected void OnOperationCompleted(OperationCompletedEventArgs args)
        {
            OperationCompleted?.Invoke(this, args);
        }
        // Progress changed.
        internal event EventHandler<ProgressChangedEventArgs> ProgressChanged;
        protected void OnProgressChanged(ProgressChangedEventArgs args)
        {
            ProgressChanged?.Invoke(this, args);
        }
        #endregion
    }
    #region Primary operations.
    /// <summary>
    /// Communication startup operation that checks for valid status and locks keyboard on cash register.
    /// </summary>
    internal sealed class StartupOperation : Operation
    {
        private bool done;
        internal override void Execute(Console console)
        {
            console.Hello(true);
            console.Answer(HandleA01);
            console.Answer(HandleB23);
            while (!done)
            {
                console.Listen(HandleC24);
            }
        }
        protected override MessageData HandleA01(MessageData message)
        {
            base.HandleA01(message);
            if (CashRegister.Status.HasFlag(CashRegister.StatusFlags.TicketOpen | CashRegister.StatusFlags.NonFiscalTicketOpen))
            {
                throw new CashRegisterTicketOpenException("Unable to take control of the cash register while ticket is open.");
            }
            if (CashRegister.Status.HasFlag(CashRegister.StatusFlags.KeyStrikingStarted))
            {
                throw new CashRegisterKeyStrikingStartedException("Unable to take control of the cash register while user input is in progress.");
            }
            return new MessageData("0;*2;+4;&m"); // Lock CR keyboard, programming mode, request connectability programing.
        }
        [MessageTypeContract(MessageTypes.C24_TransmissionLocalConnectabilityProgramming)]
        private void HandleC24(MessageData message)
        {
            if (message.Fields[3] == "*")
            {
                done = true;
                return;
            }
            // Read connectability info.
            Connectability.Read(message);
        }
    }
    /// <summary>
    /// Communication shutdown operation that unlocks keyboard on cash register and causes disconnection.
    /// </summary>
    internal sealed class ShutdownOperation : Operation
    {
        /*
        There is no explicit way of letting cash register know that we want to end communication.
        Once communication is started the only way to actually end it is to simply stop sending replies to interactive messages.
        Cash register will attempt retransmission of the last message set amount of times before considering computer disconnected.
        Disconnection prodecure takes about 4 seconds (with default programming) during which time cash register becomes unresponsive to the user input.
        */
        internal override void Execute(Console console)
        {
            console.Hello(true);
            console.Answer(HandleA01); // Handler will send command to unlock CR keyboard.
            console.Answer(HandleB23); // Requests another hello via comman.
            // Receive all incoming hello messages but don't send any reply (we know the exact retransmission amount).
            for (int i = 0; i <= Connectability.Retransmissions; i++)
            {
                // Requested hello message comes asap but retransmitted ones come with a programmed timeout between them.
                console.Swallow();
            }
            // Wait for another timeout duration because cash register will not become responsive before that.
            Thread.Sleep(Connectability.TimeoutMilliseconds);
            // Cash register is now disconnected.
        }
        protected override MessageData HandleA01(MessageData message)
        {
            base.HandleA01(message);
            return new MessageData("0;+0;*3"); // Mode change + unlock CR keyboard.
        }
        protected override MessageData HandleB23(MessageData message)
        {
            return new MessageData("0;#A"); // Request final hello via command.
        }
    }
    /// <summary>
    /// Communication keepalive operation that just requests status report from a cash register.
    /// </summary>
    internal sealed class KeepaliveOperation : Operation
    {
        internal override void Execute(Console console)
        {
            console.Hello(true);
            console.Answer(HandleA01);
            OnOperationCompleted(OperationCompletedEventArgs.Empty);
        }
    }
    #endregion
    #region PLU archive operations.
    /// <summary>
    /// Operation for reading PLU data from a cash register's internal memory.
    /// </summary>
    internal sealed class ReadPLUInfoOperation : Operation
    {
        private bool done;
        private string idFrom;
        private string idTo;
        internal List<PLUInfo> PLUInfoCollection { get; } = new List<PLUInfo>();
        internal ReadPLUInfoOperation(PLU pluFrom, PLU pluTo)
        {
            if (pluFrom == null) throw new ArgumentNullException(nameof(pluFrom));
            if (pluTo == null) throw new ArgumentNullException(nameof(pluTo));
            if (string.CompareOrdinal(pluFrom.Id, pluTo.Id) < 0)
            {
                idFrom = pluFrom.Id;
                idTo = pluTo.Id;
            }
            else
            {
                idFrom = pluTo.Id;
                idTo = pluFrom.Id;
            }
        }

        internal override void Execute(Console console)
        {
            console.Hello(true);
            console.Answer(HandleA01);
            console.Answer(HandleB23);
            while (!done)
            {
                console.Listen(HandleC08);
            }
            OnOperationCompleted(new OperationCompletedEventArgs(PLUInfoCollection));
        }
        protected override MessageData HandleA01(MessageData message)
        {
            base.HandleA01(message);
            return new MessageData($"0;+4;&M{idFrom}:{idTo}"); // Programming mode + request PLU programming C08 transmission.
        }
        // Data message containing PLU information stored in a cash register.
        [MessageTypeContract(MessageTypes.C08_TransmissionLocalPLUProgramming)]
        private void HandleC08(MessageData message)
        {
            // 0  |1      |2   |3 |4    |5         |6   |7       |8    |9     |A  |B    |C|D|E|F
            // C08:ADDRESS:PAGE:ID:PRICE:DEPARTMENT:NAME:DISCOUNT:OFFER:PRICE2:TAX:MACRO:0:0:0:0

            // Check if operation completed (no more PLUs) by receiving asterisk symbol as ID.
            if (message.Fields[3] == "*")
            {
                done = true;
                return;
            }
            // Add strings.
            string id = message.Fields[3];
            string name = message.Fields[6];
            // Parse integers.
            int tax = int.Parse(message.Fields[10]);
            int department = int.Parse(message.Fields[5]);
            int macro = int.Parse(message.Fields[11]);
            // Parse price but add missing decimal separator first.
            string priceString = message.Fields[4];
            priceString = priceString.Insert(priceString.Length - 2, ".");
            decimal price = decimal.Parse(priceString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
            // Create new PLU.
            PLUInfo pluInfo = new PLUInfo(id, name, price, department, tax, macro);
            // Add PLU to a collection.
            PLUInfoCollection.Add(pluInfo);
            // Progress notification.
            OnProgressChanged(new ProgressChangedEventArgs(pluInfo, PLUInfoCollection.Count, 0)); // Total amount is not known due to protocol limitations.
        }

        protected override void OnAbort()
        {
            OnOperationCompleted(OperationCompletedEventArgs.Empty);
        }
    }

    /// <summary>
    /// Operation for updating existing or adding new PLUs to a cash register internal memory.
    /// </summary>
    internal sealed class WritePLUInfoOperation : Operation
    {
        private bool done;
        private PLUInfo pluInfo;
        private Queue<PLUInfo> pluInfoQueue;
        private int currentProgressAmount;
        private readonly int totalProgressAmount;
        internal WritePLUInfoOperation(IEnumerable<PLUInfo> pluInfos)
        {
            if (pluInfos == null) throw new ArgumentNullException(nameof(pluInfos));
            pluInfoQueue = new Queue<PLUInfo>(pluInfos.Where(plu => plu != null));
            if (pluInfoQueue.Count == 0) throw new ArgumentException("Collection must not be empty.", nameof(pluInfos));
            totalProgressAmount = pluInfoQueue.Count;
        }

        internal override void Execute(Console console)
        {
            console.Hello(true);
            console.Answer(HandleA01);
            console.Answer(HandleB23);
            while (!done)
            {
                console.Answer(HandleB81);
                if (pluInfo != null)
                {
                    OnProgressChanged(new ProgressChangedEventArgs(pluInfo, ++currentProgressAmount, totalProgressAmount));
                    pluInfo = null;
                }
            }
            OnOperationCompleted(OperationCompletedEventArgs.Empty);
        }
        protected override MessageData HandleA01(MessageData message)
        {
            base.HandleA01(message);
            return new MessageData("0;+4;*G"); // Programming mode + request PLU programming B81 interactivity.
        }
        // B81 interactivity message requesting PLU information so it can be stored in a cash register.
        [MessageTypeContract(MessageTypes.B81_LoadingPLUProgramming)]
        private MessageData HandleB81(MessageData message)
        {
            if (pluInfoQueue.Count == 0)
            {
                done = true;
                return new MessageData("*");
            }

            // 0 |1    |2         |3   |4       |5    |6     |7  |8
            // ID:PRICE:DEPARTMENT:NAME:DISCOUNT:OFFER:PRICE2:TAX:MACRO

            pluInfo = pluInfoQueue.Dequeue();
            MessageData reply = new MessageData
                (
                    pluInfo.Id,
                    decimal.Round(pluInfo.Price * 100, 0, MidpointRounding.AwayFromZero),
                    pluInfo.Department,
                    pluInfo.Name,
                    0,
                    0,
                    0,
                    pluInfo.Tax,
                    pluInfo.Macro
                );

            return reply;
        }

        protected override void OnAbort()
        {
            OnOperationCompleted(OperationCompletedEventArgs.Empty);
        }
    }

    /// <summary>
    /// Fast PLU (initial) writing operation which automatically removes all existing PLUs from a cash register memory.
    /// Requires fiscal closing to be performed first.
    /// </summary>
    internal sealed class BroadcastPLUInfoOperation : Operation
    {
        private const int blockSize = 100;
        private const int completionWaitTimeMS = 3000;
        private const byte eroteme = 0x3F;
        private const byte asterisk = 0x2A;
        private PLUInfo[] pluInfos;
        private int pluInfoIndex;
        private int pluInfosLoaded;

        internal BroadcastPLUInfoOperation(IEnumerable<PLUInfo> pluInfos)
        {
            if (pluInfos == null) throw new ArgumentNullException(nameof(pluInfos));
            this.pluInfos = pluInfos
                .Where(pi => pi != null)
                .Distinct(new PLUComparer<PLUInfo>())
                .OrderBy(pi => pi.Id)
                .ToArray();
            if (this.pluInfos.Length == 0) throw new ArgumentException("Collection must not be empty.", nameof(pluInfos));
        }

        private byte[] GetBroadcastBytes(PLUInfo pluInfo)
        {
            // Broadcast packet data format (all fields have fixed size, 61 bytes total):
            // Field start indexes:  |0    |13     |17          |18     |         |55   |56     |
            // Field sizes:          |ID:13|PRICE:4|DEPARTMENT:1|NAME:21|UNUSED:16|TAX:1|MACRO:1|0|0|0|0|
            byte[] bb = new byte[61];

            byte[] id = Encoding.ASCII.GetBytes(pluInfo.Id);
            Array.Copy(id, bb, id.Length <= 13 ? id.Length : 13);

            byte[] price = BitConverter.GetBytes((int)(decimal.Round(pluInfo.Price * 100, 0, MidpointRounding.AwayFromZero)));
            Array.Copy(price, 0, bb, 13, 4);

            byte department = (byte)pluInfo.Department;
            bb[17] = department;

            byte[] name = Encoding.ASCII.GetBytes(pluInfo.Name);
            Array.Copy(name, 0, bb, 18, name.Length <= 21 ? name.Length : 21);

            byte tax = (byte)(pluInfo.Tax - 1);
            bb[55] = tax;

            byte macro = (byte)pluInfo.Macro;
            bb[56] = macro;

            return bb;
        }

        internal override void Execute(Console console)
        {
            console.Hello(true);
            console.Answer(HandleA01);
            console.Answer(HandleB23);

            pluInfoIndex = 0;
            pluInfosLoaded = 0;
            while (pluInfoIndex < pluInfos.Length)
            {
                // Broadcast PLU.
                console.Broadcast(GetBroadcastBytes(pluInfos[pluInfoIndex]));
                // Progress notification.
                // Note that current progress amount may fall back if whole block needs to be retransmitted.
                OnProgressChanged(new ProgressChangedEventArgs(pluInfos[pluInfoIndex], pluInfoIndex + 1, pluInfos.Length));
                // Query number of loaded PLUs at the end of each block or last PLU.
                if (++pluInfoIndex % blockSize == 0 || pluInfoIndex == pluInfos.Length)
                {
                    console.Broadcast(eroteme);
                    console.Answer(HandleB99);
                }
            }
            // Termiante.
            console.Broadcast(asterisk);
            // Cash register needs few seconds to complete.
            Thread.Sleep(completionWaitTimeMS);
            OnOperationCompleted(OperationCompletedEventArgs.Empty);
        }
        protected override MessageData HandleA01(MessageData message)
        {
            base.HandleA01(message);
            return new MessageData($"0;+4;#z{pluInfos.Length}");
        }
        // Contains information of how many PLUs have been loaded.
        [MessageTypeContract(MessageTypes.B99_LoadingFastPLUProgramming)]
        private MessageData HandleB99(MessageData message)
        {
            // If not all broadcast PLUs have been loaded repeat the last block.
            if (int.Parse(message.Fields[1]) < pluInfoIndex)
            {
                // Position back to the start of the block.
                pluInfoIndex = pluInfosLoaded;
            }
            // Load OK.
            else
            {
                pluInfosLoaded = pluInfoIndex;
            }

            return new MessageData(pluInfosLoaded.ToString());
        }

        protected override void OnAbort()
        {
            OnOperationCompleted(OperationCompletedEventArgs.Empty);
        }
    }
    #endregion
    #region Fiscal and financial operations.
    internal sealed class FinancialReportOperation : Operation
    {
        private bool done;
        internal FinancialReport financialReport = new FinancialReport();

        internal override void Execute(Console console)
        {
            console.Hello(true);
            console.Answer(HandleA01);
            console.Answer(HandleB23);
            while (!done)
            {
                console.Listen(HandleC22);
            }
            OnOperationCompleted(new OperationCompletedEventArgs(financialReport));
        }
        protected override MessageData HandleA01(MessageData message)
        {
            base.HandleA01(message);
            return new MessageData("0;+2;*f"); // Reading mode, transmission of fiscal report.
        }
        [MessageTypeContract(MessageTypes.C22_TransmissionFinancialReport)]
        private void HandleC22(MessageData message)
        {
            string recordId = message.Fields[3];
            // Final record notification.
            if (recordId == "*")
            {
                done = true;
            }
            // Single record containing information on number of tickets issued and items sold for the current fiscal day.
            else if (recordId == "0")
            {
                // |C22:ADDRESS:PAGE:0:TICKETS:CUSTOMERS:ITEMS:OPERATOR:DOCUMENT|
                financialReport.TicketsIssued = int.Parse(message.Fields[4]);
                financialReport.ItemsSold = int.Parse(message.Fields[6]);
            }
            // Format of all following records of importance:
            // Indexes: |0  |1      |2   |3       |4    |5    |...|22    |23    |24      |25      |
            // Message: |C22:ADDRESS:PAGE:RECORDID:NVAL1:AVAL1:...:NVAL10:AVAL10:OPERATOR:DOCUMENT|
            // RECORDID indicates the record type and form of payment.
            // NVAL:AVAL 10 pairs indicate count:amount section values for any given record type.
            // All records come in multiples of 4, each for one form of payment.

            // Payments with record types 4, 41, 42 and 43.
            else if (recordId.StartsWith("4"))
            {
                for (int i = 5; i < message.Fields.Count - 2; i += 2)
                {
                    financialReport.PaymentAmount += decimal.Parse(message.Fields[i]) / 100;
                }
            }
            // Inflows with record types 6, 61, 62 and 63.
            else if (recordId.StartsWith("6"))
            {
                for (int i = 5; i < message.Fields.Count - 2; i += 2)
                {
                    financialReport.InflowAmount += decimal.Parse(message.Fields[i]) / 100;
                }
            }
            // Outflows with record types 7, 71, 72 and 73.
            else if (recordId.StartsWith("7"))
            {
                for (int i = 5; i < message.Fields.Count - 2; i += 2)
                {
                    financialReport.OutflowAmount += decimal.Parse(message.Fields[i]) / 100;
                }
            }
            // Drawer with record types 8, 81, 82 and 83.
            else if (recordId.StartsWith("8"))
            {
                for (int i = 5; i < message.Fields.Count - 2; i += 2)
                {
                    financialReport.DrawerAmount += decimal.Parse(message.Fields[i]) / 100;
                }
            }
            // Payments in period with record types 9, 91, 92 and 93.
            else if (recordId.StartsWith("9"))
            {
                for (int i = 5; i < message.Fields.Count - 2; i += 2)
                {
                    financialReport.PaymentsInPeriod += decimal.Parse(message.Fields[i]) / 100;
                }
            }
            // Ignored records are considered irelevant but may be added later if need arises.
        }
    }

    /// <summary>
    /// Fiscal day closing operation.
    /// </summary>
    internal sealed class FiscalClosingOperation : Operation
    {
        internal override void Execute(Console console)
        {
            console.Hello(true);
            console.Answer(HandleA01);
            console.Answer(HandleB23);
            console.Answer(HandleB45);
            OnOperationCompleted(OperationCompletedEventArgs.Empty);
        }
        protected override MessageData HandleA01(MessageData message)
        {
            base.HandleA01(message);
            if (CashRegister.Status.HasFlag(CashRegister.StatusFlags.FiscalMemoryError))
            {
                throw new CashRegisterFiscalMemoryErrorException("Cash register fiscal memory error occured.");
            }
            if (CashRegister.Status.HasFlag(CashRegister.StatusFlags.FiscalMemoryFull))
            {
                throw new CashRegisterFiscalMemoryFullException("Cash register fiscal memory is full.");
            }
            return new MessageData("0;+3;#Z"); // Closing mode, perform fiscal closing.
        }
        [MessageTypeContract(MessageTypes.B45_InteractivityFiscalClosingEnd)]
        private MessageData HandleB45(MessageData message)
        {
            return DefaultCommand;
        }

        protected override void OnAbort()
        {
            OnOperationCompleted(OperationCompletedEventArgs.Empty);
        }
    }

    /// <summary>
    /// Operation for completing a sale of PLU records using keyboard simulation.
    /// </summary>
    internal sealed class TransactOperation : Operation
    {
        private bool done;
        private PLUInfo currentItem;
        private Queue<PLUInfo> queuedItems;
        //private PLUInfo[] sales;
        private decimal payment;
        //private decimal ticketValue;
        private int currentProgressAmount;
        private readonly int totalProgressAmount;

        //private static readonly Random rnd = new Random();
        //private static readonly int[] bills = { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000 };
        //private decimal GetRandomPayment(decimal value)
        //{
        //    int bill = bills[rnd.Next(bills.Length)];
        //    return value - (value % bill) + bill;
        //}

        internal TransactOperation(IEnumerable<PLUInfo> sale, decimal payment)
        {
            if (sale == null) throw new ArgumentNullException(nameof(sale));
            if (payment <= 0.0M) throw new ArgumentOutOfRangeException(nameof(payment), "Value must be a positive decimal.");
            queuedItems = new Queue<PLUInfo>(sale);
            if (queuedItems.Count == 0) throw new ArgumentException("Enumeration yielded no results.", nameof(sale));
            this.payment = payment;
            totalProgressAmount = queuedItems.Count;
        }

        internal override void Execute(Console console)
        {
            console.Hello(true);
            console.Answer(HandleA01); // Mode change.
            console.Answer(HandleB23); // Initiate sale of first PLU on mode change interactivity.
            while (!done)
            {
                console.AnswerAny(HandleB10, HandleB14); // Eventual ticket start and sale of subsequent PLUs.
            }
            console.Answer(HandleB15); // Initiate payment on subtotal interactivity.
            console.Answer(HandleB17); // Payment interactivity.
            console.Answer(HandleB18); // Ticket end.
            OnOperationCompleted(OperationCompletedEventArgs.Empty);
        }

        #region Message handlers.
        protected override MessageData HandleA01(MessageData message)
        {
            base.HandleA01(message);
            //if (CashRegister.Status.HasFlag(CashRegister.StatusFlags.FiscalMemoryError))
            //{
            //    throw new CashRegisterFiscalMemoryErrorException("Cash register fiscal memory error occured.");
            //}
            //if (CashRegister.Status.HasFlag(CashRegister.StatusFlags.FiscalMemoryFull))
            //{
            //    throw new CashRegisterFiscalMemoryFullException("Cash register fiscal memory is full.");
            //}
            return new MessageData("0;+1"); // Registering mode.
        }
        protected override MessageData HandleB23(MessageData message)
        {
            // Clear input and initiate sale of first PLU.
            // This will cause two interactivities:
            // B14 indicating sale of PLU (one for each sale so we use it to chain multiple PLU sales).
            // B10 ticket start (one after first plu sale).
            currentItem = queuedItems.Dequeue();
            return KeyboardSimulationSequencer.Parse($"$CLEAR$$CLEAR${currentItem.Quantity}*{currentItem.Id}$PLU$", false);
        }
        // Ticket start.
        private MessageData HandleB10(MessageData message)
        {
            return DefaultCommand;
        }
        // Sale of PLU.
        private MessageData HandleB14(MessageData message)
        {
            // 0  |1  |2         |3    |4
            // B14:PLU:DEPARTMENT:PRICE:QUANTITY

            if (currentItem.Id != message.Fields[1])
            {
                Debug.WriteLine($"Warning!!! PLU sale ID mismatch. Sold PLU [{message.Fields[1]}] / Saved PLU [{currentItem.Id}].");
            }

            // Calculate ticket value.
            //var articlePrice = decimal.Parse(message.Fields[3]) / 100;
            //var articleQuantity = int.Parse(message.Fields[4]);
            //ticketValue += articlePrice * articleQuantity;
            // Notify listeners of article sale.
            OnProgressChanged(new ProgressChangedEventArgs(currentItem, ++currentProgressAmount, totalProgressAmount));
            // Prepare another article.
            if (queuedItems.Count > 0)
            {
                currentItem = queuedItems.Dequeue();
                return KeyboardSimulationSequencer.Parse($"{currentItem.Quantity}*{currentItem.Id}$PLU$", false);
            }
            // Initiate payment if no more articles.
            done = true;
            return KeyboardSimulationSequencer.Parse("$SUBTOTAL$", false);
        }
        // Subtotal start.
        private MessageData HandleB15(MessageData message)
        {
            //decimal payment = GetRandomPayment(ticketValue);
            return KeyboardSimulationSequencer.Parse(string.Format(CultureInfo.InvariantCulture, "{0:0.00}$TOTAL$", payment), false);
        }
        // Payment start.
        private MessageData HandleB17(MessageData message)
        {
            return DefaultCommand;
        }
        // Ticket end.
        private MessageData HandleB18(MessageData message)
        {
            return DefaultCommand;
        }
        #endregion

        protected override void OnAbort()
        {
            OnOperationCompleted(OperationCompletedEventArgs.Empty);
        }
    }
    #endregion
    #region Event args.
    internal class OperationCompletedEventArgs : EventArgs
    {
        internal static readonly new OperationCompletedEventArgs Empty = new OperationCompletedEventArgs(null);
        internal object Result { get; }
        internal OperationCompletedEventArgs(object result)
        {
            Result = result;
        }
    }
    internal class ProgressChangedEventArgs : EventArgs
    {
        internal PLUInfo CurrentItem { get; }
        internal int CurrentAmount { get; }
        internal int TotalAmount { get; }
        internal ProgressChangedEventArgs(PLUInfo currentItem, int currentAmount, int totalAmount)
        {
            CurrentItem = currentItem;
            CurrentAmount = currentAmount;
            TotalAmount = totalAmount;
        }
    }
    #endregion
}