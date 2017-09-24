using System;
using System.Collections.Generic;
using ECommon.Components;
using ECommon.Logging;

namespace ECommon.Socketing.Framing
{
    public class LengthPrefixMessageFramer : IMessageFramer
    {
        private static readonly ILogger _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(typeof(LengthPrefixMessageFramer).FullName);

        public const int HeaderLength = sizeof(Int32);
        private Action<ArraySegment<byte>> _receivedHandler;

        private byte[] _messageBuffer;
        private int _bufferIndex = 0;
        private int _headerBytes = 0;
        private int _packageLength = 0;

        public IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data)
        {
            var length = data.Count;
            yield return new ArraySegment<byte>(new[] { (byte)length, (byte)(length >> 8), (byte)(length >> 16), (byte)(length >> 24) });
            yield return data;
        }
        public void UnFrameData(IEnumerable<ArraySegment<byte>> data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            foreach (ArraySegment<byte> buffer in data)
            {
                Parse(buffer);
            }
        }
        public void UnFrameData(ArraySegment<byte> data)
        {
            Parse(data);
        }
        public void RegisterMessageArrivedCallback(Action<ArraySegment<byte>> handler)
        {
            _receivedHandler = handler ?? throw new ArgumentNullException("handler");
        }

        /// <summary>
        /// Parses a stream chunking based on length-prefixed framing. 
        /// Calls are re-entrant and hold state internally. Once full message arrives,
        /// callback is raised (it is registered via <see cref="RegisterMessageArrivedCallback"/>
        /// </summary>
        /// <param name="bytes">A byte array of data to append</param>
        private void Parse(ArraySegment<byte> bytes)
        {
            byte[] data = bytes.Array;
            for (int i = bytes.Offset, n = bytes.Offset + bytes.Count; i < n; i++)
            {
                if (_headerBytes < HeaderLength)
                {
                    _packageLength |= (data[i] << (_headerBytes * 8)); // little-endian order
                    ++_headerBytes;
                    if (_headerBytes == HeaderLength)
                    {
                        if (_packageLength <= 0)
                        {
                            throw new Exception(string.Format("Package length ({0}) is out of bounds.", _packageLength));
                        }
                        _messageBuffer = new byte[_packageLength];
                    }
                }
                else
                {
                    int copyCnt = Math.Min(bytes.Count + bytes.Offset - i, _packageLength - _bufferIndex);
                    try
                    {
                        Buffer.BlockCopy(bytes.Array, i, _messageBuffer, _bufferIndex, copyCnt);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(string.Format("Parse message buffer failed, _headerLength: {0}, _packageLength: {1}, _bufferIndex: {2}, copyCnt: {3}, _messageBuffer is null: {4}",
                            _headerBytes,
                            _packageLength,
                            _bufferIndex,
                            copyCnt,
                            _messageBuffer == null), ex);
                        throw;
                    }
                    _bufferIndex += copyCnt;
                    i += copyCnt - 1;

                    if (_bufferIndex == _packageLength)
                    {
                        if (_receivedHandler != null)
                        {
                            try
                            {
                                _receivedHandler(new ArraySegment<byte>(_messageBuffer, 0, _bufferIndex));
                            }
                            catch (Exception ex)
                            {
                                _logger.Error("Handle received message fail.", ex);
                            }
                        }
                        _messageBuffer = null;
                        _headerBytes = 0;
                        _packageLength = 0;
                        _bufferIndex = 0;
                    }
                }
            }
        }
    }
}