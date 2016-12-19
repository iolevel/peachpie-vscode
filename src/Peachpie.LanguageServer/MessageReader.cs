using Newtonsoft.Json;
using Peachpie.LanguageServer.JsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer
{
    internal class MessageReader
    {
        private Stream _inputStream;

        public MessageReader(Stream inputStream)
        {
            _inputStream = inputStream;
        }

        public async Task<RpcRequest> ReadRequestAsync()
        {
            // TODO: Handle format errors (such as missing Content-Length)
            int? contentLength = await ReadHeader();

            var buffer = new byte[contentLength.Value];
            int readBytes = 0;
            while (readBytes < contentLength.Value)
            {
                readBytes += await _inputStream.ReadAsync(buffer, readBytes, contentLength.Value - readBytes);
            }

            var requestJson = Encoding.UTF8.GetString(buffer);
            var request = JsonConvert.DeserializeObject<RpcRequest>(requestJson);

            return request;
        }

        private async Task<int?> ReadHeader()
        {
            int? contentLength = null;

            var lineBytes = new List<byte>();

            // We use this workaround to perform the read of the next request asynchronously - there might be
            // significant delays between the requests and we do not want to block the thread during them.
            var singleByteBuffer = new byte[1];
            await _inputStream.ReadAsync(singleByteBuffer, 0, 1);
            int currentByte = singleByteBuffer[0];

            while (true)
            {
                if (currentByte == -1)
                {
                    throw new IOException("Unexpected end of the stream in the request header");
                }
                else if (currentByte == '\r')
                {
                    if (_inputStream.ReadByte() != '\n')
                    {
                        throw new IOException(@"Invalid end of the line, \r\n expected");
                    }

                    string line = Encoding.ASCII.GetString(lineBytes.ToArray());
                    lineBytes.Clear();

                    if (line == "")
                    {
                        break;
                    }

                    var tokens = line.Split(new string[] { ": " }, StringSplitOptions.None);
                    if (tokens[0] == "Content-Length")
                    {
                        contentLength = int.Parse(tokens[1]);
                    }
                }
                else
                {
                    lineBytes.Add((byte)currentByte);
                }

                currentByte = _inputStream.ReadByte();
            }

            return contentLength;
        }
    }
}
