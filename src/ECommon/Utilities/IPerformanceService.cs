using System;

namespace ECommon.Utilities
{
    public interface IPerformanceService
    {
        string Name { get; }
        PerformanceServiceSetting Setting { get; }
        IPerformanceService Initialize(string name, PerformanceServiceSetting setting = null);
        void Start();
        void Stop();
        void IncrementKeyCount(string key, double rtMilliseconds);
        void UpdateKeyCount(string key, long count, double rtMilliseconds);
        PerformanceInfo GetKeyPerformanceInfo(string key);
    }
    public class PerformanceServiceSetting
    {
        public int StatIntervalSeconds { get; set; }
        public bool AutoLogging { get; set; }
        public Func<string> GetLogContextTextFunc { get; set; }
        public Action<PerformanceInfo> PerformanceInfoHandler { get; set; }
    }
    public class PerformanceInfo
    {
        public long TotalCount { get; private set; }
        public long Throughput { get; private set; }
        public long AverageThroughput { get; private set; }
        public double RT { get; private set; }
        public double AverageRT { get; private set; }

        public PerformanceInfo(long totalCount, long throughput, long averageThroughput, double rt, double averageRT)
        {
            TotalCount = totalCount;
            Throughput = throughput;
            AverageThroughput = averageThroughput;
            RT = rt;
            AverageRT = averageRT;
        }
    }
}
