using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using ECommon.Autofac;
using ECommon.Components;
using ECommon.Log4Net;
using ECommon.Logging;
using ECommon.Remoting;
using ECommonConfiguration = ECommon.Configurations.Configuration;

namespace RemotingPerformanceTest.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            ECommonConfiguration
                .Create()
                .UseAutofac()
                .RegisterCommonComponents()
                .UseLog4Net()
                .RegisterUnhandledExceptionHandler();

            new SocketRemotingServer().RegisterRequestHandler(100, new RequestHandler()).Start();
            Console.ReadLine();
        }

        class RequestHandler : IRequestHandler
        {
            int totalHandled;
            Stopwatch watch;
            byte[] response = new byte[100];
            string storeOption = ConfigurationManager.AppSettings["StoreOption"];
            ConcurrentDictionary<long, object> messageDictionary = new ConcurrentDictionary<long, object>();

            public RemotingResponse HandleRequest(IRequestHandlerContext context, RemotingRequest remotingRequest)
            {
                var current = Interlocked.Increment(ref totalHandled);
                if (current == 1)
                {
                    watch = Stopwatch.StartNew();
                }
                if (storeOption == "UnManagedMemory")
                {
                    SaveMessage(remotingRequest.Body);
                }
                else if (storeOption == "ManagedMemory")
                {
                    messageDictionary[remotingRequest.Sequence] = remotingRequest.Body;
                }
                else if (storeOption == "OnlyMapping")
                {
                    messageDictionary[remotingRequest.Sequence] = remotingRequest.Sequence;
                }
                if (current % 10000 == 0)
                {
                    Console.WriteLine("Handled request, size:{0}, count:{1}, timeSpent: {2}ms", remotingRequest.Body.Length, current, watch.ElapsedMilliseconds);
                }
                return new RemotingResponse(remotingRequest.Code, 10, remotingRequest.Type, response, remotingRequest.Sequence);
            }

            unsafe void SaveMessage(byte[] message)
            {
                // Allocate a block of unmanaged memory and return an IntPtr object.	
                IntPtr memIntPtr = Marshal.AllocHGlobal(message.Length);

                // Get a byte pointer from the IntPtr object. 
                byte* memBytePtr = (byte*)memIntPtr.ToPointer();

                // Create an UnmanagedMemoryStream object using a pointer to unmanaged memory.
                UnmanagedMemoryStream writeStream = new UnmanagedMemoryStream(memBytePtr, message.Length, message.Length, FileAccess.Write);

                // Write the data.
                writeStream.Write(message, 0, message.Length);

                // Close the stream.
                writeStream.Close();
            }
        }
    }
}
