using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

namespace Smart3.Protocols
{
    internal abstract class Dispatcher : IDisposable
    {
        internal abstract void Log(string message);
        internal abstract void Send(Packet packet);
        internal abstract void Send(Packet packet, int timeout);
        internal abstract Packet Receive();
        internal abstract Packet Receive(int timeout);
        internal abstract void Open();
        internal abstract void Close();
        internal abstract bool IsOpen { get; }
        internal abstract bool IsInBufferEmpty { get; }
        internal abstract bool IsOutBufferEmpty { get; }
        protected internal abstract int ReadTimeout { get; protected set; }
        protected internal abstract int WriteTimeout { get; protected set; }
        internal abstract void DiscardInBuffer();
        internal abstract void DiscardOutBuffer();

        public void Dispose()
        {
            Close();
        }
    }
    internal sealed class Dispatcher<TPacketBuilder> : Dispatcher where TPacketBuilder : PacketBuilder, new()
    {
        private const int bufferSize = 1024;
        private byte[] buffer;
        private readonly object syncRoot = new object();
        private SerialPort serialPort;
        //private static Stopwatch sw = new Stopwatch();

        private const bool logEnabled = true;
        private BinaryWriter log;

        internal string PortName { get; private set; }
        internal int BaudRate { get; private set; }
        protected internal override int ReadTimeout { get; protected set; } = 5000;
        protected internal override int WriteTimeout { get; protected set; } = 5000;
        internal override bool IsOpen
        {
            get
            {
                lock (syncRoot)
                {
                    return (serialPort != null && serialPort.IsOpen);
                }
            }
        }
        internal override bool IsInBufferEmpty
        {
            get
            {
                CheckPort();
                lock (syncRoot)
                {
                    return (serialPort.BytesToRead < 1);
                }
            }
        }
        internal override bool IsOutBufferEmpty
        {
            get
            {
                CheckPort();
                lock (syncRoot)
                {
                    return (serialPort.BytesToWrite < 1);
                }
            }
        }
        internal override void DiscardInBuffer()
        {
            CheckPort();
            lock (syncRoot)
            {
                serialPort.DiscardInBuffer();
            }
        }
        internal override void DiscardOutBuffer()
        {
            CheckPort();
            lock (syncRoot)
            {
                serialPort.DiscardOutBuffer();
            }
        }

