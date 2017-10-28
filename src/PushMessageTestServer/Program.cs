using System;
using System.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Components;
using ECommon.Configurations;
using ECommon.Logging;
using ECommon.Remoting;
using ECommonConfiguration = ECommon.Configurations.Configuration;

namespace PushMessageTestServer
{
    class Program
    {
        static ILogger _logger;
        static SocketRemotingServer _remotingServer;

        static void Main(string[] args)
        {
            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4Net()
                .RegisterUnhandledExceptionHandler()
                .BuildContainer();

            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(typeof(Program).Name);
            _remotingServer = new SocketRemotingServer().Start();
            PushTestMessageToAllClients();
            Console.ReadLine();
        }

        static void PushTestMessageToAllClients()
        {
            var messageCount = int.Parse(ConfigurationManager.AppSettings["MessageCount"]);

            Task.Factory.StartNew(() =>
            {
                for (var i = 1; i <= messageCount; i++)
                {
                    try
                    {
                        var remotingServerMessage = new RemotingServerMessage(RemotingServerMessageType.ServerMessage, 100, Encoding.UTF8.GetBytes("message:" + i));
                        _remotingServer.PushMessageToAllConnections(remotingServerMessage);
                        _logger.InfoFormat("Pushed server message: {0}", "message:" + i);
                        Thread.Sleep(1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorFormat("PushMessageToAllConnections failed, errorMsg: {0}", ex.Message);
                        Thread.Sleep(1000);
                    }
                }
            });
        }
    }
}
