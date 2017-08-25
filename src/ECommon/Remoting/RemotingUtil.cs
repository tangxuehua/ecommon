using System;
using System.Collections.Generic;
using ECommon.Utilities;

namespace ECommon.Remoting
{
    public class RemotingUtil
    {
        public static byte[] BuildRequestMessage(RemotingRequest request)
        {
            byte[] IdBytes;
            byte[] IdLengthBytes;
            ByteUtil.EncodeString(request.Id, out IdLengthBytes, out IdBytes);

            var sequenceBytes = BitConverter.GetBytes(request.Sequence);
            var codeBytes = BitConverter.GetBytes(request.Code);
            var typeBytes = BitConverter.GetBytes(request.Type);
            var createdTimeBytes = ByteUtil.EncodeDateTime(request.CreatedTime);
            var headerBytes = HeaderUtil.EncodeHeader(request.Header);
            var headerLengthBytes = BitConverter.GetBytes(headerBytes.Length);

            return ByteUtil.Combine(
                IdLengthBytes,
                IdBytes,
                sequenceBytes,
                codeBytes,
                typeBytes,
                createdTimeBytes,
                headerLengthBytes,
                headerBytes,
                request.Body);
        }
        public static RemotingRequest ParseRequest(byte[] data)
        {
            var srcOffset = 0;

            var id = ByteUtil.DecodeString(data, srcOffset, out srcOffset);
            var sequence = ByteUtil.DecodeLong(data, srcOffset, out srcOffset);
            var code = ByteUtil.DecodeShort(data, srcOffset, out srcOffset);
            var type = ByteUtil.DecodeShort(data, srcOffset, out srcOffset);
            var createdTime = ByteUtil.DecodeDateTime(data, srcOffset, out srcOffset);
            var headerLength = ByteUtil.DecodeInt(data, srcOffset, out srcOffset);
            var header = HeaderUtil.DecodeHeader(data, srcOffset, out srcOffset);
            var bodyLength = data.Length - srcOffset;
            var body = new byte[bodyLength];

            Buffer.BlockCopy(data, srcOffset, body, 0, bodyLength);

            return new RemotingRequest(id, code, sequence, body, createdTime, header) { Type = type };
        }

        public static byte[] BuildResponseMessage(RemotingResponse response)
        {
            var requestSequenceBytes = BitConverter.GetBytes(response.RequestSequence);
            var requestCodeBytes = BitConverter.GetBytes(response.RequestCode);
            var requestTypeBytes = BitConverter.GetBytes(response.RequestType);
            var requestTimeBytes = ByteUtil.EncodeDateTime(response.RequestTime);
            var requestHeaderBytes = HeaderUtil.EncodeHeader(response.RequestHeader);
            var requestHeaderLengthBytes = BitConverter.GetBytes(requestHeaderBytes.Length);

            var responseCodeBytes = BitConverter.GetBytes(response.ResponseCode);
            var responseTimeBytes = ByteUtil.EncodeDateTime(response.ResponseTime);
            var responseHeaderBytes = HeaderUtil.EncodeHeader(response.ResponseHeader);
            var responseHeaderLengthBytes = BitConverter.GetBytes(requestHeaderBytes.Length);

            return ByteUtil.Combine(
                requestSequenceBytes,
                requestCodeBytes,
                requestTypeBytes,
                requestTimeBytes,
                requestHeaderLengthBytes,
                requestHeaderBytes,
                responseCodeBytes,
                responseTimeBytes,
                responseHeaderLengthBytes,
                responseHeaderBytes,
                response.ResponseBody);
        }
        public static RemotingResponse ParseResponse(byte[] data)
        {
            var srcOffset = 0;

            var requestSequence = ByteUtil.DecodeLong(data, srcOffset, out srcOffset);
            var requestCode = ByteUtil.DecodeShort(data, srcOffset, out srcOffset);
            var requestType = ByteUtil.DecodeShort(data, srcOffset, out srcOffset);
            var requestTime = ByteUtil.DecodeDateTime(data, srcOffset, out srcOffset);
            var requestHeaderLength = ByteUtil.DecodeInt(data, srcOffset, out srcOffset);
            var requestHeader = HeaderUtil.DecodeHeader(data, srcOffset, out srcOffset);
            var responseCode = ByteUtil.DecodeShort(data, srcOffset, out srcOffset);
            var responseTime = ByteUtil.DecodeDateTime(data, srcOffset, out srcOffset);
            var responseHeaderLength = ByteUtil.DecodeInt(data, srcOffset, out srcOffset);
            var responseHeader = HeaderUtil.DecodeHeader(data, srcOffset, out srcOffset);

            var responseBodyLength = data.Length - srcOffset;
            var responseBody = new byte[responseBodyLength];

            Buffer.BlockCopy(data, srcOffset, responseBody, 0, responseBodyLength);

            return new RemotingResponse(
                requestType,
                requestCode,
                requestSequence,
                requestTime,
                responseCode,
                responseBody,
                responseTime,
                requestHeader,
                responseHeader);
        }

