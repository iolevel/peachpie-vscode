using Newtonsoft.Json;
using Peachpie.LanguageServer.JsonRpc;
using System.IO;
using System.Text;

namespace Peachpie.LanguageServer
{
    internal class MessageWriter
    {
        private Stream _outputStream;

        public MessageWriter(Stream outputStream)
        {
            _outputStream = outputStream;
        }

        public void WriteResponse<T>(object requestId, T result)
        {
            var response = new RpcResponse()
            {
                Id = requestId,
                Result = result
            };

            SerializeAndSend(response);
        }

        public void WriteNotification<T>(string method, T parameters)
        {
            var notification = new RpcNotification<T>()
            {
                Method = method,
                Params = parameters
            };

            SerializeAndSend(notification);
        }

        private void SerializeAndSend<T>(T data)
        {
            lock (this)
            {
                var dataText = JsonConvert.SerializeObject(data);
                var dataBytes = Encoding.UTF8.GetBytes(dataText);

                var headerText = $"Content-Length: {dataBytes.Length}\r\n\r\n";
                var headerBytes = Encoding.ASCII.GetBytes(headerText);

                _outputStream.Write(headerBytes, 0, headerBytes.Length);
                _outputStream.Write(dataBytes, 0, dataBytes.Length);
            }
        }
    }
}
