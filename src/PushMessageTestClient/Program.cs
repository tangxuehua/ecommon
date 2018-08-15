using System;
using System.Configuration;
using System.Net;
using System.Text;
using ECommon.Components;
using ECommon.Configurations;
using ECommon.Logging;
using ECommon.Remoting;
using ECommonConfiguration = ECommon.Configurations.Configuration;

namespace PushMessageTestClient
{
    class Program
    {
        static ILogger _logger;
        static SocketRemotingClient _client;

        static void Main(string[] args)
        {
            InitializeECommon();
            Console.ReadLine();
        }

        static void InitializeECommon()
        {
            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4Net()
                .RegisterUnhandledExceptionHandler()
                .BuildContainer();

            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(typeof(Program).Name);
            var serverIP = ConfigurationManager.AppSettings["ServerAddress"];
            var serverAddress = string.IsNullOrEmpty(serverIP) ? IPAddress.Loopback : IPAddress.Parse(serverIP);
            _client = new SocketRemotingClient("Client", new IPEndPoint(serverAddress, 5000)).Start();
            _client.RegisterRemotingServerMessageHandler(100, new RemotingServerMessageHandler());
        }
        
        class RemotingServerMessageHandler : IRemotingServerMessageHandler
        {
            public void HandleMessage(RemotingServerMessage message)
            {
                if (message.Code != 100)
                {
                    _logger.ErrorFormat("Invalid remoting server message: {0}", message);
                    return;
                }
                _logger.InfoFormat("Received server message: {0}", Encoding.UTF8.GetString(message.Body));
            }
        }
    }
}