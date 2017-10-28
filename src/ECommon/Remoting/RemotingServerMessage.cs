using System;
using System.Collections.Generic;
using System.Linq;
using ECommon.Utilities;

namespace ECommon.Remoting
{
    public class RemotingServerMessage
    {
        public short Type { get; set; }
        public string Id { get; set; }
        public short Code { get; set; }
        public byte[] Body { get; set; }
        public DateTime CreatedTime { get; set; }
        public IDictionary<string, string> Header { get; set; }

        public RemotingServerMessage() { }
        public RemotingServerMessage(short type, short code, byte[] body, IDictionary<string, string> header = null) : this(type, ObjectId.GenerateNewStringId(), code, body, DateTime.Now, header) { }
        public RemotingServerMessage(short type, string id, short code, byte[] body, DateTime createdTime, IDictionary<string, string> header)
        {
            Type = type;
            Id = id;
            Code = code;
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
            return string.Format("[Type:{0}, Id:{1}, Code:{2}, CreatedTime:{3}, BodyLength:{4}, Header: [{5}]]",
                Type, Id, Code, createdTime, bodyLength, header);
        }
    }
}
