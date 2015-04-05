using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ECommon.Components;
using ECommon.Configurations;
using ECommon.Logging;
using ECommon.TcpTransport.BufferManagement;
using ECommon.Utilities;

namespace ECommon.TcpTransport
{
    public class TcpConnection : TcpConnectionBase, ITcpConnection
    {
        private static readonly TcpConfiguration TcpConfiguration = Configuration.Instance.Setting.TcpConfiguration;
        private static readonly int MaxSendPacketSize = TcpConfiguration.MaxSendPacketSize;
        private static readonly BufferManager BufferManager = new BufferManager(TcpConfiguration.BufferChunksCount, TcpConfiguration.SocketBufferSize);
        private static readonly ILogger _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(typeof(TcpConnection).FullName);
        private static readonly SocketArgsPool SocketArgsPool = new SocketArgsPool("TcpConnection.SocketArgsPool",
                                                                                   TcpConfiguration.SendReceivePoolSize,
                                                                                   () => new SocketAsyncEventArgs());

        public static ITcpConnection CreateConnectingTcpConnection(Guid connectionId,
                                                                   IPEndPoint localEndPoint,
                                                                   IPEndPoint remoteEndPoint,
                                                                   TcpClientConnector connector,
                                                                   Action<ITcpConnection> onConnectionEstablished,
                                                                   Action<ITcpConnection, SocketError> onConnectionFailed)
        {
            var connection = new TcpConnection(connectionId, remoteEndPoint);
            connector.InitConnect(localEndPoint, remoteEndPoint,
                                  (_, socket) =>
                                  {
                                      connection.InitSocket(socket);
                                      if (onConnectionEstablished != null)
                                          onConnectionEstablished(connection);
                                  },
                                  (_, socketError) =>
                                  {
                                      if (onConnectionFailed != null)
                                          onConnectionFailed(connection, socketError);
                                  }, connection);
            return connection;
        }

        public static ITcpConnection CreateAcceptedTcpConnection(Guid connectionId, IPEndPoint remoteEndPoint, Socket socket)
        {
            var connection = new TcpConnection(connectionId, remoteEndPoint);
            connection.InitSocket(socket);
            return connection;
        }

        public event Action<ITcpConnection, SocketError> ConnectionClosed;
        public Guid ConnectionId { get { return _connectionId; } }

        private readonly Guid _connectionId;

        private Socket _socket;
        private SocketAsyncEventArgs _receiveSocketArgs;
        private SocketAsyncEventArgs _sendSocketArgs;

        private readonly ConcurrentQueue<ArraySegment<byte>> _sendQueue = new ConcurrentQueue<ArraySegment<byte>>();
        private readonly Queue<ReceivedData> _receiveQueue = new Queue<ReceivedData>();
        private readonly MemoryStream _memoryStream = new MemoryStream();

        private readonly object _enqueueLock = new object();
        private readonly object _receivingLock = new object();
        private int _isSending;
        private int _closed;

        private Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> _receiveCallback;

        private TcpConnection(Guid connectionId, IPEndPoint remoteEndPoint) : base(remoteEndPoint)
        {
            Ensure.NotEmptyGuid(connectionId, "connectionId");
            _connectionId = connectionId;
        }

        private void InitSocket(Socket socket)
        {
            InitConnectionBase(socket);

            _socket = socket;
            try
            {
                socket.NoDelay = true;
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            }
            catch (ObjectDisposedException)
            {
                CloseInternal(SocketError.Shutdown, "Socket disposed.");
                return;
            }

            var receiveSocketArgs = SocketArgsPool.Get();
            _receiveSocketArgs = receiveSocketArgs;
            _receiveSocketArgs.AcceptSocket = socket;
            _receiveSocketArgs.Completed += OnReceiveAsyncCompleted;

            var sendSocketArgs = SocketArgsPool.Get();
            _sendSocketArgs = sendSocketArgs;
            _sendSocketArgs.AcceptSocket = socket;
            _sendSocketArgs.Completed += OnSendAsyncCompleted;

            StartReceive();
            TrySend();
        }

