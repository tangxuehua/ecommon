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
using ECommon.Scheduling;
using ECommon.Socketing;
using ECommonConfiguration = ECommon.Configurations.Configuration;

namespace RemotingPerformanceTest.Client
{
    class Program
    {
        static string _mode;
        static int _messageCount;
        static int _sentCount;
        static int _previousSentCount;
        static long _calculateCount = 0;
        static byte[] _message;
        static ILogger _logger;
        static IScheduleService _scheduleService;
        static SocketRemotingClient _client;

        static void Main(string[] args)
        {
            InitializeECommon();
            StartSendMessageTest();
            StartPrintThroughputTask();
            Console.ReadLine();
        }

        static void InitializeECommon()
        {
            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4Net()
                .RegisterUnhandledExceptionHandler();

            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(typeof(Program).Name);
            _scheduleService = ObjectContainer.Resolve<IScheduleService>();
        }
        static void StartSendMessageTest()
        {
            var serverIP = ConfigurationManager.AppSettings["ServerAddress"];
            var serverAddress = string.IsNullOrEmpty(serverIP) ? SocketUtils.GetLocalIPV4() : IPAddress.Parse(serverIP);
            var messageSize = int.Parse(ConfigurationManager.AppSettings["MessageSize"]);

            _message = new byte[messageSize];
            _mode = ConfigurationManager.AppSettings["Mode"];
            _messageCount = int.Parse(ConfigurationManager.AppSettings["MessageCount"]);

            _client = new SocketRemotingClient(new IPEndPoint(serverAddress, 5000)).Start();

            var sendAction = default(Action);

            if (_mode == "Oneway")
            {
                sendAction = () =>
                {
                    _client.InvokeOneway(new RemotingRequest(100, _message));
                    Interlocked.Increment(ref _sentCount);
                };
            }
            else if (_mode == "Sync")
            {
                sendAction = () =>
                {
                    _client.InvokeSync(new RemotingRequest(100, _message), 5000);
                    Interlocked.Increment(ref _sentCount);
                };
            }
            else if (_mode == "Async")
            {
                sendAction = () => _client.InvokeAsync(new RemotingRequest(100, _message), 100000).ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        _logger.Error(t.Exception);
                        return;
                    }
                    if (t.Result.Code <= 0)
                    {
                        _logger.Error(Encoding.UTF8.GetString(t.Result.Body));
                        return;
                    }
                    Interlocked.Increment(ref _sentCount);
                });
            }
            else if (_mode == "Callback")
            {
                _client.RegisterResponseHandler(100, new ResponseHandler());
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
                        _logger.ErrorFormat("Send remoting request failed, errorMsg:{0}", ex.Message);
                        Thread.Sleep(3000);
                    }
                }
            });
        }
        static void StartPrintThroughputTask()
        {
            _scheduleService.StartTask("Program.PrintThroughput", PrintThroughput, 1000, 1000);
        }
        static void PrintThroughput()
        {
            var totalSentCount = _sentCount;
            var throughput = totalSentCount - _previousSentCount;
            _previousSentCount = totalSentCount;
            if (throughput > 0)
            {
                _calculateCount++;
            }

            var average = 0L;
            if (_calculateCount > 0)
            {
                average = totalSentCount / _calculateCount;
            }
            _logger.InfoFormat("Send message mode: {0}, totalSent: {1}, throughput: {2}/s, average: {3}", _mode, totalSentCount, throughput, average);
        }

        class ResponseHandler : IResponseHandler
        {
            public void HandleResponse(RemotingResponse remotingResponse)
            {
                if (remotingResponse.Code <= 0)
                {
                    _logger.Error(Encoding.UTF8.GetString(remotingResponse.Body));
                    return;
                }
                Interlocked.Increment(ref _sentCount);
            }
        }
    }
}
