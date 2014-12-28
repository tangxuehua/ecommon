using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ECommon.Utilities;

namespace ECommon.TcpTransport
{
    public class TcpConnectionBase
    {
        private Socket _socket;
        private readonly IPEndPoint _remoteEndPoint;
        private IPEndPoint _localEndPoint;
        private bool _isClosed;

        public IPEndPoint RemoteEndPoint { get { return _remoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return _localEndPoint; } }
        public bool IsInitialized { get { return _socket != null; } }
        public bool IsClosed { get { return _isClosed; } }

        public TcpConnectionBase(IPEndPoint remoteEndPoint)
        {
            Ensure.NotNull(remoteEndPoint, "remoteEndPoint");
            _remoteEndPoint = remoteEndPoint;
        }

        protected void InitConnectionBase(Socket socket)
        {
            Ensure.NotNull(socket, "socket");
            _socket = socket;
            _localEndPoint = Helper.EatException(() => (IPEndPoint)socket.LocalEndPoint);
        }
        protected void NotifyClosed()
        {
            _isClosed = true;
        }
    }
}