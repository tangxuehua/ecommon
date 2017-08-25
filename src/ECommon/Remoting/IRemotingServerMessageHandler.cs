namespace ECommon.Remoting
{
    public interface IRemotingServerMessageHandler
    {
        void HandleMessage(RemotingServerMessage message);
    }
}
