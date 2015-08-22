namespace ECommon.Remoting
{
    public class RemotingMessage
    {
        public short Code { get; private set; }
        public short Type { get; internal set; }
        public byte[] Body { get; private set; }
        public long Sequence { get; private set; }

        public RemotingMessage(short code, byte[] body, long sequence)
        {
            Code = code;
            Body = body;
            Sequence = sequence;
        }
    }
}
