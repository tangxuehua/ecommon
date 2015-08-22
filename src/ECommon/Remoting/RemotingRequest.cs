using System.Threading;

namespace ECommon.Remoting
{
    public class RemotingRequest : RemotingMessage
    {
        private static long _sequence;

        public bool IsOneway { get; set; }

        public RemotingRequest(short code, byte[] body) : this(code, body, false) { }
        public RemotingRequest(short code, byte[] body, bool isOneway) : this(code, Interlocked.Increment(ref _sequence), body, isOneway) { }
        public RemotingRequest(short code, long sequence, byte[] body, bool isOneway) : base(code, sequence, body)
        {
            IsOneway = isOneway;
        }

        public override string ToString()
        {
            return string.Format("[Code:{0}, Sequence:{1}, IsOneway:{2}]", Code, Sequence, IsOneway);
        }
    }
}