        public static byte[] BuildRemotingServerMessage(RemotingServerMessage message)
        {
            byte[] IdBytes;
            byte[] IdLengthBytes;
            ByteUtil.EncodeString(message.Id, out IdLengthBytes, out IdBytes);

            var typeBytes = BitConverter.GetBytes(message.Type);
            var codeBytes = BitConverter.GetBytes(message.Code);
            var createdTimeBytes = ByteUtil.EncodeDateTime(message.CreatedTime);
            var headerBytes = HeaderUtil.EncodeHeader(message.Header);
            var headerLengthBytes = BitConverter.GetBytes(headerBytes.Length);

            return ByteUtil.Combine(
                IdLengthBytes,
                IdBytes,
                typeBytes,
                codeBytes,
                createdTimeBytes,
                headerLengthBytes,
                headerBytes,
                message.Body);
        }
        public static RemotingServerMessage ParseRemotingServerMessage(byte[] data)
        {
            var srcOffset = 0;

            var id = ByteUtil.DecodeString(data, srcOffset, out srcOffset);
            var type = ByteUtil.DecodeShort(data, srcOffset, out srcOffset);
            var code = ByteUtil.DecodeShort(data, srcOffset, out srcOffset);
            var createdTime = ByteUtil.DecodeDateTime(data, srcOffset, out srcOffset);
            var headerLength = ByteUtil.DecodeInt(data, srcOffset, out srcOffset);
            var header = HeaderUtil.DecodeHeader(data, srcOffset, out srcOffset);
            var bodyLength = data.Length - srcOffset;
            var body = new byte[bodyLength];

            Buffer.BlockCopy(data, srcOffset, body, 0, bodyLength);

            return new RemotingServerMessage(type, id, code, body, createdTime, header);
        }
    }
    public class HeaderUtil
    {
        public static readonly byte[] ZeroLengthBytes = BitConverter.GetBytes(0);
        public static readonly byte[] EmptyBytes = new byte[0];

        public static byte[] EncodeHeader(IDictionary<string, string> header)
        {
            var headerKeyCount = header != null ? header.Count : 0;
            var headerKeyCountBytes = BitConverter.GetBytes(headerKeyCount);
            var bytesList = new List<byte[]>();

            bytesList.Add(headerKeyCountBytes);

            if (headerKeyCount > 0)
            {
                foreach (var entry in header)
                {
                    byte[] keyBytes;
                    byte[] keyLengthBytes;
                    byte[] valueBytes;
                    byte[] valueLengthBytes;

                    ByteUtil.EncodeString(entry.Key, out keyLengthBytes, out keyBytes);
                    ByteUtil.EncodeString(entry.Value, out valueLengthBytes, out valueBytes);

                    bytesList.Add(keyLengthBytes);
                    bytesList.Add(keyBytes);
                    bytesList.Add(valueLengthBytes);
                    bytesList.Add(valueBytes);
                }
            }   

            return ByteUtil.Combine(bytesList.ToArray());
        }
        public static IDictionary<string, string> DecodeHeader(byte[] data, int startOffset, out int nextStartOffset)
        {
            var dict = new Dictionary<string, string>();
            var srcOffset = startOffset;
            var headerKeyCount = ByteUtil.DecodeInt(data, srcOffset, out srcOffset);
            for (var i = 0; i < headerKeyCount; i++)
            {
                var key = ByteUtil.DecodeString(data, srcOffset, out srcOffset);
                var value = ByteUtil.DecodeString(data, srcOffset, out srcOffset);
                dict.Add(key, value);
            }
            nextStartOffset = srcOffset;
            return dict;
        }
    }
}
