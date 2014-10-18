using System;
using System.Net;

namespace ECommon.Remoting
{
    public interface ISocketChannel
    {
        Guid Id { get; }
        IPEndPoint RemoteEndPoint { get; }
        IPEndPoint LocalEndPoint { get; }
        void Close();
    }
}
