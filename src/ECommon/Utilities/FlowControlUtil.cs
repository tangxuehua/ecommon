namespace ECommon.Utilities
{
    public static class FlowControlUtil
    {
        public static int CalculateFlowControlTimeMilliseconds(int pendingCount, int thresholdCount, int stepPercent, int baseWaitMilliseconds, int maxWaitMilliseconds = 10000)
        {
            var exceedCount = pendingCount - thresholdCount;
            exceedCount = exceedCount <= 0 ? 1 : exceedCount;

            var stepCount = stepPercent * thresholdCount / 100;
            stepCount = stepCount <= 0 ? 1 : stepCount;

            var times = exceedCount / stepCount;
            times = times <= 0 ? 1 : times;

            var waitMilliseconds = times * baseWaitMilliseconds;

            if (waitMilliseconds > maxWaitMilliseconds)
            {
                return maxWaitMilliseconds;
            }
            return waitMilliseconds;
        }
    }
}
