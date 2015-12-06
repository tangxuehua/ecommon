using System;
using System.Threading;
using ECommon.Components;
using ECommon.Configurations;
using ECommon.Logging;
using ECommon.Remoting;
using ECommon.Scheduling;
using ECommonConfiguration = ECommon.Configurations.Configuration;

namespace RemotingPerformanceTest.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4Net()
                .RegisterUnhandledExceptionHandler();

            new SocketRemotingServer().RegisterRequestHandler(100, new RequestHandler()).Start();
            Console.ReadLine();
        }

        class RequestHandler : IRequestHandler
        {
            private readonly IScheduleService _scheduleService;
            private readonly ILogger _logger;
            private readonly byte[] response = new byte[0];
            private long _previusHandledCount;
            private long _handledCount;
            private long _calculateCount = 0;

            public RequestHandler()
            {
                _logger = ObjectContainer.Resolve<ILoggerFactory>().Create("RequestHandler");
                _scheduleService = ObjectContainer.Resolve<IScheduleService>();
                _scheduleService.StartTask("Program.PrintThroughput", PrintThroughput, 1000, 1000);
            }

            public RemotingResponse HandleRequest(IRequestHandlerContext context, RemotingRequest remotingRequest)
            {
                Interlocked.Increment(ref _handledCount);
                return new RemotingResponse(remotingRequest.Code, 10, remotingRequest.Type, response, remotingRequest.Sequence);
            }

            private void PrintThroughput()
            {
                var totalHandledCount = _handledCount;
                var throughput = totalHandledCount - _previusHandledCount;
                _previusHandledCount = totalHandledCount;

                if (throughput > 0)
                {
                    _calculateCount++;
                }

                var average = 0L;
                if (_calculateCount > 0)
                {
                    average = totalHandledCount / _calculateCount;
                }

                _logger.InfoFormat("totalReceived: {0}, throughput: {1}/s, average: {2}", totalHandledCount, throughput, average);
            }
        }
    }
}
