using System;
using System.IO;

namespace ECommon.Remoting
{
    public class RemotingUtil
    {
        public static byte[] BuildRequestMessage(RemotingRequest request)
        {
            var sequenceBytes = BitConverter.GetBytes(request.Sequence);
            var codeBytes = BitConverter.GetBytes(request.Code);
            var isOnewayBytes = BitConverter.GetBytes(request.IsOneway);
            var message = new byte[11 + request.Body.Length];

            sequenceBytes.CopyTo(message, 0);
            codeBytes.CopyTo(message, 8);
            isOnewayBytes.CopyTo(message, 10);
            request.Body.CopyTo(message, 11);

            return message;
        }
        public static RemotingRequest ParseRequest(byte[] messageBuffer)
        {
            var sequenceBytes = new byte[8];
            var codeBytes = new byte[2];
            var isOnewayBytes = new byte[1];
            var data = new byte[messageBuffer.Length - 11];

            Array.Copy(messageBuffer, 0, sequenceBytes, 0, 8);
            Array.Copy(messageBuffer, 8, codeBytes, 0, 2);
            Array.Copy(messageBuffer, 10, isOnewayBytes, 0, 1);
            Array.Copy(messageBuffer, 11, data, 0, data.Length);

            var sequence = BitConverter.ToInt64(sequenceBytes, 0);
            var code = BitConverter.ToInt16(codeBytes, 0);
            var isOneway = BitConverter.ToBoolean(isOnewayBytes, 0);

            return new RemotingRequest(code, sequence, data, isOneway);
        }

        public static byte[] BuildResponseMessage(RemotingResponse response)
        {
            var sequenceBytes = BitConverter.GetBytes(response.Sequence);
            var requestCodeBytes = BitConverter.GetBytes(response.RequestCode);
            var responseCodeBytes = BitConverter.GetBytes(response.Code);
            var message = new byte[12 + response.Body.Length];

            sequenceBytes.CopyTo(message, 0);
            requestCodeBytes.CopyTo(message, 8);
            responseCodeBytes.CopyTo(message, 10);
            response.Body.CopyTo(message, 12);

            return message;
        }
        public static RemotingResponse ParseResponse(byte[] messageBuffer)
        {
            var sequenceBytes = new byte[8];
            var requestCodeBytes = new byte[2];
            var responseCodeBytes = new byte[2];
            var data = new byte[messageBuffer.Length - 12];

            Array.Copy(messageBuffer, 0, sequenceBytes, 0, 8);
            Array.Copy(messageBuffer, 8, requestCodeBytes, 0, 2);
            Array.Copy(messageBuffer, 10, responseCodeBytes, 0, 2);
            Array.Copy(messageBuffer, 12, data, 0, data.Length);

            var sequence = BitConverter.ToInt64(sequenceBytes, 0);
            var requestCode = BitConverter.ToInt16(requestCodeBytes, 0);
            var responseCode = BitConverter.ToInt16(responseCodeBytes, 0);

            return new RemotingResponse(requestCode, responseCode, sequence, data);
        }
    }
}
