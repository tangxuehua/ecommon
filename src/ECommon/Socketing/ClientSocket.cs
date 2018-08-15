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
            get { return Connection != null && Connection.IsConnected; }
        }
        public string Name { get; private set; }
        public TcpConnection Connection { get; private set; }

        public ClientSocket(string name, EndPoint serverEndPoint, EndPoint localEndPoint, SocketSetting setting, IBufferPool receiveDataBufferPool, Action<ITcpConnection, byte[]> messageArrivedHandler)
        {
            Ensure.NotNull(serverEndPoint, "serverEndPoint");
            Ensure.NotNull(setting, "setting");
            Ensure.NotNull(receiveDataBufferPool, "receiveDataBufferPool");
            Ensure.NotNull(messageArrivedHandler, "messageArrivedHandler");

            Name = name;
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

            if (Connection == null)
            {
                throw new Exception(string.Format("Client socket connect failed or timeout, name: {0}, serverEndPoint: {1}", Name, _serverEndPoint));
            }

            return this;
        }
        public ClientSocket QueueMessage(byte[] message)
        {
            Connection.QueueMessage(message);
            FlowControlIfNecessary();
            return this;
        }
        public ClientSocket Shutdown()
        {
            if (Connection != null)
            {
                Connection.Close();
                Connection = null;
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
            var pendingMessageCount = Connection.PendingMessageCount;
            if (_flowControlThreshold > 0 && pendingMessageCount >= _flowControlThreshold)
            {
                Thread.Sleep(1);
                var flowControlTimes = Interlocked.Increment(ref _flowControlTimes);
                if (flowControlTimes % 10000 == 0)
                {
                    _logger.InfoFormat("Client socket send data flow control, name: {0}, pendingMessageCount: {1}, flowControlThreshold: {2}, flowControlTimes: {3}", Name, pendingMessageCount, _flowControlThreshold, flowControlTimes);
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
                _logger.ErrorFormat("Client socket connect failed, name: {0}, remotingServerEndPoint: {1}, socketError: {2}", Name, _serverEndPoint, e.SocketError);
                OnConnectionFailed(_serverEndPoint, e.SocketError);
                _waitConnectHandle.Set();
                return;
            }

            Connection = new TcpConnection(Name, _socket, _setting, _receiveDataBufferPool, OnMessageArrived, OnConnectionClosed);

            _logger.InfoFormat("Client socket connected, name: {0}, remotingServerEndPoint: {1}, localEndPoint: {2}", Name, Connection.RemotingEndPoint, Connection.LocalEndPoint);

            OnConnectionEstablished(Connection);

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
                _logger.Error(string.Format("Client socket handle message has exception, name: {0}", Name), ex);
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
                    _logger.Error(string.Format("Client socket notify connection established has exception, name: {0}, listenerType: {1}", Name, listener.GetType().Name), ex);
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
                    _logger.Error(string.Format("Client socket notify connection failed has exception, name: {0}, listenerType: {1}", Name, listener.GetType().Name), ex);
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
                    _logger.Error(string.Format("Client socket notify connection closed has exception, name: {0}, listenerType: {1}", Name, listener.GetType().Name), ex);
                }
            }
        }
    }
}
