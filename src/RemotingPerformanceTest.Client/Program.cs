using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using ECommon.Autofac;
using ECommon.Configurations;
using ECommon.Log4Net;
using ECommon.Remoting;
using ECommon.Utilities;

namespace RemotingPerformanceTest.Client
{
    class Program
    {
        static SocketRemotingClient _remotingClient;

        static void Main(string[] args)
        {
            Configuration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4Net()
                .RegisterUnhandledExceptionHandler();

            _remotingClient = new SocketRemotingClient(new IPEndPoint(SocketUtils.GetLocalIPV4(), 5000));
            _remotingClient.Start();

            SendMessageSync();
            SendMessageAsync();

            Console.ReadLine();
        }

        static void SendMessageSync()
        {
            Console.WriteLine("----SendMessageSync Test----");

            var messageSize = 300;
            var messageCount = 100000;
            var printSize = 10000;
            var message = new byte[messageSize];
            var watch = Stopwatch.StartNew();

            for (var i = 1; i <= messageCount; i++)
            {
                _remotingClient.InvokeSync(new RemotingRequest(100, message), 10000);
                if (i % printSize == 0)
                {
                    Console.WriteLine("Sent {0} messages, timeSpent: {1}ms", i, watch.ElapsedMilliseconds);
                }
            }
        }
        static void SendMessageAsync()
        {
            Console.WriteLine("----SendMessageAsync Test----");

            var messageSize = 300;
            var messageCount = 1000000;
            var printSize = 10000;
            var message = new byte[messageSize];
            var sentCount = 0L;

            var watch = Stopwatch.StartNew();
            for (var i = 1; i <= messageCount; i++)
            {
                _remotingClient.InvokeAsync(new RemotingRequest(100, message), 100000).ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Console.WriteLine(t);
                        return;
                    }
                    var current = Interlocked.Increment(ref sentCount);
                    if (current % printSize == 0)
                    {
                        Console.WriteLine("Sent {0} messages, timeSpent: {1}ms", current, watch.ElapsedMilliseconds);
                    }
                });
            }
        }
    }
}
