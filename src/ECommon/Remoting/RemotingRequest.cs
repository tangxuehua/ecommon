using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ECommon.Remoting
{
    public class RemotingRequest
    {
        private static long _sequence;

        public short Type { get; set; }
        public short Code { get; set; }
        public long Sequence { get; set; }
        public byte[] Body { get; set; }
        public DateTime CreatedTime { get; set; }
        public IDictionary<string, string> Header { get; set; }

        public RemotingRequest() { }
        public RemotingRequest(short code, byte[] body, IDictionary<string, string> header = null) : this(code, Interlocked.Increment(ref _sequence), body, DateTime.Now, header) { }
        public RemotingRequest(short code, long sequence, byte[] body, DateTime createdTime, IDictionary<string, string> header)
        {
            Code = code;
            Sequence = sequence;
            Body = body;
            Header = header;
            CreatedTime = createdTime;
        }

        public override string ToString()
        {
            var createdTime = CreatedTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var bodyLength = 0;
            if (Body != null)
            {
                bodyLength = Body.Length;
            }
            var header = string.Empty;
            if (Header != null && Header.Count > 0)
            {
                header = string.Join(",", Header.Select(x => string.Format("{0}:{1}", x.Key, x.Value)));
            }
            return string.Format("[Type:{0}, Code:{1}, Sequence:{2}, CreatedTime:{3}, BodyLength:{4}, Header: [{5}]]",
                Type, Code, Sequence, createdTime, bodyLength, header);
        }
    }
    public class RemotingRequestType
    {
        public const short Async        = 1;
        public const short Oneway       = 2;
        public const short Callback     = 3;
    }
}
