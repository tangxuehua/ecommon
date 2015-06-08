using System;
using System.IO;

namespace ECommon.Remoting
{
    public class RemotingUtil
    {
        public static byte[] BuildRequestMessage(RemotingRequest request)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(request.Sequence);
                    writer.Write(request.Code);
                    writer.Write(request.IsOneway);
                    writer.Write(request.Body);
                }
                return stream.ToArray();
            }
        }
        public static RemotingRequest ParseRequest(byte[] messageBuffer)
        {
            using (var stream = new MemoryStream(messageBuffer))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var sequence = reader.ReadInt64();
                    var code = reader.ReadInt32();
                    var isOneway = reader.ReadBoolean();
                    var body = reader.ReadBytes(messageBuffer.Length - 13);
                    return new RemotingRequest(code, sequence, body, isOneway);
                }
            }
        }

        public static byte[] BuildResponseMessage(RemotingResponse response)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(response.Sequence);
                    writer.Write(response.Code);
                    writer.Write(response.Body);
                }
                return stream.ToArray();
            }
        }
        public static RemotingResponse ParseResponse(byte[] messageBuffer)
        {
            using (var stream = new MemoryStream(messageBuffer))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var sequence = reader.ReadInt64();
                    var code = reader.ReadInt32();
                    var body = reader.ReadBytes(messageBuffer.Length - 12);
                    return new RemotingResponse(code, sequence, body);
                }
            }
        }
    }
}
