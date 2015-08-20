using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using ECommon.Components;
using ECommon.Logging;
using ECommon.Utilities;

namespace ECommon.Socketing
{
    public class ServerSocket
    {
        #region Private Variables

        private readonly Socket _socket;
        private readonly IPEndPoint _listeningEndPoint;
        private readonly SocketAsyncEventArgs _acceptSocketArgs;
        private readonly IList<IConnectionEventListener> _connectionEventListeners;
        private readonly Action<ITcpConnection, byte[], Action<byte[]>> _messageArrivedHandler;
        private readonly ILogger _logger;

        #endregion

        public ServerSocket(IPEndPoint listeningEndPoint, Action<ITcpConnection, byte[], Action<byte[]>> messageArrivedHandler)
        {
            Ensure.NotNull(listeningEndPoint, "listeningEndPoint");
            Ensure.NotNull(messageArrivedHandler, "messageArrivedHandler");

            _listeningEndPoint = listeningEndPoint;
            _connectionEventListeners = new List<IConnectionEventListener>();
            _messageArrivedHandler = messageArrivedHandler;
            _socket = SocketUtils.CreateSocket();
            _acceptSocketArgs = new SocketAsyncEventArgs();
            _acceptSocketArgs.Completed += AcceptCompleted;
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        public void RegisterConnectionEventListener(IConnectionEventListener listener)
        {
            _connectionEventListeners.Add(listener);
        }
        public void Start()
        {
            _logger.InfoFormat("Starting listening on TCP endpoint: {0}.", _listeningEndPoint);

            try
            {
                _socket.Bind(_listeningEndPoint);
                _socket.Listen(5000);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Failed to listen on TCP endpoint: {0}.", _listeningEndPoint), ex);
                SocketUtils.ShutdownSocket(_socket);
                throw;
            }

            StartAccepting();
        }
        public void Shutdown()
        {
            SocketUtils.ShutdownSocket(_socket);
        }

        private void StartAccepting()
        {
            try
            {
                var firedAsync = _socket.AcceptAsync(_acceptSocketArgs);
                if (!firedAsync)
                {
                    ProcessAccept(_acceptSocketArgs);
                }
            }
            catch (Exception ex)
            {
                _logger.Info("Socket accept error", ex);
            }
        }
        private void AcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                SocketUtils.ShutdownSocket(e.AcceptSocket);
                e.AcceptSocket = null;
                return;
            }

            var acceptSocket = e.AcceptSocket;
            e.AcceptSocket = null;
            OnSocketAccepted(acceptSocket);
            StartAccepting();
        }

        private void OnSocketAccepted(Socket socket)
        {
            var connection = new TcpConnection(socket, OnMessageArrived, OnConnectionClosed);

            _logger.InfoFormat("Socket accepted, remote endpoint:{0}", socket.RemoteEndPoint);

            foreach (var listener in _connectionEventListeners)
            {
                try
                {
                    listener.OnConnectionAccepted(connection);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Notify connection accepted failed, listener type:{0}", listener.GetType().Name), ex);
                }
            }
        }
        private void OnMessageArrived(ITcpConnection connection, byte[] message)
        {
            try
            {
                _messageArrivedHandler(connection, message, reply => connection.Send(reply));
            }
            catch (Exception ex)
            {
                _logger.Error("Handle message error.", ex);
            }
        }
        private void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
        {
            foreach (var listener in _connectionEventListeners)
            {
                try
                {
                    listener.OnConnectionClosed(connection, socketError);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Notify connection closed failed, listener type:{0}", listener.GetType().Name), ex);
                }
            }
        }
    }
}