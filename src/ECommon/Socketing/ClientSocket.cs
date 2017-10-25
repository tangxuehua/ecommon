using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ECommon.Components;
using ECommon.Logging;
using ECommon.Socketing.BufferManagement;
using ECommon.Utilities;

namespace ECommon.Socketing
{
    public class ClientSocket
    {
        #region Private Variables

        private readonly EndPoint _serverEndPoint;
        private readonly EndPoint _localEndPoint;
        private Socket _socket;
        private TcpConnection _connection;
        private readonly SocketSetting _setting;
        private readonly IList<IConnectionEventListener> _connectionEventListeners;
        private readonly Action<ITcpConnection, byte[]> _messageArrivedHandler;
        private readonly IBufferPool _receiveDataBufferPool;
        private readonly ILogger _logger;
        private readonly ManualResetEvent _waitConnectHandle;
        private readonly int _flowControlThreshold;
        private long _flowControlTimes;

        #endregion

        public bool IsConnected
        {
            get { return _connection != null && _connection.IsConnected; }
        }
        public TcpConnection Connection
        {
            get { return _connection; }
        }

        public ClientSocket(EndPoint serverEndPoint, EndPoint localEndPoint, SocketSetting setting, IBufferPool receiveDataBufferPool, Action<ITcpConnection, byte[]> messageArrivedHandler)
        {
            Ensure.NotNull(serverEndPoint, "serverEndPoint");
            Ensure.NotNull(setting, "setting");
            Ensure.NotNull(receiveDataBufferPool, "receiveDataBufferPool");
            Ensure.NotNull(messageArrivedHandler, "messageArrivedHandler");

            _connectionEventListeners = new List<IConnectionEventListener>();

            _serverEndPoint = serverEndPoint;
            _localEndPoint = localEndPoint;
            _setting = setting;
            _receiveDataBufferPool = receiveDataBufferPool;
            _messageArrivedHandler = messageArrivedHandler;
            _waitConnectHandle = new ManualResetEvent(false);
            _socket = SocketUtils.CreateSocket(_setting.SendBufferSize, _setting.ReceiveBufferSize);
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
            _flowControlThreshold = _setting.SendMessageFlowControlThreshold;
        }

        public ClientSocket RegisterConnectionEventListener(IConnectionEventListener listener)
        {
            _connectionEventListeners.Add(listener);
            return this;
        }
        public ClientSocket Start(int waitMilliseconds = 5000)
        {
            var socketArgs = new SocketAsyncEventArgs
            {
                AcceptSocket = _socket,
                RemoteEndPoint = _serverEndPoint
            };
            socketArgs.Completed += OnConnectAsyncCompleted;
            if (_localEndPoint != null)
            {
                _socket.Bind(_localEndPoint);
            }

            var firedAsync = _socket.ConnectAsync(socketArgs);
            if (!firedAsync)
            {
                ProcessConnect(socketArgs);
            }

            _waitConnectHandle.WaitOne(waitMilliseconds);

            return this;
        }
        public ClientSocket QueueMessage(byte[] message)
        {
            _connection.QueueMessage(message);
            FlowControlIfNecessary();
            return this;
        }
        public ClientSocket Shutdown()
        {
            if (_connection != null)
            {
                _connection.Close();
                _connection = null;
            }
            else
            {
                SocketUtils.ShutdownSocket(_socket);
                _socket = null;
            }
            return this;
        }

        private void FlowControlIfNecessary()
        {
            var pendingMessageCount = _connection.PendingMessageCount;
            if (_flowControlThreshold > 0 && pendingMessageCount >= _flowControlThreshold)
            {
                Thread.Sleep(1);
                var flowControlTimes = Interlocked.Increment(ref _flowControlTimes);
                if (flowControlTimes % 10000 == 0)
                {
                    _logger.InfoFormat("Send socket data flow control, pendingMessageCount: {0}, flowControlThreshold: {1}, flowControlTimes: {2}", pendingMessageCount, _flowControlThreshold, flowControlTimes);
                }
            }
        }
        private void OnConnectAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessConnect(e);
        }
        private void ProcessConnect(SocketAsyncEventArgs e)
        {
            e.Completed -= OnConnectAsyncCompleted;
            e.AcceptSocket = null;
            e.RemoteEndPoint = null;
            e.Dispose();

            if (e.SocketError != SocketError.Success)
            {
                SocketUtils.ShutdownSocket(_socket);
                _logger.ErrorFormat("Socket connect failed, remoting server endpoint:{0}, socketError:{1}", _serverEndPoint, e.SocketError);
                OnConnectionFailed(_serverEndPoint, e.SocketError);
                _waitConnectHandle.Set();
                return;
            }

            _connection = new TcpConnection(_socket, _setting, _receiveDataBufferPool, OnMessageArrived, OnConnectionClosed);

            _logger.InfoFormat("Socket connected, remote endpoint:{0}, local endpoint:{1}", _connection.RemotingEndPoint, _connection.LocalEndPoint);

            OnConnectionEstablished(_connection);

            _waitConnectHandle.Set();
        }
        private void OnMessageArrived(ITcpConnection connection, byte[] message)
        {
            try
            {
                _messageArrivedHandler(connection, message);
            }
            catch (Exception ex)
            {
                _logger.Error("Handle message error.", ex);
            }
        }
        private void OnConnectionEstablished(ITcpConnection connection)
        {
            foreach (var listener in _connectionEventListeners)
            {
                try
                {
                    listener.OnConnectionEstablished(connection);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Notify connection established failed, listener type:{0}", listener.GetType().Name), ex);
                }
            }
        }
        private void OnConnectionFailed(EndPoint remotingEndPoint, SocketError socketError)
        {
            foreach (var listener in _connectionEventListeners)
            {
                try
                {
                    listener.OnConnectionFailed(remotingEndPoint, socketError);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Notify connection failed has exception, listener type:{0}", listener.GetType().Name), ex);
                }
            }
        }
        private void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
        {
            foreach (var listener in _connectionEventListeners)
            {
                try
                {
                    listener.OnConnectionClosed(connection, socketError);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Notify connection closed failed, listener type:{0}", listener.GetType().Name), ex);
                }
            }
        }
    }
}
