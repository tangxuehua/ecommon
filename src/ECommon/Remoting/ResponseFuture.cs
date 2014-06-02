using System;
using System.Threading.Tasks;

namespace ECommon.Remoting
{
    public class ResponseFuture
    {
        private DateTime _beginTime;
        private TaskCompletionSource<RemotingResponse> _taskSource;

        public long TimeoutMillis { get; private set; }
        public RemotingRequest Request { get; private set; }

        public ResponseFuture(RemotingRequest request, long timeoutMillis, TaskCompletionSource<RemotingResponse> taskSource)
        {
            Request = request;
            TimeoutMillis = timeoutMillis;
            _taskSource = taskSource;
            _beginTime = DateTime.Now;
        }

        public bool IsTimeout()
        {
            return (DateTime.Now - _beginTime).TotalMilliseconds > TimeoutMillis;
        }
        public void SetResponse(RemotingResponse response)
        {
            _taskSource.TrySetResult(response);
        }
        public void SetException(Exception exception)
        {
            _taskSource.TrySetException(exception);
        }
    }
}
