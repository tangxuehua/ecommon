using System;
using System.Net;

namespace ECommon.Remoting.Exceptions
{
    public class RemotingServerNotConnectedException : Exception
    {
        public RemotingServerNotConnectedException(IPEndPoint serverEndPoint)
            : base(string.Format("Remoting server is not connected, server address:{0}. Please ensure that you have started the remoting client and connected to the remoting server.", serverEndPoint))
        {
        }
    }
}
