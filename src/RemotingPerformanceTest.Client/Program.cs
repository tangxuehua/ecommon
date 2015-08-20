using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Autofac;
using ECommon.Components;
using ECommon.Log4Net;
using ECommon.Logging;
using ECommon.Remoting;
using ECommon.Socketing;
using ECommonConfiguration = ECommon.Configurations.Configuration;

namespace RemotingPerformanceTest.Client
{
    class Program
    {
        static long _totalSentCount = 0L;
        static ILogger _logger;
        static Stopwatch _watch = new Stopwatch();

        static void Main(string[] args)
        {
            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4Net()
                .RegisterUnhandledExceptionHandler();

            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(typeof(Program).Name);

            var serverIP = ConfigurationManager.AppSettings["ServerAddress"];
            var mode = ConfigurationManager.AppSettings["Mode"];
            var serverAddress = string.IsNullOrEmpty(serverIP) ? SocketUtils.GetLocalIPV4() : IPAddress.Parse(serverIP);
            var parallelThreadCount = int.Parse(ConfigurationManager.AppSettings["ClientCount"]);
            var messageSize = int.Parse(ConfigurationManager.AppSettings["MessageSize"]);
            var messageCount = int.Parse(ConfigurationManager.AppSettings["MessageCount"]);
            var sleepMilliseconds = int.Parse(ConfigurationManager.AppSettings["SleepMilliseconds"]);
            var batchSize = int.Parse(ConfigurationManager.AppSettings["BatchSize"]);
            var message = new byte[messageSize];
            var actions = new List<Action>();

            for (var i = 0; i < parallelThreadCount; i++)
            {
                var client = new SocketRemotingClient(new IPEndPoint(serverAddress, 5000));
                client.Start();
                actions.Add(() => SendMessages(client, mode, messageCount, sleepMilliseconds, batchSize, message));
            }

            _watch.Start();
            Parallel.Invoke(actions.ToArray());
            Console.ReadLine();
        }

        static void SendMessages(SocketRemotingClient client, string mode, int count, int sleepMilliseconds, int batchSize, byte[] message)
        {
            Console.WriteLine("----Send message test----");

            var sentCount = 0;

            if (mode == "Oneway")
            {
                for (var i = 1; i <= count; i++)
                {
                    TrySendMessage(() => client.InvokeOneway(new RemotingRequest(100, message)));
                    var current = Interlocked.Increment(ref _totalSentCount);
                    if (current % 10000 == 0)
                    {
                        Console.WriteLine("Sent {0} messages, timeSpent: {1}ms", current, _watch.ElapsedMilliseconds);
                    }
                    var currentCount = Interlocked.Increment(ref sentCount);
                    if (currentCount % batchSize == 0)
                    {
                        Thread.Sleep(sleepMilliseconds);
                    }
                }
            }
            else if (mode == "Async")
            {
                for (var i = 1; i <= count; i++)
                {
                    TrySendMessage(() => client.InvokeAsync(new RemotingRequest(100, message), 100000).ContinueWith(SendCallback));
                    var current = Interlocked.Increment(ref sentCount);
                    if (current % batchSize == 0)
                    {
                        Thread.Sleep(sleepMilliseconds);
                    }
                }
            }
        }
        static void SendCallback(Task<RemotingResponse> task)
        {
            if (task.Exception != null)
            {
                Console.WriteLine(task.Exception);
                return;
            }
            if (task.Result.Code == 0)
            {
                Console.WriteLine(Encoding.UTF8.GetString(task.Result.Body));
                return;
            }
            var current = Interlocked.Increment(ref _totalSentCount);
            if (current % 10000 == 0)
            {
                Console.WriteLine("Sent {0} messages, timeSpent: {1}ms", current, _watch.ElapsedMilliseconds);
            }
        }
        static void TrySendMessage(Action sendMessageAction)
        {
            try
            {
                sendMessageAction();
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Send message failed, errorMsg:{0}", ex.Message);
                Thread.Sleep(5000);
            }
        }
    }
}
