namespace ECommon.Remoting
{
    public interface IChannel
    {
        string RemotingAddress { get; }
        void Close();
    }
}
