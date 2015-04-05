using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ECommon.Components;
using ECommon.Logging;
using ECommon.Socketing;
using ECommon.TcpTransport.Framing;
using ECommon.Utilities;

namespace ECommon.TcpTransport
{
    public class TcpClient
    {
        private ITcpConnection _connection;
        private IMessageFramer _framer;
        private readonly IPEndPoint _localEndPoint;
        private readonly IPEndPoint _serverEndPoint;
        private readonly ISocketClientEventListener _eventListener;
        private readonly Action<byte[]> _replyHandler;
        private readonly ManualResetEvent _waitHandle;
        private readonly ILogger _logger;

        public bool IsStopped { get; private set; }
        public TcpConnectionStatus ConnectionStatus { get; private set; }

        public TcpClient(IPEndPoint serverEndPoint, Action<byte[]> replyHandler, ISocketClientEventListener eventListener = null)
            : this(null, serverEndPoint, replyHandler, eventListener)
        {
        }
        public TcpClient(IPEndPoint localEndPoint, IPEndPoint serverEndPoint, Action<byte[]> replyHandler, ISocketClientEventListener eventListener = null)
        {
            Ensure.NotNull(serverEndPoint, "serverEndPoint");
            Ensure.NotNull(replyHandler, "replyHandler");
            _localEndPoint = localEndPoint;
            _serverEndPoint = serverEndPoint;
            _replyHandler = replyHandler;
            _eventListener = eventListener;
            _framer = new LengthPrefixMessageFramer();
            _framer.RegisterMessageArrivedCallback(OnMessageArrived);
            _waitHandle = new ManualResetEvent(false);
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        public void Start(int connectTimeoutMilliseconds = 5000)
        {
            _connection = new TcpClientConnector().ConnectTo(Guid.NewGuid(), _localEndPoint, _serverEndPoint, OnConnectionEstablished, OnConnectionFailed);
            _connection.ConnectionClosed += OnConnectionClosed;
            _connection.ReceiveAsync(OnRawDataReceived);
            _waitHandle.WaitOne(connectTimeoutMilliseconds);
        }
        public void Stop()
        {
            IsStopped = true;
            _connection.Close();
            _connection.ConnectionClosed -= OnConnectionClosed;
        }
        public void ReconnectToServer()
        {
            _connection.ConnectionClosed -= OnConnectionClosed;
            _connection = new TcpClientConnector().ConnectTo(Guid.NewGuid(), _localEndPoint, _serverEndPoint, OnConnectionEstablished, OnConnectionFailed);
            _connection.ConnectionClosed += OnConnectionClosed;
            _connection.ReceiveAsync(OnRawDataReceived);
        }
        public void SendAsync(byte[] message)
        {
            _connection.SendAsync(_framer.FrameData(new ArraySegment<byte>(message, 0, message.Length)));
        }

        private void OnConnectionEstablished(ITcpConnection connection)
        {
            ConnectionStatus = TcpConnectionStatus.Established;
            _logger.InfoFormat("TCP connection established: [remoteEndPoint:{0}, localEndPoint:{1}, connectionId:{2:B}].", connection.RemoteEndPoint, connection.LocalEndPoint, connection.ConnectionId);
            _waitHandle.Set();
            if (_eventListener != null)
            {
                _eventListener.OnConnectionEstablished(connection);
            }
        }
        private void OnConnectionFailed(ITcpConnection connection, SocketError socketError)
        {
            ConnectionStatus = TcpConnectionStatus.Failed;
            _logger.InfoFormat("TCP connection failed: [remoteEndPoint:{0}, localEndPoint:{1}, connectionId:{2:B}, socketError:{3}].", connection.RemoteEndPoint, connection.LocalEndPoint, connection.ConnectionId, socketError);
            if (_eventListener != null)
            {
                _eventListener.OnConnectionFailed(connection, socketError);
            }
        }
        private void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
        {
            ConnectionStatus = TcpConnectionStatus.Closed;
            _logger.InfoFormat("TCP connection closed: [remoteEndPoint:{0}, localEndPoint:{1}, connectionId:{2:B}, socketError:{3}].", connection.RemoteEndPoint, connection.LocalEndPoint, connection.ConnectionId, socketError);
            if (_eventListener != null)
            {
                _eventListener.OnConnectionClosed(connection, socketError);
            }
        }
        private void OnRawDataReceived(ITcpConnection connection, IEnumerable<ArraySegment<byte>> data)
        {
            try
            {
                _framer.UnFrameData(data);
            }
            catch (PackageFramingException ex)
            {
                _logger.Error("UnFrame data has exception.", ex);
                return;
            }
            connection.ReceiveAsync(OnRawDataReceived);
        }
        private void OnMessageArrived(ArraySegment<byte> message)
        {
            byte[] reply = new byte[message.Count];
            Array.Copy(message.Array, message.Offset, reply, 0, message.Count);
            _replyHandler(reply);
        }
    }
    public enum TcpConnectionStatus
    {
        NotEstablished,
        Established,
        Failed,
        Closed
    }
}
