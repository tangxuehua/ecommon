using System;
using System.Configuration;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Autofac;
using ECommon.Components;
using ECommon.Log4Net;
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
        static byte[] _message;
        static int _parallelThreadCount;
        static ILogger _logger;
        static IScheduleService _scheduleService;
        static TaskFactory _sendTaskFactory;
        static SocketRemotingClient _client;

        static void Main(string[] args)
        {
            InitializeECommon();
            InitializeTest();
            SendMessages();
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
        static void InitializeTest()
        {
            _mode = ConfigurationManager.AppSettings["Mode"];
            _parallelThreadCount = int.Parse(ConfigurationManager.AppSettings["ParallelThreadCount"]);
            _sendTaskFactory = new TaskFactory(new LimitedConcurrencyLevelTaskScheduler(_parallelThreadCount));
            _messageCount = int.Parse(ConfigurationManager.AppSettings["MessageCount"]);

            var serverIP = ConfigurationManager.AppSettings["ServerAddress"];
            var serverAddress = string.IsNullOrEmpty(serverIP) ? SocketUtils.GetLocalIPV4() : IPAddress.Parse(serverIP);
            var messageSize = int.Parse(ConfigurationManager.AppSettings["MessageSize"]);
            var sendMessageFlowControlWaitMilliseconds = int.Parse(ConfigurationManager.AppSettings["SendMessageFlowControlWaitMilliseconds"]);
            var sendMessageFlowControlThreshold = int.Parse(ConfigurationManager.AppSettings["SendMessageFlowControlThreshold"]);
            var socketSetting = new SocketSetting
            {
                SendMessageFlowControlThreshold = sendMessageFlowControlThreshold,
                SendMessageFlowControlWaitMilliseconds = sendMessageFlowControlWaitMilliseconds
            };

            _message = new byte[messageSize];

            _client = new SocketRemotingClient(new IPEndPoint(serverAddress, 5000), socketSetting);
            _client.Start();
        }
        static void SendMessages()
        {
            if (_mode == "Oneway")
            {
                ParallelSendMessages(messageCount =>
                {
                    for (var i = 0; i < messageCount; i++)
                    {
                        _client.InvokeOneway(new RemotingRequest(100, _message));
                        Interlocked.Increment(ref _sentCount);
                    }
                });
            }
            else if (_mode == "Sync")
            {
                ParallelSendMessages(messageCount =>
                {
                    for (var i = 0; i < messageCount; i++)
                    {
                        _client.InvokeSync(new RemotingRequest(100, _message), 5000);
                        Interlocked.Increment(ref _sentCount);
                    }
                });
            }
            else if (_mode == "Async")
            {
                ParallelSendMessages(messageCount =>
                {
                    for (var i = 0; i < messageCount; i++)
                    {
                        _client.InvokeAsync(new RemotingRequest(100, _message), 100000).ContinueWith(SendCallback);
                    }
                });
            }
            else if (_mode == "Callback")
            {
                _client.RegisterResponseHandler(100, new ResponseHandler());
                ParallelSendMessages(messageCount =>
                {
                    for (var i = 0; i < messageCount; i++)
                    {
                        _client.InvokeWithCallback(new RemotingRequest(100, _message));
                    }
                });
            }
        }
        static void SendCallback(Task<RemotingResponse> task)
        {
            if (task.Exception != null)
            {
                _logger.Error(task.Exception);
                return;
            }
            if (task.Result.Code <= 0)
            {
                _logger.Error(Encoding.UTF8.GetString(task.Result.Body));
                return;
            }
            Interlocked.Increment(ref _sentCount);
        }
        static void ParallelSendMessages(Action<int> sendRequestAction)
        {
            var messageCountPerThread = _messageCount / _parallelThreadCount;

            for (var i = 0; i < _parallelThreadCount; i++)
            {
                _sendTaskFactory.StartNew(() =>
                {
                    try
                    {
                        sendRequestAction(messageCountPerThread);
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorFormat("Send remoting request failed, errorMsg:{0}", ex.Message);
                        Thread.Sleep(5000);
                    }
                });
            }
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
            _logger.InfoFormat("Send message mode: {0}, currentTime: {1}, totalSent: {2}, throughput: {3}/s", _mode, DateTime.Now.ToLongTimeString(), totalSentCount, throughput);
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