        public void SendAsync(IEnumerable<ArraySegment<byte>> data)
        {
            lock (_enqueueLock)
            {
                foreach (var segment in data)
                {
                    _sendQueue.Enqueue(segment);
                }
            }
            TrySend();
        }

        private bool EnterSending()
        {
            return Interlocked.CompareExchange(ref _isSending, 1, 0) == 0;
        }
        private void ExitSending()
        {
            Interlocked.Exchange(ref _isSending, 0);
        }
        private void TrySend()
        {
            if (!EnterSending()) return;

            if (_socket == null)
            {
                ExitSending();
                return;
            }

            _memoryStream.SetLength(0);
            ArraySegment<byte> sendPiece;
            while (_sendQueue.TryDequeue(out sendPiece))
            {
                _memoryStream.Write(sendPiece.Array, sendPiece.Offset, sendPiece.Count);
                if (_memoryStream.Length >= MaxSendPacketSize)
                {
                    break;
                }
            }

            if (_memoryStream.Length == 0)
            {
                ExitSending();
                if (!_sendQueue.IsEmpty)
                {
                    TrySend();
                }
                return;
            }

            _sendSocketArgs.SetBuffer(_memoryStream.GetBuffer(), 0, (int)_memoryStream.Length);

            try
            {
                var firedAsync = _sendSocketArgs.AcceptSocket.SendAsync(_sendSocketArgs);
                if (!firedAsync)
                {
                    ProcessSend(_sendSocketArgs);
                }
            }
            catch (ObjectDisposedException)
            {
                ReturnSendingSocketArgs();
                CloseInternal(SocketError.Shutdown, "Socket disposed.");
            }
        }

        private void OnSendAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessSend(e);
        }

        private void ProcessSend(SocketAsyncEventArgs socketArgs)
        {
            if (socketArgs.SocketError != SocketError.Success)
            {
                ReturnSendingSocketArgs();
                CloseInternal(socketArgs.SocketError, "Socket send error.");
            }
            else
            {
                if (_closed != 0)
                {
                    ReturnSendingSocketArgs();
                }
                else
                {
                    ExitSending();
                    TrySend();
                }
            }
        }

        public void ReceiveAsync(Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback)
        {
            if (callback == null)
                throw new ArgumentNullException("callback");

            lock (_receivingLock)
            {
                if (_receiveCallback != null)
                {
                    _logger.Fatal("ReceiveAsync called again while previous call wasn't fulfilled");
                    throw new InvalidOperationException("ReceiveAsync called again while previous call wasn't fulfilled");
                }
                _receiveCallback = callback;
            }

            TryDequeueReceivedData();
        }

        private void StartReceive()
        {
            var buffer = BufferManager.CheckOut();
            if (buffer.Array == null || buffer.Count == 0 || buffer.Array.Length < buffer.Offset + buffer.Count)
                throw new Exception("Invalid buffer allocated");
            // TODO AN: do we need to lock on _receiveSocketArgs?..
            lock (_receiveSocketArgs)
            {
                _receiveSocketArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
                if (_receiveSocketArgs.Buffer == null) throw new Exception("Buffer was not set");
            }
            try
            {
                bool firedAsync;
                lock (_receiveSocketArgs)
                {
                    if (_receiveSocketArgs.Buffer == null) throw new Exception("Buffer was lost");
                    firedAsync = _receiveSocketArgs.AcceptSocket.ReceiveAsync(_receiveSocketArgs);
                }
                if (!firedAsync)
                    ProcessReceive(_receiveSocketArgs);
            }
            catch (ObjectDisposedException)
            {
                ReturnReceivingSocketArgs();
            }
        }

