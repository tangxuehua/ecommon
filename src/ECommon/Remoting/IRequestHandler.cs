namespace ECommon.Remoting
{
    public interface IRequestHandler
    {
        RemotingResponse HandleRequest(IRequestHandlerContext context, RemotingRequest remotingRequest);
    }
}
