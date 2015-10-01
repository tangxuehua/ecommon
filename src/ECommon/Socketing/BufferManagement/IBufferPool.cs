namespace ECommon.Socketing.BufferManagement
{
    public interface IBufferPool : IPool<byte[]>
    {
        int BufferSize { get; }
    }
}
