using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Autofac;
using ECommon.Configurations;
using ECommon.Log4Net;
using ECommon.Remoting;
using ECommon.TcpTransport;
using ECommon.Utilities;
using ECommonConfiguration = ECommon.Configurations.Configuration;

namespace RemotingPerformanceTest.Client
{
    class Program
    {
        static SocketRemotingClient _remotingClient;

        static void Main(string[] args)
        {
            var maxSendPacketSize = int.Parse(ConfigurationManager.AppSettings["MaxSendPacketSize"]);
            var socketBufferSize = int.Parse(ConfigurationManager.AppSettings["SocketBufferSize"]);
            var setting = new Setting
            {
                TcpConfiguration = new TcpConfiguration
                {
                    MaxSendPacketSize = maxSendPacketSize,
                    SocketBufferSize = socketBufferSize
                }
            };
            ECommonConfiguration
                .Create(setting)
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4Net()
                .RegisterUnhandledExceptionHandler();

            var serverIP = ConfigurationManager.AppSettings["ServerAddress"];
            var mode = ConfigurationManager.AppSettings["Mode"];
            var ipAddress = string.IsNullOrEmpty(serverIP) ? SocketUtils.GetLocalIPV4() : IPAddress.Parse(serverIP);
            _remotingClient = new SocketRemotingClient(new IPEndPoint(ipAddress, 5000));
            _remotingClient.Start();

            var parallelThreadCount = int.Parse(ConfigurationManager.AppSettings["ParallelThreadCount"]);
            var actions = new List<Action>();
            for (var i = 0; i < parallelThreadCount; i++)
            {
                actions.Add(() => SendMessageAsync(mode));
            }

            _watch.Start();
            Parallel.Invoke(actions.ToArray());
            Console.ReadLine();
        }

        static void SendMessageAsync(string mode)
        {
            Console.WriteLine("----SendMessageAsync Test----");

            var messageSize = int.Parse(ConfigurationManager.AppSettings["MessageSize"]);
            var messageCount = int.Parse(ConfigurationManager.AppSettings["MessageCount"]);
            var message = new byte[messageSize];

            if (mode == "Oneway")
            {
                for (var i = 1; i <= messageCount; i++)
                {
                    _remotingClient.InvokeOneway(new RemotingRequest(100, message));
                }
            }
            else if (mode == "Async")
            {
                for (var i = 1; i <= messageCount; i++)
                {
                    _remotingClient.InvokeAsync(new RemotingRequest(100, message), 200000).ContinueWith(SendCallback);
                }
            }

        }

        static long _sentCount = 0L;
        static Stopwatch _watch = new Stopwatch();
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
            var current = Interlocked.Increment(ref _sentCount);
            if (current % 10000 == 0)
            {
                Console.WriteLine("Sent {0} messages, timeSpent: {1}ms", current, _watch.ElapsedMilliseconds);
            }
        }
    }
}
