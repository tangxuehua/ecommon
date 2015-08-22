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
            var typeBytes = BitConverter.GetBytes(request.Type);
            var message = new byte[12 + request.Body.Length];

            sequenceBytes.CopyTo(message, 0);
            codeBytes.CopyTo(message, 8);
            typeBytes.CopyTo(message, 10);
            request.Body.CopyTo(message, 12);

            return message;
        }
        public static RemotingRequest ParseRequest(byte[] messageBuffer)
        {
            var sequenceBytes = new byte[8];
            var codeBytes = new byte[2];
            var typeBytes = new byte[2];
            var body = new byte[messageBuffer.Length - 12];

            Array.Copy(messageBuffer, 0, sequenceBytes, 0, 8);
            Array.Copy(messageBuffer, 8, codeBytes, 0, 2);
            Array.Copy(messageBuffer, 10, typeBytes, 0, 2);
            Array.Copy(messageBuffer, 12, body, 0, body.Length);

            var sequence = BitConverter.ToInt64(sequenceBytes, 0);
            var code = BitConverter.ToInt16(codeBytes, 0);
            var type = BitConverter.ToInt16(typeBytes, 0);

            return new RemotingRequest(code, body, sequence) { Type = type };
        }

        public static byte[] BuildResponseMessage(RemotingResponse response)
        {
            var sequenceBytes = BitConverter.GetBytes(response.Sequence);
            var requestCodeBytes = BitConverter.GetBytes(response.RequestCode);
            var responseCodeBytes = BitConverter.GetBytes(response.Code);
            var requestTypeBytes = BitConverter.GetBytes(response.Type);
            var message = new byte[14 + response.Body.Length];

            sequenceBytes.CopyTo(message, 0);
            requestCodeBytes.CopyTo(message, 8);
            responseCodeBytes.CopyTo(message, 10);
            requestTypeBytes.CopyTo(message, 12);
            response.Body.CopyTo(message, 14);

            return message;
        }
        public static RemotingResponse ParseResponse(byte[] messageBuffer)
        {
            var requestSequenceBytes = new byte[8];
            var requestCodeBytes = new byte[2];
            var responseCodeBytes = new byte[2];
            var requestTypeBytes = new byte[2];
            var responseBody = new byte[messageBuffer.Length - 14];

            Array.Copy(messageBuffer, 0, requestSequenceBytes, 0, 8);
            Array.Copy(messageBuffer, 8, requestCodeBytes, 0, 2);
            Array.Copy(messageBuffer, 10, responseCodeBytes, 0, 2);
            Array.Copy(messageBuffer, 12, requestTypeBytes, 0, 2);
            Array.Copy(messageBuffer, 14, responseBody, 0, responseBody.Length);

            var requestSequence = BitConverter.ToInt64(requestSequenceBytes, 0);
            var requestCode = BitConverter.ToInt16(requestCodeBytes, 0);
            var responseCode = BitConverter.ToInt16(responseCodeBytes, 0);
            var requestType = BitConverter.ToInt16(requestTypeBytes, 0);

            return new RemotingResponse(requestCode, responseCode, requestType, responseBody, requestSequence);
        }
    }
}
