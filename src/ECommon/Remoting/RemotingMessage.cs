namespace ECommon.Remoting
{
    public class RemotingMessage
    {
        public long Sequence { get; private set; }
        public short Code { get; private set; }
        public byte[] Body { get; private set; }

        public RemotingMessage(short code, long sequence, byte[] body)
        {
            Code = code;
            Sequence = sequence;
            Body = body;
        }
    }
}
