using System;
using System.Net;

namespace ECommon.Remoting.Exceptions
{
    public class RemotingServerNotConnectedException : Exception
    {
        public RemotingServerNotConnectedException(IPEndPoint serverEndPoint)
            : base(string.Format("Remoting server is not connected, server address:{0}.", serverEndPoint))
        {
        }
    }
}
