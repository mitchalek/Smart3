using System;

namespace Smart3.Protocols
{
    internal sealed class Console
    {
        private const int maxRetries = 3;
        private Transceiver transceiver;
        internal Console(Transceiver transceiver)
        {
            if (transceiver == null) throw new ArgumentNullException(nameof(transceiver));
            this.transceiver = transceiver;
        }

        private bool CheckContract(Delegate party, MessageData message)
        {
            foreach (var attribute in Attribute.GetCustomAttributes(party.Method))
            {
                if (attribute.GetType() == typeof(MessageTypeContractAttribute) && ((MessageTypeContractAttribute)attribute).Type == message.Type)
                {
                    return true;
                }
            }
            return false;
        }
        private void EnforceContract(Delegate party, MessageData message)
        {
            if (!CheckContract(party, message))
            {
                throw new ProtocolException($"Message type contract violation. Method {party.Method.DeclaringType.Name}.{party.Method.Name} is not allowed to handle a message of type {message.Type}.");
            }
        }

        private void AnswerQuestion(Func<MessageData, MessageData> answerer, MessageData question)
        {
            MessageData answer = answerer(question);
            bool protocolError;
            for (int i = 0; i <= maxRetries; i++)
            {
                transceiver.SendMessage(answer);
                protocolError = false;
                while (!protocolError)
                {
                    switch (transceiver.ReceiveIndicator())
                    {
                        case IndicatorControlBytes.ACK_Acknowledged:
                            return;
                        case IndicatorControlBytes.NAK_NotAcknowledged:
                            protocolError = true;
                            break;
                        case IndicatorControlBytes.SYN_Synchronize:
                            break;
                        case IndicatorControlBytes.BEL_PaperOut:
                            break;
                        case IndicatorControlBytes.CAN_OperationAborted:
                            throw new CashRegisterException("Cash register was unable to complete the request.");
                        default:
                            throw new ProtocolException("Invalid control byte received.");
                    }
                }
            }
            throw new ProtocolException("Message write retry timeout exceeded on CRC error.");
        }

        internal void Hello(bool immediateMode)
        {
            transceiver.SendHelloRequest(immediateMode);
        }
        internal void Listen(Action<MessageData> listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    MessageData message = transceiver.ReceiveMessage();
                    EnforceContract(listener, message);
                    transceiver.SendACK();
                    listener(message);
                    return;
                }
                catch (PacketValidationException)
                {
                    transceiver.SendNAK();
                }
            }
            throw new ProtocolException("Message read retry timeout exceeded on CRC error.");
        }
        internal void AnswerAny(params Func<MessageData, MessageData>[] answerers)
        {
            if (answerers == null) throw new ArgumentNullException(nameof(answerers));
            var question = transceiver.ReceiveMessage();
            for (int i = 0; i < answerers.Length; i++)
            {
                if (answerers[i] != null && CheckContract(answerers[i], question))
                {
                    AnswerQuestion(answerers[i], question);
                    return;
                }
            }
            throw new ProtocolException($"Message type contract violation. Could not find a handler (out of {answerers.Length}) for a message type {question.Type}.");
        }
        internal void Answer(Func<MessageData, MessageData> answerer)
        {
            if (answerer == null) throw new ArgumentNullException(nameof(answerer));
            var question = transceiver.ReceiveMessage();
            AnswerQuestion(answerer, question);
        }
        internal void Broadcast(params byte[] byteSequence)
        {
            transceiver.BroadcastSequence(byteSequence);
        }
        internal void Swallow()
        {
            transceiver.ReceiveMessage();
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    internal sealed class MessageTypeContractAttribute : Attribute
    {
        internal readonly string Type;
        internal MessageTypeContractAttribute(string type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            Type = type;
        }
    }
}