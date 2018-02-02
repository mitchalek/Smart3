using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Smart3.Protocols
{
    internal abstract class OperationManager
    {
        private const int processResumerTimeoutMS = 250;
        private static readonly object syncRoot = new object();
        private static readonly Queue<Operation> operations = new Queue<Operation>();
        private static ManualResetEvent processResumer;
        private static Thread processingThread;
        private Thread callingThread;
        internal string PortName { get; set; }
        internal int BaudRate { get; set; }
        protected OperationManager(string portName, int baudRate)
        {
            PortName = portName;
            BaudRate = baudRate;
            callingThread = Thread.CurrentThread;
        }

        protected abstract Dispatcher CreateDispatcher();
        protected abstract Transceiver CreateTransceiver(Dispatcher dispatcher);

        /// <summary>
        /// Queue operation for processing on a dedicated thread.
        /// </summary>
        /// <param name="operation">Operation to be processed.</param>
        internal void EnqueueOperation(Operation operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            // Start a new dedicated thread from a thread pool in order to prevent possibly long wait times to obtain the lock.
            ThreadPool.QueueUserWorkItem(o =>
            {
                lock (syncRoot)
                {
                    // Place operation on a queue.
                    operations.Enqueue(operation);
                    // Start a new dedicated thread if one does not exist.
                    if (processingThread == null || !processingThread.IsAlive)
                    {
                        processingThread = new Thread(ProcessOperations);
                        processingThread.CurrentCulture = CultureInfo.InvariantCulture;
                        processingThread.CurrentUICulture = CultureInfo.InvariantCulture;
                        processingThread.Start();
                        Debug.WriteLine($"Started new thread with Id: {processingThread.ManagedThreadId}");

                    }
                    // Send the signal that new operation has been queued.
                    else
                    {
                        processResumer?.Set();
                    }
                }
            });
        }

        // This method runs on a dedicated thread.
        private void ProcessOperations()
        {
            Exception processException = null;
            //ExceptionDispatchInfo capturedException = null;
            Dispatcher dispatcher = null;
            Transceiver transceiver = null;
            Console console = null;
            // Operations to execute.
            StartupOperation startupOperation = new StartupOperation();
            ShutdownOperation shutdownOperation = new ShutdownOperation();
            Operation queuedOperation = null;

            processResumer = new ManualResetEvent(false);

            bool isLocked = false;

            try
            {
                // Create RS232/485 layered communication protocol objects.
                // General packet dispatcher (1st layer).
                // Uses serial stream to send/receive raw byte data in form of generic packets (byte array units).
                // Received bytes are packetized using standard specific packet builder state machine.
                dispatcher = CreateDispatcher();
                // Packet layer protocol (2nd layer).
                // Provides with basic communication mechanisms such as sending/receiving messages and indicator control bytes using concrete packets.
                transceiver = CreateTransceiver(dispatcher);
                // Basic communication interface (3rd layer).
                // Provides with auxiliary communication mechanisms such as dialogue initiation, broadcasting and interactivities.
                // Operations use these mechanisms to actually communicate with a cash register.
                console = new Console(transceiver);
                // Open underlying serial port.
                dispatcher.Open();
                // Execute startup operation which locks keyboard on a cash register.
                startupOperation.Execute(console);
                // Execute all operations from a queue.
                while (true)
                {
                    // Locking ensures that thread starter doesn't get the incorrect information about this thread's running state (i.e. while shutting down).
                    Monitor.Enter(syncRoot, ref isLocked);
                    if (callingThread.IsAlive && operations.Count > 0)
                    {
                        queuedOperation = operations.Dequeue();
                        // Loop will continue after dequeued operation is executed so it is safe to release the lock.
                        Monitor.Exit(syncRoot);
                        isLocked = false;
                        // Execute dequeued operation.
                        queuedOperation.Execute(console);
                        queuedOperation = null;
                        // Continuation support (i.e. caller was waiting for operation completion in order to queue another operation).
                        // If we don't block the thread here then shutdown will begin and new thread will need to be created for subsequent operations.
                        if (callingThread.IsAlive && operations.Count == 0)
                        {
                            // Pause this thread for a timeout specified or until signaled when a new operation has been queued.
                            processResumer.Reset();
                            processResumer.WaitOne(processResumerTimeoutMS);
                        }
                        continue;
                    }
                    // Break the loop in order to begin shutdown operation if caller has abandoned or no more queued operations.
                    // Lock will be released at the end of a finally statement.
                    // If we didn't use a lock operations that arrive while shutting down will never be completed because no new thread is created while this one is alive.
                    break;
                }
                // Execute shutdown operation which unlocks keyboard on a cash register.
                shutdownOperation.Execute(console);
            }
            catch (PacketValidationException e)
            {
                processException = new ProtocolException("One or more invalid bytes received.", e);
                //capturedException = ExceptionDispatchInfo.Capture(new ProtocolException("One or more invalid bytes received.", e));
            }
            catch (Exception e)
            {
                //capturedException = ExceptionDispatchInfo.Capture(e);
                processException = e;
            }
            finally
            {
                // Abort current operation that caused the exception.
                queuedOperation?.Abort(processException);
                // Abort all other queued operations.
                while (operations.Count > 0)
                {
                    operations.Dequeue().Abort(processException);
                }
                // Disposal.
                dispatcher?.Close();
                processResumer.Dispose();
                processResumer = null;
                // Release the lock.
                if (isLocked)
                {
                    Monitor.Exit(syncRoot);
                }
            }
        }
    }

    internal sealed class OperationManager232 : OperationManager
    {
        internal OperationManager232(string portName, int baudRate) : base(portName, baudRate) { }

        protected override Dispatcher CreateDispatcher()
        {
            return new Dispatcher<PacketBuilder232>(PortName, BaudRate);
        }
        protected override Transceiver CreateTransceiver(Dispatcher dispatcher)
        {
            return new Transceiver232(dispatcher);
        }
    }

    internal sealed class OperationManager485 : OperationManager
    {
        private RS485Addresses address = RS485Addresses._01;

        internal OperationManager485(string portName, int baudRate, RS485Addresses address) : base(portName, baudRate)
        {
            this.address = address;
        }

        protected override Dispatcher CreateDispatcher()
        {
            return new Dispatcher<PacketBuilder485>(PortName, BaudRate);
        }
        protected override Transceiver CreateTransceiver(Dispatcher dispatcher)
        {
            return new Transceiver485(dispatcher, address);
        }
    }
}