        internal Dispatcher(string portName, int baudRate)
        {
            if (portName == null) throw new ArgumentNullException(nameof(portName));
            if (!portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Incorrect port name.", nameof(portName));
            if (baudRate != 38400 && baudRate != 19200 && baudRate != 9600) throw new ArgumentOutOfRangeException(nameof(baudRate), "Incorrect baud rate.");
            PortName = portName;
            BaudRate = baudRate;
        }
        internal Dispatcher(string portName, int baudRate, int readTimeout, int writeTimeout) : this(portName, baudRate)
        {
            if (readTimeout < -1) throw new ArgumentException("Incorrect read timeout.", nameof(readTimeout));
            if (writeTimeout < -1) throw new ArgumentException("Incorrect write timeout.", nameof(writeTimeout));
            ReadTimeout = readTimeout;
            WriteTimeout = writeTimeout;
        }

        private void CheckPort()
        {
            if (!IsOpen) throw new IOException("Serial port is closed.");
        }

        internal override void Open()
        {
            lock (syncRoot)
            {
                if (serialPort == null)
                {
                    serialPort = new SerialPort(PortName, BaudRate, Parity.None, 8, StopBits.One)
                    {
                        Handshake = Handshake.None,
                        ReadBufferSize = bufferSize,
                        WriteBufferSize = bufferSize,
                        //ReadTimeout = ReadTimeout,
                        //WriteTimeout = WriteTimeout
                    };
                    serialPort.Open();
                }
                if (logEnabled && log == null)
                {
                    log = new BinaryWriter(File.Open("Smart3.log", FileMode.Create), Encoding.ASCII);
                    //log = new StreamWriter("Smart3.log", false, Encoding.ASCII);
                }
                if (buffer == null)
                {
                    buffer = new byte[bufferSize];
                }
            }
        }

        internal override void Close()
        {
            lock (syncRoot)
            {
                if (serialPort != null)
                {
                    serialPort.Close();
                    serialPort = null;
                    //try
                    //{
                    //    serialPort.Close();
                    //    serialPort.Dispose();
                    //}
                    //catch
                    //{
                    //}
                }
                if (log != null)
                {
                    log.Close();
                    log = null;
                }
                if (buffer != null)
                {
                    buffer = null;
                }
            }
        }

        internal override void Log(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            lock (syncRoot)
            {
                if (log != null)
                {
                    log.Write(message);
                    log.Write((byte)13);
                    log.Write((byte)10);
                }
            }
        }

        internal override void Send(Packet packet)
        {
            Send(packet, WriteTimeout);
        }
        internal override void Send(Packet packet, int timeout)
        {
            if (packet == null) throw new ArgumentNullException("packet");
            if (timeout < -1) throw new ArgumentException("Incorrect timeout.", nameof(timeout));
            CheckPort();
            lock (syncRoot)
            {
                // Retrieve byte data from a packet.
                byte[] packetSerialData = packet.GetBytes();
                if (packetSerialData == null) throw new ArgumentException("Packet is empty.");
                // Copy packet data to a private buffer.
                int length = packetSerialData.Length;
                Array.Copy(packetSerialData, buffer, length);
                // Purge serial port buffers.
                //serialPort.DiscardOutBuffer();
                //serialPort.DiscardInBuffer();
                // Set the write timeout.
                serialPort.WriteTimeout = timeout;
                // Write to serial stream.
                serialPort.BaseStream.Write(buffer, 0, length);
                //serialPort.BaseStream.Flush();
                // Write to log.
                if (log != null)
                {
                    log.Write((byte)62);
                    log.Write((byte)32);
                    for (int i = 0; i < length; i++)
                    {
                        log.Write(packetSerialData[i]);
                    }
                    log.Write((byte)13);
                    log.Write((byte)10);
                }
                //Debug.Write("> " + StaticControlBytes.BytesToString(buffer, 0, length));
                //Debug.WriteLine("");

                return;
            }
        }

        internal override Packet Receive()
        {
            return Receive(ReadTimeout);
        }
        internal override Packet Receive(int timeout)
        {
            CheckPort();
            lock (syncRoot)
            {
                Packet packet = null;
                TPacketBuilder packetBuilder = new TPacketBuilder();
                serialPort.ReadTimeout = timeout;
                try
                {
                    int i, bytesRead, bytesToRead = packetBuilder.BytesExpected;
                    while (bytesToRead > 0)
                    {
                        bytesRead = serialPort.BaseStream.Read(buffer, 0, bytesToRead);
                        for (i = 0; i < bytesRead; i++)
                        {
                            packet = packetBuilder.Packetize(buffer[i]);
                            bytesToRead = packetBuilder.BytesExpected;
                        }
                    }
                    // Write to log.
                    byte[] packetSerialData = packet.GetBytes();
                    int length = packetSerialData.Length;
                    if (log != null && length > 0)
                    {
                        log.Write((byte)60);
                        log.Write((byte)32);
                        for (i = 0; i < length; i++)
                        {
                            log.Write(packetSerialData[i]);
                        }
                        log.Write((byte)13);
                        log.Write((byte)10);
                    }
                }
                catch (TimeoutException e)
                {
                    throw new TimeoutException($"Insufficient bytes received. Timeout: {timeout}ms. Packet builder state info: {packetBuilder.BytesExpected} expected, {packetBuilder.BytesReceived} received, {packetBuilder.BytesDiscarded} discarded bytes.", e);
                }
                return packet;
            }
        }
    }
}