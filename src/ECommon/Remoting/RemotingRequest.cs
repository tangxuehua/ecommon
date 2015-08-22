using System.Threading;

namespace ECommon.Remoting
{
    public class RemotingRequest : RemotingMessage
    {
        private static long _sequence;

        public RemotingRequest(short code, byte[] body)
            : base(code, body, Interlocked.Increment(ref _sequence)) { }
        public RemotingRequest(short code, byte[] body, long sequence)
            : base(code, body, sequence) { }

        public override string ToString()
        {
            return string.Format("[Code:{0}, Type:{1}, Sequence:{2}]", Code, Type, Sequence);
        }
    }
    public class RemotingRequestType
    {
        public const short Async        = 1;
        public const short Oneway       = 2;
        public const short Callback     = 3;
    }
}
