using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using ECommon.Autofac;
using ECommon.Configurations;
using ECommon.Log4Net;
using ECommon.TcpTransport;
using ECommon.Utilities;

namespace TcpPerformanceTest.Server
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

            var sendReply = true;
            int totalHandled = 0;
            Stopwatch watch = null;
            var serverEndPoint = new IPEndPoint(SocketUtils.GetLocalIPV4(), 5000);
            var serverListener = new TcpServerListener(serverEndPoint, null, (connection, message, sendReplyAction) =>
            {
                var local = Interlocked.Increment(ref totalHandled);
                if (local == 1)
                {
                    watch = Stopwatch.StartNew();
                }
                if (local % 10000 == 0)
                {
                    Console.WriteLine("received message, size:" + message.Length + ", count:" + local + ", timeSpent:" + watch.ElapsedMilliseconds + "ms");
                }
                if (sendReply)
                {
                    sendReplyAction(message);
                }
            });
            serverListener.Start();

            Console.ReadLine();
        }
    }
}
