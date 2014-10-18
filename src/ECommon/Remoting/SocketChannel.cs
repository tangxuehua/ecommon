using System;
using System.Net;
using ECommon.TcpTransport;
using ECommon.Utilities;

namespace ECommon.Remoting
{
    public class SocketChannel : ISocketChannel
    {
        private ITcpConnection _connection;

        public SocketChannel(ITcpConnection connection)
        {
            Ensure.NotNull(connection, "connection");
            _connection = connection;
        }

        public Guid Id
        {
            get { return _connection.ConnectionId; }
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return _connection.RemoteEndPoint; }
        }

        public IPEndPoint LocalEndPoint
        {
            get { return _connection.LocalEndPoint; }
        }

        public void Close()
        {
            _connection.Close(null);
        }

        public override string ToString()
        {
            return string.Format("[Id:{0},RemoteEndPoint:{1},LocalEndPoint:{2}]", Id, RemoteEndPoint, LocalEndPoint);
        }
    }
}
