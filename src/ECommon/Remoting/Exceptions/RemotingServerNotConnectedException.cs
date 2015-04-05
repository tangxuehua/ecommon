using System;
using System.Net;
using ECommon.Extensions;
using ECommon.TcpTransport;

namespace ECommon.Remoting.Exceptions
{
    public class RemotingServerNotConnectedException : Exception
    {
        public RemotingServerNotConnectedException(IPEndPoint serverEndPoint, TcpConnectionStatus status)
            : base(string.Format("Remoting server is not connected, server address: {0}, connectionStatus: {1}. Please ensure that you have started the remoting client and connected to the remoting server.", serverEndPoint, status))
        {
        }
    }
}
