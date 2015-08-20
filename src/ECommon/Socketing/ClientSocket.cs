using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ECommon.Components;
using ECommon.Logging;
using ECommon.Utilities;

namespace ECommon.Socketing
{
    public class ClientSocket
    {
        #region Private Variables

        private EndPoint _serverEndPoint;
        private EndPoint _localEndPoint;
        private Socket _socket;
        private TcpConnection _connection;
        private readonly IList<IConnectionEventListener> _connectionEventListeners;
        private readonly Action<ITcpConnection, byte[]> _messageArrivedHandler;
        private readonly ILogger _logger;
        private readonly ManualResetEvent _waitConnectHandle;

        #endregion

        public bool IsConnected
        {
            get { return _socket.Connected; }
        }
        public Socket Socket
        {
            get { return _socket; }
        }

        public ClientSocket(EndPoint serverEndPoint, EndPoint localEndPoint, Action<ITcpConnection, byte[]> messageArrivedHandler)
        {
            Ensure.NotNull(serverEndPoint, "serverEndPoint");
            Ensure.NotNull(messageArrivedHandler, "messageArrivedHandler");

            _connectionEventListeners = new List<IConnectionEventListener>();

            _serverEndPoint = serverEndPoint;
            _localEndPoint = localEndPoint;
            _messageArrivedHandler = messageArrivedHandler;
            _waitConnectHandle = new ManualResetEvent(false);
            _socket = SocketUtils.CreateSocket();
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        public ClientSocket RegisterConnectionEventListener(IConnectionEventListener listener)
        {
            _connectionEventListeners.Add(listener);
            return this;
        }
        public ClientSocket Start()
        {
            var socketArgs = new SocketAsyncEventArgs();
            socketArgs.AcceptSocket = _socket;
            socketArgs.RemoteEndPoint = _serverEndPoint;
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

            _waitConnectHandle.WaitOne(5000);

            return this;
        }
        public ClientSocket SendAsync(byte[] message)
        {
            _connection.Send(message);
            return this;
        }
        public ClientSocket Shutdown()
        {
            SocketUtils.ShutdownSocket(_socket);
            return this;
        }

        private void OnConnectAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessConnect(e);
        }
        private void ProcessConnect(SocketAsyncEventArgs e)
        {
            e.AcceptSocket = null;

            if (e.SocketError != SocketError.Success)
            {
                SocketUtils.ShutdownSocket(_socket);
                _logger.InfoFormat("Socket connect failed, socketError:{0}", e.SocketError);
                OnConnectionFailed(e.SocketError);
                return;
            }

            _connection = new TcpConnection(_socket, OnMessageArrived, OnConnectionClosed);

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
        private void OnConnectionFailed(SocketError socketError)
        {
            foreach (var listener in _connectionEventListeners)
            {
                try
                {
                    listener.OnConnectionFailed(socketError);
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