        private void OnReceiveAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            // No other code should go here.  All handling is the same on async and sync completion.
            ProcessReceive(e);
        }

        private void ProcessReceive(SocketAsyncEventArgs socketArgs)
        {
            // socket closed normally or some error occurred
            if (socketArgs.BytesTransferred == 0 || socketArgs.SocketError != SocketError.Success)
            {
                ReturnReceivingSocketArgs();
                CloseInternal(socketArgs.SocketError, socketArgs.SocketError != SocketError.Success ? "Socket receive error" : "Socket closed");
                return;
            }

            lock (_receivingLock)
            {
                var buf = new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.Count);
                _receiveQueue.Enqueue(new ReceivedData(buf, socketArgs.BytesTransferred));
            }

            lock (_receiveSocketArgs)
            {
                if (socketArgs.Buffer == null)
                    throw new Exception("Cleaning already null buffer");
                socketArgs.SetBuffer(null, 0, 0);
            }

            StartReceive();
            TryDequeueReceivedData();
        }

        private void TryDequeueReceivedData()
        {
            Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback;
            List<ReceivedData> res;
            lock (_receivingLock)
            {
                // no awaiting callback or no data to dequeue
                if (_receiveCallback == null || _receiveQueue.Count == 0)
                    return;

                res = new List<ReceivedData>(_receiveQueue.Count);
                while (_receiveQueue.Count > 0)
                {
                    res.Add(_receiveQueue.Dequeue());
                }

                callback = _receiveCallback;
                _receiveCallback = null;
            }

            var data = new ArraySegment<byte>[res.Count];
            int bytes = 0;
            for (int i = 0; i < data.Length; ++i)
            {
                var d = res[i];
                bytes += d.DataLen;
                data[i] = new ArraySegment<byte>(d.Buf.Array, d.Buf.Offset, d.DataLen);
            }
            callback(this, data);

            for (int i = 0, n = res.Count; i < n; ++i)
            {
                BufferManager.CheckIn(res[i].Buf); // dispose buffers
            }
        }

        public void Close()
        {
            Close(SocketError.Success, null);
        }
        public void Close(SocketError socketError, string reason)
        {
            CloseInternal(socketError, reason ?? "Normal socket close.");
        }

        private void CloseInternal(SocketError socketError, string reason)
        {
            if (Interlocked.CompareExchange(ref _closed, 1, 0) != 0)
            {
                return;
            }

            NotifyClosed();

            if (_socket != null)
            {
                Helper.EatException(() => _socket.Shutdown(SocketShutdown.Both));
                Helper.EatException(() => _socket.Close(TcpConfiguration.SocketCloseTimeoutMs));
                _socket = null;
            }

            ReturnSendingSocketArgs();

            var handler = ConnectionClosed;
            if (handler != null)
                handler(this, socketError);
        }

        private void ReturnSendingSocketArgs()
        {
            var socketArgs = Interlocked.Exchange(ref _sendSocketArgs, null);
            if (socketArgs != null)
            {
                socketArgs.Completed -= OnSendAsyncCompleted;
                socketArgs.AcceptSocket = null;
                if (socketArgs.Buffer != null)
                {
                    socketArgs.SetBuffer(null, 0, 0);
                }
                SocketArgsPool.Return(socketArgs);
            }
        }

        private void ReturnReceivingSocketArgs()
        {
            var socketArgs = Interlocked.Exchange(ref _receiveSocketArgs, null);
            if (socketArgs != null)
            {
                socketArgs.Completed -= OnReceiveAsyncCompleted;
                socketArgs.AcceptSocket = null;
                if (socketArgs.Buffer != null)
                {
                    BufferManager.CheckIn(new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.Count));
                    socketArgs.SetBuffer(null, 0, 0);
                }
                SocketArgsPool.Return(socketArgs);
            }
        }

        public override string ToString()
        {
            return RemoteEndPoint.ToString();
        }

        private struct ReceivedData
        {
            public readonly ArraySegment<byte> Buf;
            public readonly int DataLen;

            public ReceivedData(ArraySegment<byte> buf, int dataLen)
            {
                Buf = buf;
                DataLen = dataLen;
            }
        }
    }
}
