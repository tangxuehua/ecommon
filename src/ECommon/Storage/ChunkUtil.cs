using Microsoft.VisualBasic.Devices;

namespace ECommon.Storage
{
    public class ChunkUtil
    {
        public static bool IsMemoryEnoughToCacheChunk(ulong chunkSize, uint maxUseMemoryPercent)
        {
            var computerInfo = new ComputerInfo();
            var maxAllowUseMemory = computerInfo.TotalPhysicalMemory * maxUseMemoryPercent / 100;
            var currentUsedMemory = computerInfo.TotalPhysicalMemory - computerInfo.AvailablePhysicalMemory;

            return currentUsedMemory + chunkSize <= maxAllowUseMemory;
        }
        /// <summary>获取当前使用的物理内存百分比
        /// </summary>
        /// <returns></returns>
        public static ulong GetUsedMemoryPercent()
        {
            var computerInfo = new ComputerInfo();
            var usedPhysicalMemory = computerInfo.TotalPhysicalMemory - computerInfo.AvailablePhysicalMemory;
            var usedMemoryPercent = usedPhysicalMemory * 100 / computerInfo.TotalPhysicalMemory;
            return usedMemoryPercent;
        }
    }
}
