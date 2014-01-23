using System;

namespace ECommon.Socketing
{
    public class SendResult
    {
        public bool Success { get; private set; }
        public Exception Exception { get; private set; }

        public SendResult(bool success, Exception exception)
        {
            Success = success;
            Exception = exception;
        }
    }
}
