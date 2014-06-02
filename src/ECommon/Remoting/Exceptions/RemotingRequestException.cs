using System;

namespace ECommon.Remoting.Exceptions
{
    public class RemotingRequestException : Exception
    {
        public RemotingRequestException(string address, RemotingRequest request, string errorMessage)
            : base(string.Format("Send request {0} to <{1}> failed, errorMessage:{2}", request, address, errorMessage))
        {
        }
        public RemotingRequestException(string address, RemotingRequest request, Exception exception)
            : base(string.Format("Send request {0} to <{1}> failed.", request, address), exception)
        {
        }
    }
}
