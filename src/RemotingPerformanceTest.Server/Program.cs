using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using ECommon.Autofac;
using ECommon.Configurations;
using ECommon.Log4Net;
using ECommon.Remoting;
using ECommon.TcpTransport.Utils;
using ECommon.Utilities;

namespace RemotingPerformanceTest.Server
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
            var server = new SocketRemotingServer("Server", new IPEndPoint(SocketUtils.GetLocalIPV4(), 5000));
            server.RegisterRequestHandler(100, new RequestHandler());
            server.Start();
            Console.ReadLine();
        }

        class RequestHandler : IRequestHandler
        {
            int totalHandled;
            Stopwatch watch;
            byte[] response = new byte[100];

            public RemotingResponse HandleRequest(IRequestHandlerContext context, RemotingRequest remotingRequest)
            {
                var local = Interlocked.Increment(ref totalHandled);
                if (local == 1)
                {
                    watch = Stopwatch.StartNew();
                }
                if (local % 10000 == 0)
                {
                    Console.WriteLine("handle request, size:" + remotingRequest.Body.Length + ", count:" + local + ", timeSpent:" + watch.ElapsedMilliseconds + "ms");
                }
                return new RemotingResponse(10, remotingRequest.Sequence, response);
            }
        }

    }
}
