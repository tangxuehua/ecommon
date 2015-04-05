using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ECommon.Components;
using ECommon.Configurations;
using ECommon.Logging;
using ECommon.Socketing;
using ECommon.TcpTransport.Framing;
using ECommon.Utilities;

namespace ECommon.TcpTransport
{
    public class TcpServerListener
    {
        private readonly ILogger _logger;
        private readonly IPEndPoint _serverEndPoint;
        private readonly Socket _listeningSocket;
        private readonly SocketArgsPool _acceptSocketArgsPool;
        private readonly ISocketServerEventListener _eventListener;
        private readonly Action<ITcpConnection, byte[], Action<byte[]>> _messageHandler;

        public TcpServerListener(IPEndPoint serverEndPoint, ISocketServerEventListener eventListener, Action<ITcpConnection, byte[], Action<byte[]>> messageHandler)
        {
            Ensure.NotNull(serverEndPoint, "serverEndPoint");
            Ensure.NotNull(messageHandler, "messageHandler");
            _serverEndPoint = serverEndPoint;
            _eventListener = eventListener;
            _messageHandler = messageHandler;
            _listeningSocket = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _acceptSocketArgsPool = new SocketArgsPool("TcpServerListener.AcceptSocketArgsPool", Configuration.Instance.Setting.TcpConfiguration.ConcurrentAccepts * 2, CreateAcceptSocketArgs);
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        public void Start()
        {
            _logger.InfoFormat("Starting listening on TCP endpoint: {0}.", _serverEndPoint);
            try
            {
                _listeningSocket.Bind(_serverEndPoint);
                _listeningSocket.Listen(Configuration.Instance.Setting.TcpConfiguration.AcceptBacklogCount);
            }
            catch (Exception)
            {
                _logger.InfoFormat("Failed to listen on TCP endpoint: {0}.", _serverEndPoint);
                Helper.EatException(() => _listeningSocket.Close(Configuration.Instance.Setting.TcpConfiguration.SocketCloseTimeoutMs));
                throw;
            }

            for (int i = 0; i < Configuration.Instance.Setting.TcpConfiguration.ConcurrentAccepts; ++i)
            {
                StartAccepting();
            }
        }
        public void Stop()
        {
            Helper.EatException(() => _listeningSocket.Close(Configuration.Instance.Setting.TcpConfiguration.SocketCloseTimeoutMs));
        }

        private SocketAsyncEventArgs CreateAcceptSocketArgs()
        {
            var socketArgs = new SocketAsyncEventArgs();
            socketArgs.Completed += AcceptCompleted;
            return socketArgs;
        }
        private void StartAccepting()
        {
            var socketArgs = _acceptSocketArgsPool.Get();
            try
            {
                var firedAsync = _listeningSocket.AcceptAsync(socketArgs);
                if (!firedAsync)
                    ProcessAccept(socketArgs);
            }
            catch (ObjectDisposedException)
            {
                HandleBadAccept(socketArgs);
            }
        }
        private void AcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                HandleBadAccept(e);
            }
            else
            {
                var acceptSocket = e.AcceptSocket;
                e.AcceptSocket = null;
                _acceptSocketArgsPool.Return(e);

                OnSocketAccepted(acceptSocket);
            }
            StartAccepting();
        }
        private void HandleBadAccept(SocketAsyncEventArgs socketArgs)
        {
            Helper.EatException(
                () =>
                {
                    if (socketArgs.AcceptSocket != null) // avoid annoying exceptions
                        socketArgs.AcceptSocket.Close(Configuration.Instance.Setting.TcpConfiguration.SocketCloseTimeoutMs);
                });
            socketArgs.AcceptSocket = null;
            _acceptSocketArgsPool.Return(socketArgs);
        }
        private void OnSocketAccepted(Socket socket)
        {
            IPEndPoint socketEndPoint;
            try
            {
                socketEndPoint = (IPEndPoint)socket.RemoteEndPoint;
            }
            catch (Exception ex)
            {
                _logger.Error("TCP connection accepted, but get remote endpoint of this connection failed.", ex);
                return;
            }

            var connection = TcpConnection.CreateAcceptedTcpConnection(Guid.NewGuid(), socketEndPoint, socket);
            new AcceptedConnection(connection, _eventListener, _messageHandler).StartReceiving();
            _logger.InfoFormat("TCP connection accepted: [remoteEndPoint:{0}, localEndPoint:{1}, connectionId:{2:B}].", connection.RemoteEndPoint, connection.LocalEndPoint, connection.ConnectionId);
            if (_eventListener != null)
            {
                _eventListener.OnConnectionAccepted(connection);
            }
        }

        class AcceptedConnection
        {
            private readonly ILogger _logger;
            private readonly ISocketServerEventListener _eventListener;
            private readonly Action<ITcpConnection, byte[], Action<byte[]>> _messageHandler;
            private readonly ITcpConnection _connection;
            private readonly IMessageFramer _messageFramer;
            private int _isClosed;

            public AcceptedConnection(ITcpConnection connection, ISocketServerEventListener eventListener, Action<ITcpConnection, byte[], Action<byte[]>> messageHandler)
            {
                _messageFramer = new LengthPrefixMessageFramer();
                _messageFramer.RegisterMessageArrivedCallback(OnMessageArrived);
                _connection = connection;
                _eventListener = eventListener;
                _messageHandler = messageHandler;
                _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(typeof(TcpServerListener).FullName);

                _connection.ConnectionClosed += OnConnectionClosed;
                if (_connection.IsClosed)
                {
                    OnConnectionClosed(_connection, SocketError.Success);
                    return;
                }
            }

            public void StartReceiving()
            {
                _connection.ReceiveAsync(OnRawDataReceived);
            }

            private void OnRawDataReceived(ITcpConnection connection, IEnumerable<ArraySegment<byte>> data)
            {
                try
                {
                    _messageFramer.UnFrameData(data);
                }
                catch (PackageFramingException ex)
                {
                    _logger.Error("Unframe data has exception.", ex);
                    return;
                }
                connection.ReceiveAsync(OnRawDataReceived);
            }
            private void OnMessageArrived(ArraySegment<byte> message)
            {
                byte[] data = new byte[message.Count];
                Array.Copy(message.Array, message.Offset, data, 0, message.Count);
                _messageHandler(_connection, data, reply => _connection.SendAsync(_messageFramer.FrameData(new ArraySegment<byte>(reply, 0, reply.Length))));
            }
            private void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
            {
                if (Interlocked.CompareExchange(ref _isClosed, 1, 0) == 0)
                {
                    _logger.InfoFormat("TCP connection closed: [remoteEndPoint:{0}, localEndPoint:{1}, connectionId:{2:B}, socketError:{3}].", connection.RemoteEndPoint, connection.LocalEndPoint, connection.ConnectionId, socketError);
                    if (_eventListener != null)
                    {
                        _eventListener.OnConnectionClosed(connection, socketError);
                    }
                }
            }
        }
    }
}