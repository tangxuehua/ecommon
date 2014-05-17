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
            ReceiveInternal(new ReceiveState(sourceSocket, messageReceivedCallback), 4);
        }

        private void ReceiveInternal(ReceiveState receiveState, int size)
        {
            SafeSocketOperation("BeginReceive", receiveState.SourceSocket, () =>
            {
                receiveState.SourceSocket.InnerSocket.BeginReceive(receiveState.Buffer, 0, size, 0, ReceiveCallback, receiveState);
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
                return;
            }

            SafeSocketOperation("EndReceive", sourceSocketInfo, () => bytesRead = sourceSocket.EndReceive(asyncResult));

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
                _logger.Error(string.Format("Socket {0} has unkonwn exception, remoting endpoint address:{1}",
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
