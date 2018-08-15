using System;
using System.Configuration;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Components;
using ECommon.Configurations;
using ECommon.Logging;
using ECommon.Remoting;
using ECommon.Socketing;
using ECommon.Utilities;
using ECommonConfiguration = ECommon.Configurations.Configuration;

namespace RemotingPerformanceTest.Client
{
    class Program
    {
        static string _performanceKey = "SendMessage";
        static string _mode;
        static int _messageCount;
        static byte[] _message;
        static ILogger _logger;
        static IPerformanceService _performanceService;
        static SocketRemotingClient _client;

        static void Main(string[] args)
        {
            InitializeECommon();
            StartSendMessageTest();
            Console.ReadLine();
        }

        static void InitializeECommon()
        {
            _message = new byte[int.Parse(ConfigurationManager.AppSettings["MessageSize"])];
            _mode = ConfigurationManager.AppSettings["Mode"];
            _messageCount = int.Parse(ConfigurationManager.AppSettings["MessageCount"]);

            var logContextText = "mode: " + _mode;

            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4Net()
                .RegisterUnhandledExceptionHandler()
                .BuildContainer();

            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(typeof(Program).Name);
            _performanceService = ObjectContainer.Resolve<IPerformanceService>();
            var setting = new PerformanceServiceSetting
            {
                AutoLogging = false,
                StatIntervalSeconds = 1,
                PerformanceInfoHandler = x =>
                {
                    _logger.InfoFormat("{0}, {1}, totalCount: {2}, throughput: {3}, averageThrughput: {4}, rt: {5:F3}ms, averageRT: {6:F3}ms", _performanceService.Name, logContextText, x.TotalCount, x.Throughput, x.AverageThroughput, x.RT, x.AverageRT);
                }
            };
            _performanceService.Initialize(_performanceKey, setting);
            _performanceService.Start();
        }
        static void StartSendMessageTest()
        {
            var serverIP = ConfigurationManager.AppSettings["ServerAddress"];
            var serverAddress = string.IsNullOrEmpty(serverIP) ? IPAddress.Loopback : IPAddress.Parse(serverIP);
            var sendAction = default(Action);

            _client = new SocketRemotingClient("Client", new IPEndPoint(serverAddress, 5000)).Start();

            if (_mode == "Oneway")
            {
                sendAction = () =>
                {
                    var request = new RemotingRequest(100, _message);
                    _client.InvokeOneway(request);
                    _performanceService.IncrementKeyCount(_mode, (DateTime.Now - request.CreatedTime).TotalMilliseconds);
                };
            }
            else if (_mode == "Async")
            {
                sendAction = () =>
                {
                    var request = new RemotingRequest(100, _message);
                    _client.InvokeAsync(request, 100000).ContinueWith(t =>
                    {
                        if (t.Exception != null)
                        {
                            _logger.Error(t.Exception);
                            return;
                        }
                        var response = t.Result;
                        if (response.ResponseCode != 10)
                        {
                            _logger.Error(Encoding.UTF8.GetString(response.ResponseBody));
                            return;
                        }
                        _performanceService.IncrementKeyCount(_mode, (DateTime.Now - response.RequestTime).TotalMilliseconds);
                    });
                };
            }
            else if (_mode == "Callback")
            {
                _client.RegisterResponseHandler(100, new ResponseHandler(_performanceService, _mode));
                sendAction = () => _client.InvokeWithCallback(new RemotingRequest(100, _message));
            }

            Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < _messageCount; i++)
                {
                    try
                    {
                        sendAction();
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorFormat("Send remotingRequest failed, errorMsg:{0}", ex.Message);
                        Thread.Sleep(3000);
                    }
                }
            });
        }

        class ResponseHandler : IResponseHandler
        {
            private IPerformanceService _performanceService;
            private string _performanceKey;

            public ResponseHandler(IPerformanceService performanceService, string performanceKey)
            {
                _performanceService = performanceService;
                _performanceKey = performanceKey;
            }

            public void HandleResponse(RemotingResponse remotingResponse)
            {
                if (remotingResponse.ResponseCode != 10)
                {
                    _logger.Error(Encoding.UTF8.GetString(remotingResponse.ResponseBody));
                    return;
                }
                _performanceService.IncrementKeyCount(_performanceKey, (DateTime.Now - remotingResponse.RequestTime).TotalMilliseconds);
            }
        }
    }
}
