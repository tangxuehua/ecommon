using System;
using System.Collections.Concurrent;
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
        static byte[] _message;
        static int _parallelThreadCount;
        static ILogger _logger;
        static IScheduleService _scheduleService;
        static TaskFactory _sendTaskFactory;
        static SocketRemotingClient _client;
        static ConcurrentDictionary<int, ThreadSendMessageStatisticData> _threadSendMessageStatisticDataDict;

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
            var sendMessageFlowControlCount = int.Parse(ConfigurationManager.AppSettings["SendMessageFlowControlCount"]);
            var socketSetting = new SocketSetting
            {
                SendMessageFlowControlCount = sendMessageFlowControlCount,
                SendMessageFlowControlWaitMilliseconds = sendMessageFlowControlWaitMilliseconds
            };

            _message = new byte[messageSize];
            _threadSendMessageStatisticDataDict = new ConcurrentDictionary<int, ThreadSendMessageStatisticData>();

            _client = new SocketRemotingClient(new IPEndPoint(serverAddress, 5000), socketSetting);
            _client.Start();
        }
        static void SendMessages()
        {
            if (_mode == "Oneway")
            {
                ParallelSendMessages((messageCount, statisticData) =>
                {
                    for (var i = 0; i < messageCount; i++)
                    {
                        _client.InvokeOneway(new RemotingRequest(100, _message));
                        statisticData.SentCount++;
                    }
                });
            }
            else if (_mode == "Sync")
            {
                ParallelSendMessages((messageCount, statisticData) =>
                {
                    for (var i = 0; i < messageCount; i++)
                    {
                        _client.InvokeSync(new RemotingRequest(100, _message), 5000);
                        statisticData.SentCount++;
                    }
                });
            }
            else if (_mode == "Async")
            {
                ParallelSendMessages((messageCount, statisticData) =>
                {
                    for (var i = 0; i < messageCount; i++)
                    {
                        _client.InvokeAsync(new RemotingRequest(100, _message), 100000).ContinueWith(SendCallback);
                        statisticData.SentCount++;
                    }
                });
            }
            else if (_mode == "Callback")
            {
                _client.RegisterResponseHandler(100, new ResponseHandler());
                ParallelSendMessages((messageCount, statisticData) =>
                {
                    for (var i = 0; i < messageCount; i++)
                    {
                        _client.InvokeWithCallback(new RemotingRequest(100, _message));
                        statisticData.SentCount++;
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
        }
        static void ParallelSendMessages(Action<int, ThreadSendMessageStatisticData> sendRequestAction)
        {
            var messageCountPerThread = _messageCount / _parallelThreadCount;

            for (var i = 0; i < _parallelThreadCount; i++)
            {
                _sendTaskFactory.StartNew(() =>
                {
                    var currentThreadId = Thread.CurrentThread.ManagedThreadId;
                    var statisticData = new ThreadSendMessageStatisticData
                    {
                        ThreadId = currentThreadId
                    };
                    if (_threadSendMessageStatisticDataDict.TryAdd(statisticData.ThreadId, statisticData))
                    {
                        try
                        {
                            sendRequestAction(messageCountPerThread, statisticData);
                        }
                        catch (Exception ex)
                        {
                            _logger.ErrorFormat("Send remoting request failed, errorMsg:{0}", ex.Message);
                            Thread.Sleep(5000);
                        }
                    }
                    else
                    {
                        _logger.ErrorFormat("Duplicated threadId {0}", currentThreadId);
                    }
                });
            }
        }
        static void StartPrintThroughputTask()
        {
            _scheduleService.StartTask("Program.PrintThroughput", PrintThroughput, 0, 1000);
        }
        static void PrintThroughput()
        {
            foreach (var statisticData in _threadSendMessageStatisticDataDict.Values)
            {
                var totalSentCount = statisticData.SentCount;
                var totalCountOfCurrentPeriod = totalSentCount - statisticData.PreviousSentCount;
                statisticData.PreviousSentCount = totalSentCount;
                _logger.InfoFormat("Send message mode: {0}, threadId: {1}, currentTime: {2}, totalSent: {3}, throughput: {4}/s", _mode, statisticData.ThreadId, DateTime.Now.ToLongTimeString(), totalSentCount, totalCountOfCurrentPeriod);
            }
        }

        class ThreadSendMessageStatisticData
        {
            public int ThreadId;
            public int SentCount;
            public int PreviousSentCount;
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
            }
        }
    }
}
