using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Autofac;
using ECommon.Configurations;
using ECommon.Log4Net;
using ECommon.TcpTransport;
using ECommon.TcpTransport.Framing;
using ECommon.Utilities;
using TcpSocketClient = ECommon.TcpTransport.TcpClient;

namespace TcpPerformanceTest.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            Configuration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4Net()
                .RegisterUnhandledExceptionHandler();

            var totalHandled = 0;
            var watch = default(Stopwatch);
            var serverEndPoint = new IPEndPoint(SocketUtils.GetLocalIPV4(), 5000);
            var messageSize = 100 * 1;
            var messageCount = 1000000;
            var message = new byte[messageSize];

            var client = new TcpSocketClient(serverEndPoint, reply =>
            {
                var local = Interlocked.Increment(ref totalHandled);
                if (local == 1)
                {
                    watch = Stopwatch.StartNew();
                }
                if (local % 10000 == 0)
                {
                    Console.WriteLine("received reply message, size:" + reply.Length + ", count:" + local + ", timeSpent:" + watch.ElapsedMilliseconds + "ms");
                }
            });
            client.Start();

            var action = new Action(() =>
            {
                for (var index = 0; index < messageCount; index++)
                {
                    client.SendMessage(message);
                }
            });
            Parallel.Invoke(action, action, action, action);

            Console.ReadLine();
        }
    }
}
