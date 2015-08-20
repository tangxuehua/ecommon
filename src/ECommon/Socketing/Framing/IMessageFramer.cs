using System;
using System.Collections.Generic;

namespace ECommon.Socketing.Framing
{
    public interface IMessageFramer
    {
        void UnFrameData(IEnumerable<ArraySegment<byte>> data);
        void UnFrameData(ArraySegment<byte> data);
        IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data);
        void RegisterMessageArrivedCallback(Action<ArraySegment<byte>> handler);
    }
}