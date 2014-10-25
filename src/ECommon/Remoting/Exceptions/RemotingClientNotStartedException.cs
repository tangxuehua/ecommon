using System;

namespace ECommon.Remoting.Exceptions
{
    public class RemotingClientNotStartedException : Exception
    {
        public RemotingClientNotStartedException()
            : base("Remoting client not started, please start the remoting client first.")
        {
        }
    }
}
