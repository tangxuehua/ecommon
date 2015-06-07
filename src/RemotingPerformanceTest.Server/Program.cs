using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Threading;
using ECommon.Autofac;
using ECommon.Configurations;
using ECommon.Log4Net;
using ECommon.Remoting;
using ECommon.TcpTransport;
using ECommon.Utilities;
using ECommonConfiguration = ECommon.Configurations.Configuration;

namespace RemotingPerformanceTest.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var socketBufferSize = int.Parse(ConfigurationManager.AppSettings["SocketBufferSize"]);
            var setting = new Setting
            {
                TcpConfiguration = new TcpConfiguration
                {
                    SocketBufferSize = socketBufferSize
                }
            };
            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4Net()
                .RegisterUnhandledExceptionHandler();

            var bindingIP = ConfigurationManager.AppSettings["BindingAddress"];
            var serverIP = string.IsNullOrEmpty(bindingIP) ? SocketUtils.GetLocalIPV4() : IPAddress.Parse(bindingIP);
            var server = new SocketRemotingServer("Server", new IPEndPoint(serverIP, 5000));
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
