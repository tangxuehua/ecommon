using System;
using ECommon.Components;
using ECommon.Configurations;
using ECommon.Logging;
using ECommon.Remoting;
using ECommon.Utilities;
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
            private readonly ILogger _logger;
            private readonly string _performanceKey = "ReceiveMessage";
            private readonly IPerformanceService _performanceService;
            private readonly byte[] response = new byte[0];

            public RequestHandler()
            {
                _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
                _performanceService = ObjectContainer.Resolve<IPerformanceService>();
                var setting = new PerformanceServiceSetting
                {
                    AutoLogging = false,
                    StatIntervalSeconds = 1,
                    PerformanceInfoHandler = x =>
                    {
                        _logger.InfoFormat("{0}, totalCount: {1}, throughput: {2}, averageThrughput: {3}, rt: {4:F3}ms, averageRT: {5:F3}ms", _performanceService.Name, x.TotalCount, x.Throughput, x.AverageThroughput, x.RT, x.AverageRT);
                    }
                };
                _performanceService.Initialize(_performanceKey, setting);
                _performanceService.Start();
            }

            public RemotingResponse HandleRequest(IRequestHandlerContext context, RemotingRequest remotingRequest)
            {
                var currentTime = DateTime.Now;
                _performanceService.IncrementKeyCount(_performanceKey, (currentTime - remotingRequest.CreatedTime).TotalMilliseconds);
                return new RemotingResponse(
                    remotingRequest.Type,
                    remotingRequest.Code,
                    remotingRequest.Sequence,
                    remotingRequest.CreatedTime,
                    10,
                    response,
                    currentTime,
                    remotingRequest.Header,
                    null);
            }
        }
    }
}
