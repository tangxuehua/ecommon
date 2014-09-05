using System;
using System.Collections.Generic;
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
            ReceiveInternal(new ReceiveState(sourceSocket, 6, messageReceivedCallback));
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
            var receivedData = receiveState.Data;
            var bytesRead = 0;
            if (!sourceSocket.Connected)
            {
                _logger.ErrorFormat("Socket disconnected, address:" + sourceSocketInfo.SocketRemotingEndpointAddress);
                return;
            }

            SafeSocketOperation("EndReceive", sourceSocketInfo, () => bytesRead = sourceSocket.EndReceive(asyncResult));

            if (bytesRead <= 0)
            {
                _logger.ErrorFormat("Socket EndReceive completed, but no bytes were read, stop to receive data again, source socket address:{0}, receiveSize:{1}", sourceSocketInfo.SocketRemotingEndpointAddress, receiveState.ReceiveSize);
                return;
            }

            //Receive a new message
            if (receiveState.MessageSize == null)
            {
                if (bytesRead < 6)
                {
                    for (var index = 0; index < bytesRead; index++)
                    {
                        receivedData.Add(receiveState.Buffer[index]);
                    }
                    var remainSize = 6 - receivedData.Count;
                    if (remainSize > 0)
                    {
                        _logger.DebugFormat("Received part of message header, receivedSize:{0}, remainSize:{1}, bytesRead:{2}", receivedData.Count, remainSize, bytesRead);
                        receiveState.ReceiveSize = remainSize;
                        ReceiveInternal(receiveState);
                    }
                    else
                    {
                        receiveState.MessageSize = SocketUtils.ParseMessageLength(receivedData.ToArray());
                        _logger.Debug("Receive New Message, Size:" + receiveState.MessageSize + ", bytesRead:" + bytesRead);
                        var size = receiveState.MessageSize <= ReceiveState.BufferSize ? receiveState.MessageSize.Value : ReceiveState.BufferSize;
                        receivedData.Clear();
                        receiveState.ClearBuffer();
                        receiveState.ReceiveSize = size;
                        ReceiveInternal(receiveState);
                    }
                }
                else
                {
                    receiveState.MessageSize = SocketUtils.ParseMessageLength(receiveState.Buffer);
                    _logger.Debug("Receive New Message, Size:" + receiveState.MessageSize + ", bytesRead:" + bytesRead);
                    var size = receiveState.MessageSize <= ReceiveState.BufferSize ? receiveState.MessageSize.Value : ReceiveState.BufferSize;
                    receivedData.Clear();
                    receiveState.ClearBuffer();
                    receiveState.ReceiveSize = size;
                    ReceiveInternal(receiveState);
                }
            }
            //Receive data of the current message.
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
                        _logger.Error(string.Format("Exception raised when calling MessageReceivedCallback, source socket:{0}", sourceSocketInfo.SocketRemotingEndpointAddress), ex);
                    }

                    receiveState.MessageSize = null;
                    receivedData.Clear();
                    receiveState.ClearBuffer();
                    receiveState.ReceiveSize = 6;
                    ReceiveInternal(receiveState);
                }
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
