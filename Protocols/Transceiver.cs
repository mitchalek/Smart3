using System;
using System.Threading;

namespace Smart3.Protocols
{
    internal abstract class Transceiver
    {
        protected byte sequence, crn;
        protected Packet pkRequestHello;
        protected Packet pkRequestHelloImmediate;
        protected Packet pkAcknowledged;
        protected Packet pkNotAcknowledged;

        private Dispatcher dispatcher;
        protected Dispatcher Dispatcher { get { return dispatcher; } }
        internal Transceiver(Dispatcher dispatcher)
        {
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
            this.dispatcher = dispatcher;
        }

        internal abstract MessageData ReceiveMessage();
        internal abstract IndicatorControlBytes ReceiveIndicator();
        internal abstract void SendMessage(MessageData message);
        internal abstract void BroadcastSequence(byte[] byteSequence);

        internal void SendHelloRequest(bool immediateMode)
        {
            if (immediateMode)
            {
                Dispatcher.Send(pkRequestHelloImmediate);
            }
            else
            {
                Dispatcher.Send(pkRequestHello);
            }
        }
        internal void SendACK()
        {
            Dispatcher.Send(pkAcknowledged);
        }
        internal void SendNAK()
        {
            Dispatcher.Send(pkNotAcknowledged);
        }
    }
    internal sealed class Transceiver232 : Transceiver
    {
        internal Transceiver232(Dispatcher dispatcher) : base(dispatcher)
        {
            pkRequestHello = new IndicatorPacket232(IndicatorControlBytes.DLE_StatusRequest);
            pkRequestHelloImmediate = new IndicatorPacket232(IndicatorControlBytes.DC1_StatusRequest);
            pkAcknowledged = new IndicatorPacket232(IndicatorControlBytes.ACK_Acknowledged);
            pkNotAcknowledged = new IndicatorPacket232(IndicatorControlBytes.NAK_NotAcknowledged);
        }

        internal override IndicatorControlBytes ReceiveIndicator()
        {
            IndicatorPacket232 pkIndicatorIn = (IndicatorPacket232)Dispatcher.Receive();
            return pkIndicatorIn.Indicator;
        }
        internal override MessageData ReceiveMessage()
        {
            MessagePacket232 pkMessageIn = (MessagePacket232)Dispatcher.Receive();
            sequence = pkMessageIn.Sequence;
            crn = pkMessageIn.CRN;
            return pkMessageIn.Message;
        }
        internal override void SendMessage(MessageData message)
        {
            MessagePacket232 pkMessageOut = new MessagePacket232(message, sequence, crn);
            Dispatcher.Send(pkMessageOut);
        }
        internal override void BroadcastSequence(params byte[] byteSequence)
        {
            BroadcastPacket232 pkBroadcast = new BroadcastPacket232(byteSequence);
            Dispatcher.Send(pkBroadcast);
        }
    }
    internal sealed class Transceiver485 : Transceiver
    {
        private const int enquiryIntervalMS = 20;
        private RS485Addresses address;

        private IndicatorPacket485 pkEnquiry;
        private IndicatorPacket485 pkBroadcastEnquiry;
        private bool isBroadcastAnnounced;

        internal Transceiver485(Dispatcher dispatcher, RS485Addresses address) : base(dispatcher)
        {
            this.address = address;
            pkRequestHello = new IndicatorPacket485(IndicatorControlBytes.DLE_StatusRequest, address);
            pkRequestHelloImmediate = new IndicatorPacket485(IndicatorControlBytes.DC1_StatusRequest, address);
            pkAcknowledged = new IndicatorPacket485(IndicatorControlBytes.ACK_Acknowledged, address);
            pkNotAcknowledged = new IndicatorPacket485(IndicatorControlBytes.NAK_NotAcknowledged, address);
            pkEnquiry = new IndicatorPacket485(IndicatorControlBytes.ENQ_Enquiry, address);
            pkBroadcastEnquiry = new IndicatorPacket485(IndicatorControlBytes.ENQ_Enquiry, RS485Addresses.ALL);
        }

        internal override IndicatorControlBytes ReceiveIndicator()
        {
            IndicatorPacket485 pkIndicatorIn = (IndicatorPacket485)Dispatcher.Receive();
            return pkIndicatorIn.Indicator;
        }
        internal override MessageData ReceiveMessage()
        {
            MessagePacket485 pkMessageIn;
            int numEnquiries = 0;
            // Knowing enquiry interval we convert default timeout duration to a number of enquiries.
            int maxEnquiries = Dispatcher.ReadTimeout / enquiryIntervalMS;
            // Keep sending enquiry at given intervals while waiting for some data to arrive to a serial port buffer.
            while (numEnquiries++ < maxEnquiries && Dispatcher.IsInBufferEmpty)
            {
                Dispatcher.Send(pkEnquiry);
                SpinWait.SpinUntil(() => !Dispatcher.IsInBufferEmpty, enquiryIntervalMS);
            }
            // We might have used whole timeout duration for enquiry so either receive data or let it throw a timeout exception.
            pkMessageIn = (MessagePacket485)Dispatcher.Receive(200); // Override default timeout.
            isBroadcastAnnounced = false; // Receiving a message indicates that broadcast session has ended, if any.
            sequence = pkMessageIn.Sequence;
            crn = pkMessageIn.CRN;
            return pkMessageIn.Message;

            // Obsolete: timeout exception based approach.
            /*
            while (true)
            {
                Dispatcher.Send(pkEnquiry);
                try
                {
                    pkMessageIn = (MessagePacket485)Dispatcher.Receive(enquiryIntervalMS);
                    sequence = pkMessageIn.Sequence;
                    crn = pkMessageIn.CRN;
                    return pkMessageIn.Message;
                }
                catch (TimeoutException)
                {
                    if (numEnquiries++ < maxEnquiries)
                    {
                        continue;
                    }
                    throw;
                }
            }
            */
        }
        internal override void SendMessage(MessageData message)
        {
            MessagePacket485 pkMessageOut = new MessagePacket485(message, sequence, crn, address);
            Dispatcher.Send(pkMessageOut);
        }
        internal override void BroadcastSequence(params byte[] byteSequence)
        {
            // We must announce broadcasting by sending special non-addressed ENQ packet first (RS485 only).
            // This is done only once per broadcast session and is reset when any message is received.
            if (!isBroadcastAnnounced)
            {
                Dispatcher.Send(pkBroadcastEnquiry);
                isBroadcastAnnounced = true;
            }
            BroadcastPacket485 pkBroadcast = new BroadcastPacket485(byteSequence);
            Dispatcher.Send(pkBroadcast);
        }
    }
}