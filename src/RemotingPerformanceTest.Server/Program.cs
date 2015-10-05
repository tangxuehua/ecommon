using System;
using System.Threading;
using ECommon.Autofac;
using ECommon.Components;
using ECommon.Log4Net;
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

                _logger.InfoFormat("currentTime: {0}, totalReceived: {1}, throughput: {2}/s", DateTime.Now.ToLongTimeString(), totalHandledCount, throughput);
            }
        }
    }
}
