using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using ECommon.Components;
using ECommon.Logging;

namespace ECommon.Socketing
{
    public class SocketService
    {
        private ILogger _logger;
        private ISocketEventListener _socketEventListener;

        public SocketService(ISocketEventListener socketEventListener)
        {
            _socketEventListener = socketEventListener;
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }
        public void SendMessage(SocketInfo socketInfo, byte[] message, Action<SendResult> messageSentCallback)
        {
            if (message.Length == 0)
            {
                throw new Exception(string.Format("Send message failed, message length cannot be zero, target socket address:{0}", socketInfo.SocketRemotingEndpointAddress));
            }
            var wrappedMessage = SocketUtils.BuildMessage(message);
            if (wrappedMessage.Length > 0)
            {
                SafeSocketOperation("BeginSend", socketInfo, () =>
                {
                    socketInfo.InnerSocket.BeginSend(
                        wrappedMessage,
                        0,
                        wrappedMessage.Length,
                        SocketFlags.None,
                        new AsyncCallback(SendCallback),
                        new SendContext(socketInfo, wrappedMessage, messageSentCallback));
                });
            }
        }
        public void ReceiveMessage(SocketInfo sourceSocket, Action<byte[]> messageReceivedCallback)
        {
            ReceiveInternal(new ReceiveState(sourceSocket, SocketUtils.MessageHeaderLength, messageReceivedCallback));
        }

        private void ReceiveInternal(ReceiveState receiveState)
        {
            SafeSocketOperation("BeginReceive", receiveState.SourceSocket, () =>
            {
                receiveState.SourceSocket.InnerSocket.BeginReceive(receiveState.Buffer, 0, receiveState.ReceiveSize, 0, ReceiveCallback, receiveState);
            });
        }
        private void SendCallback(IAsyncResult asyncResult)
        {
            var sendContext = (SendContext)asyncResult.AsyncState;

            SafeSocketOperation("EndSend", sendContext.TargetSocket, () =>
            {
                sendContext.TargetSocket.InnerSocket.EndSend(asyncResult);
                sendContext.MessageSendCallback(new SendResult(true, null));
            },
            (ex) =>
            {
                sendContext.MessageSendCallback(new SendResult(false, ex));
            });
        }
        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            var receiveState = (ReceiveState)asyncResult.AsyncState;
            var sourceSocketInfo = receiveState.SourceSocket;
            var sourceSocket = sourceSocketInfo.InnerSocket;
            var receivedData = receiveState.ReceivedData;
            var bytesRead = 0;

            if (!sourceSocket.Connected)
            {
                _logger.DebugFormat("Source socket disconnected, address:" + sourceSocketInfo.SocketRemotingEndpointAddress);
                return;
            }

            SafeSocketOperation("EndReceive", sourceSocketInfo, () => bytesRead = sourceSocket.EndReceive(asyncResult));

            if (!sourceSocket.Connected)
            {
                _logger.DebugFormat("Source socket disconnected, address:" + sourceSocketInfo.SocketRemotingEndpointAddress);
                return;
            }

            if (bytesRead <= 0)
            {
                sourceSocketInfo.Close();
                _logger.DebugFormat("Source socket EndReceive completed, but no bytes were read, close the source socket, source socket address:{0}, receiveSize:{1}", sourceSocketInfo.SocketRemotingEndpointAddress, receiveState.ReceiveSize);
                return;
            }

            //Receive the header of the message
            if (receiveState.MessageSize == null)
            {
                if (bytesRead < SocketUtils.MessageHeaderLength)
                {
                    for (var index = 0; index < bytesRead; index++)
                    {
                        receivedData.Add(receiveState.Buffer[index]);
                    }
                    var remainSize = SocketUtils.MessageHeaderLength - receivedData.Count;
                    if (remainSize > 0)
                    {
                        _logger.DebugFormat("Received part of message header, receivedSize:{0}, remainSize:{1}, bytesRead:{2}", receivedData.Count, remainSize, bytesRead);
                        receiveState.ReceiveSize = remainSize;
                        ReceiveInternal(receiveState);
                    }
                    else
                    {
                        StartToReceiveMessageBody(receiveState, receivedData.ToArray(), sourceSocketInfo, bytesRead);
                    }
                }
                else
                {
                    StartToReceiveMessageBody(receiveState, receiveState.Buffer, sourceSocketInfo, bytesRead);
                }
                return;
            }

            //Receive the body of the message.
            for (var index = 0; index < bytesRead; index++)
            {
                receivedData.Add(receiveState.Buffer[index]);
            }
            if (receivedData.Count < receiveState.MessageSize.Value)
            {
                var remainSize = receiveState.MessageSize.Value - receivedData.Count;
                var size = remainSize <= ReceiveState.BufferSize ? remainSize : ReceiveState.BufferSize;
                _logger.DebugFormat("Receive part of Message, receivedSize:{0}, remainSize:{1}, messageSize:{2}", receivedData.Count, remainSize, receiveState.MessageSize.Value);
                receiveState.ReceiveSize = size;
                ReceiveInternal(receiveState);
            }
            else
            {
                try
                {
                    _logger.DebugFormat("Receive last part of Message, bytesRead:{0}, messageSize:{1}", bytesRead, receiveState.MessageSize.Value);
                    receiveState.MessageReceivedCallback(receivedData.ToArray());
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Exception raised when calling MessageReceivedCallback, source socket address:{0}", sourceSocketInfo.SocketRemotingEndpointAddress), ex);
                }

                receiveState.MessageSize = null;
                receivedData.Clear();
                receiveState.ClearBuffer();
                receiveState.ReceiveSize = SocketUtils.MessageHeaderLength;
                ReceiveInternal(receiveState);
            }
        }
        private void StartToReceiveMessageBody(ReceiveState receiveState, byte[] messageHeaderBuffer, SocketInfo sourceSocketInfo, int bytesRead)
        {
            string errorMessage;
            var messageLength = SocketUtils.ParseMessageLength(messageHeaderBuffer, out errorMessage);

            if (messageLength > 0)
            {
                receiveState.MessageSize = messageLength;
                _logger.DebugFormat("Start to receive new message, message body size:{0}, bytesRead:{1}", receiveState.MessageSize, bytesRead);
                var size = receiveState.MessageSize <= ReceiveState.BufferSize ? receiveState.MessageSize.Value : ReceiveState.BufferSize;
                receiveState.ReceivedData.Clear();
                receiveState.ClearBuffer();
                receiveState.ReceiveSize = size;
                ReceiveInternal(receiveState);
            }
            else
            {
                _logger.ErrorFormat("Parse message length failed, source socket address:{0}, receiveSize:{1}, bytesRead:{2}, errorMessage:{3}", sourceSocketInfo.SocketRemotingEndpointAddress, receiveState.ReceiveSize, bytesRead, errorMessage);
            }
        }
        private void SafeSocketOperation(string operationName, SocketInfo socketInfo, Action action, Action<Exception> exceptionHandler = null)
        {
            try
            {
                action();
            }
            catch (SocketException ex)
            {
                if (_socketEventListener != null)
                {
                    Task.Factory.StartNew(() =>
                    {
                        _socketEventListener.OnSocketException(socketInfo, ex);
                    });
                }
                if (exceptionHandler != null)
                {
                    exceptionHandler(ex);
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Socket {0} has unknown exception, remoting endpoint address:{1}",
                    operationName,
                    socketInfo.SocketRemotingEndpointAddress), ex);
                if (exceptionHandler != null)
                {
                    exceptionHandler(ex);
                }
            }
        }
    }
}
