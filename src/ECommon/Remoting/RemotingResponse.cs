namespace ECommon.Remoting
{
    public class RemotingResponse : RemotingMessage
    {
        public short RequestCode { get; private set; }

        public RemotingResponse(short requestCode, short responseCode, long sequence, byte[] body) : base(responseCode, sequence, body)
        {
            RequestCode = requestCode;
        }

        public override string ToString()
        {
            return string.Format("[RequestCode:{0}, ResponseCode:{1}, Sequence:{2}]", RequestCode, Code, Sequence);
        }
    }
}
