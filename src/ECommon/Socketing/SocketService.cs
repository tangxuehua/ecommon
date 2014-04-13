using System;
using System.Net.Sockets;
using ECommon.IoC;
using ECommon.Logging;

namespace ECommon.Socketing
{
    public class SocketService
    {
        private ILogger _logger;
        private Action<SocketInfo, Exception> _socketReceiveExceptionAction;

        public SocketService(Action<SocketInfo, Exception> socketReceiveExceptionAction)
        {
            _socketReceiveExceptionAction = socketReceiveExceptionAction;
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }
        public void SendMessage(SocketInfo socketInfo, byte[] message, Action<SendResult> messageSentCallback)
        {
            var wrappedMessage = SocketUtils.BuildMessage(message);
            if (wrappedMessage.Length > 0)
            {
                SafeSocketOperation("BeginSend", socketInfo.SocketRemotingEndpointAddress, () =>
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
            ReceiveInternal(new ReceiveState(sourceSocket, messageReceivedCallback), 4);
        }

        private void ReceiveInternal(ReceiveState receiveState, int size)
        {
            SafeSocketOperation("BeginReceive", receiveState.SourceSocket.SocketRemotingEndpointAddress, () =>
            {
                receiveState.SourceSocket.InnerSocket.BeginReceive(receiveState.Buffer, 0, size, 0, ReceiveCallback, receiveState);
            });
        }
        private void SendCallback(IAsyncResult asyncResult)
        {
            var sendContext = (SendContext)asyncResult.AsyncState;
            try
            {
                sendContext.TargetSocket.InnerSocket.EndSend(asyncResult);
                sendContext.MessageSendCallback(new SendResult(true, null));
            }
            catch (SocketException socketException)
            {
                _logger.Error(string.Format("Socket EndSend has socket exception, remoting endpoint address:{0}, errorCode:{1}",
                    sendContext.TargetSocket.SocketRemotingEndpointAddress,
                    socketException.SocketErrorCode), socketException);
                sendContext.MessageSendCallback(new SendResult(false, socketException));
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Socket EndSend has unkonwn exception, remoting endpoint address:{1}", sendContext.TargetSocket.SocketRemotingEndpointAddress), ex);
                sendContext.MessageSendCallback(new SendResult(false, ex));
            }
        }
        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            var receiveState = (ReceiveState)asyncResult.AsyncState;
            var sourceSocketInfo = receiveState.SourceSocket;
            var sourceSocket = sourceSocketInfo.InnerSocket;
            var receivedData = receiveState.Data;
            var bytesRead = 0;
            if (!sourceSocket.Connected)
            {
                return;
            }

            try
            {
                bytesRead = sourceSocket.EndReceive(asyncResult);
            }
            catch (SocketException socketException)
            {
                _logger.Error(string.Format("Socket EndReceive has socket exception, remoting endpoint address:{0}, errorCode:{1}",
                    sourceSocketInfo.SocketRemotingEndpointAddress,
                    socketException.SocketErrorCode), socketException);
                if (_socketReceiveExceptionAction != null)
                {
                    _socketReceiveExceptionAction(sourceSocketInfo, socketException);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Socket EndReceive has unkonwn exception, remoting endpoint address:{0}", sourceSocketInfo.SocketRemotingEndpointAddress), ex);
                if (_socketReceiveExceptionAction != null)
                {
                    _socketReceiveExceptionAction(sourceSocketInfo, ex);
                }
            }

            if (bytesRead > 0)
            {
                if (receiveState.MessageSize == null)
                {
                    receiveState.MessageSize = SocketUtils.ParseMessageLength(receiveState.Buffer);
                    var size = receiveState.MessageSize <= ReceiveState.BufferSize ? receiveState.MessageSize.Value : ReceiveState.BufferSize;
                    ReceiveInternal(receiveState, size);
                }
                else
                {
                    for (var index = 0; index < bytesRead; index++)
                    {
                        receivedData.Add(receiveState.Buffer[index]);
                    }
                    if (receivedData.Count < receiveState.MessageSize.Value)
                    {
                        var remainSize = receiveState.MessageSize.Value - receivedData.Count;
                        var size = remainSize <= ReceiveState.BufferSize ? remainSize : ReceiveState.BufferSize;
                        ReceiveInternal(receiveState, size);
                    }
                    else
                    {
                        receiveState.MessageReceivedCallback(receivedData.ToArray());
                        receiveState.MessageSize = null;
                        receivedData.Clear();
                        ReceiveInternal(receiveState, 4);
                    }
                }
            }
        }
        private void SafeSocketOperation(string operationName, string socketRemotingEndpointAddress, Action action)
        {
            try
            {
                action();
            }
            catch (SocketException socketException)
            {
                _logger.Error(string.Format("Socket {0} has socket exception, remoting endpoint address:{1}, errorCode:{2}",
                    operationName,
                    socketRemotingEndpointAddress,
                    socketException.SocketErrorCode), socketException);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Socket {0} has unkonwn exception, remoting endpoint address:{1}",
                    operationName,
                    socketRemotingEndpointAddress), ex);
            }
        }
    }
}
