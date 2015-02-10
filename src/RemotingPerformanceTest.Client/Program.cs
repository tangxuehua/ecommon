using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Autofac;
using ECommon.Configurations;
using ECommon.Log4Net;
using ECommon.Remoting;
using ECommon.Utilities;

namespace RemotingPerformanceTest.Client
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

            var messageSize = 100;
            var messageCount = 5000000;
            var message = new byte[messageSize];
            var totalSent = 0;
            var watch = default(Stopwatch);

            var client = new SocketRemotingClient("Client", new IPEndPoint(SocketUtils.GetLocalIPV4(), 5000));
            client.Start();

            for (var i = 0; i < messageCount; i++)
            {
                client.InvokeAsync(new RemotingRequest(100, message), 100000000).ContinueWith(task =>
                {
                    if (task.Exception != null)
                    {
                        Console.WriteLine("sent has exception, errorMsg:{0}", task.Exception.InnerExceptions[0].Message);
                        return;
                    }
                    var local = Interlocked.Increment(ref totalSent);
                    if (local == 1)
                    {
                        watch = Stopwatch.StartNew();
                    }
                    if (local % 10000 == 0)
                    {
                        Console.WriteLine("handle response, size:" + task.Result.Body.Length + ", count:" + local + ", timeSpent:" + watch.ElapsedMilliseconds + "ms");
                    }
                });
            }

            Console.ReadLine();
        }
    }
}
