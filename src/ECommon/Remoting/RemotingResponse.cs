namespace ECommon.Remoting
{
    public class RemotingResponse : RemotingMessage
    {
        public short RequestCode { get; private set; }

        public RemotingResponse(short requestCode, short responseCode, short requestType, byte[] responseBody, long requestSequence)
            : base(responseCode, responseBody, requestSequence)
        {
            RequestCode = requestCode;
            Type = requestType;
        }

        public override string ToString()
        {
            return string.Format("[RequestCode:{0}, ResponseCode:{1}, RequestType:{2}, RequestSequence:{3}]", RequestCode, Code, Type, Sequence);
        }
    }
}
