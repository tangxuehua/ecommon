using System;
using System.Collections.Generic;
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
        static long _sendingCount = 0;
        static long _previousSentCount = 0;
        static long _sentCount = 0;
        static string _mode;
        static ILogger _logger;
        static IScheduleService _scheduleService;

        static void Main(string[] args)
        {
            InitializeECommon();
            StartPrintThroughputTask();
            SendMessageTest();
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
        static void SendMessageTest()
        {
            _mode = ConfigurationManager.AppSettings["Mode"];

            var serverIP = ConfigurationManager.AppSettings["ServerAddress"];
            var serverAddress = string.IsNullOrEmpty(serverIP) ? SocketUtils.GetLocalIPV4() : IPAddress.Parse(serverIP);
            var clientCount = int.Parse(ConfigurationManager.AppSettings["ClientCount"]);
            var messageSize = int.Parse(ConfigurationManager.AppSettings["MessageSize"]);
            var messageCount = int.Parse(ConfigurationManager.AppSettings["MessageCount"]);
            var sleepMilliseconds = int.Parse(ConfigurationManager.AppSettings["SleepMilliseconds"]);
            var batchSize = int.Parse(ConfigurationManager.AppSettings["BatchSize"]);
            var message = new byte[messageSize];
            var actions = new List<Action>();

            for (var i = 1; i <= clientCount; i++)
            {
                var client = new SocketRemotingClient("Client" + i.ToString(), new IPEndPoint(serverAddress, 5000));
                client.Start();
                actions.Add(() => SendMessages(client, _mode, messageCount, sleepMilliseconds, batchSize, message));
            }

            Parallel.Invoke(actions.ToArray());
        }
        static void SendMessages(SocketRemotingClient client, string mode, int count, int sleepMilliseconds, int batchSize, byte[] message)
        {
            _logger.Info("----Send message test----");

            if (mode == "Oneway")
            {
                for (var i = 1; i <= count; i++)
                {
                    TryAction(() => client.InvokeOneway(new RemotingRequest(100, message)));
                    Interlocked.Increment(ref _sentCount);
                    WaitIfNecessory(batchSize, sleepMilliseconds);
                }
            }
            else if (mode == "Async")
            {
                for (var i = 1; i <= count; i++)
                {
                    TryAction(() => client.InvokeAsync(new RemotingRequest(100, message), 100000).ContinueWith(SendCallback));
                    WaitIfNecessory(batchSize, sleepMilliseconds);
                }
            }
            else if (mode == "Callback")
            {
                client.RegisterResponseHandler(100, new ResponseHandler());
                for (var i = 1; i <= count; i++)
                {
                    TryAction(() => client.InvokeWithCallback(new RemotingRequest(100, message)));
                    WaitIfNecessory(batchSize, sleepMilliseconds);
                }
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
        static void TryAction(Action sendRequestAction)
        {
            try
            {
                sendRequestAction();
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Send remoting request failed, errorMsg:{0}", ex.Message);
                Thread.Sleep(5000);
            }
        }
        static void WaitIfNecessory(int batchSize, int sleepMilliseconds)
        {
            var current = Interlocked.Increment(ref _sendingCount);
            if (current % batchSize == 0)
            {
                Thread.Sleep(sleepMilliseconds);
            }
        }
        static void StartPrintThroughputTask()
        {
            _scheduleService.StartTask("Program.PrintThroughput", PrintThroughput, 0, 1000);
        }
        static void PrintThroughput()
        {
            var totalSentCount = _sentCount;
            var totalCountOfCurrentPeriod = totalSentCount - _previousSentCount;
            _previousSentCount = totalSentCount;

            _logger.InfoFormat("Send message mode: {0}, currentTime: {1}, totalSent: {2}, throughput: {3}/s", _mode, DateTime.Now.ToLongTimeString(), totalSentCount, totalCountOfCurrentPeriod);
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
