
using System.Net.WebSockets;


namespace AspNetCoreChatRoom
{


    public class ChatWebSocketMiddleware
    {

        private static System.Collections.Concurrent.ConcurrentDictionary<string, WebSocket> _sockets = 
            new System.Collections.Concurrent.ConcurrentDictionary<string, WebSocket>();

        private readonly Microsoft.AspNetCore.Http.RequestDelegate _next;



        public ChatWebSocketMiddleware(Microsoft.AspNetCore.Http.RequestDelegate next)
        {
            _next = next;
        } // End Constructor 



        public async System.Threading.Tasks.Task Invoke(Microsoft.AspNetCore.Http.HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                await _next.Invoke(context);
                return;
            }

            System.Threading.CancellationToken ct = context.RequestAborted;
            WebSocket currentSocket = await context.WebSockets.AcceptWebSocketAsync();
            string socketId = System.Guid.NewGuid().ToString();

            _sockets.TryAdd(socketId, currentSocket);

            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                string response = await ReceiveStringAsync(currentSocket, ct);
                if(string.IsNullOrEmpty(response))
                {
                    if(currentSocket.State != System.Net.WebSockets.WebSocketState.Open)
                    {
                        break;
                    }

                    continue;
                }

                foreach (System.Collections.Generic.KeyValuePair<string, WebSocket> socket in _sockets)
                {
                    if(socket.Value.State != System.Net.WebSockets.WebSocketState.Open)
                    {
                        continue;
                    }

                    await SendStringAsync(socket.Value, response, ct);
                }
            }

            WebSocket dummy;
            _sockets.TryRemove(socketId, out dummy);

            await currentSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Closing", ct);
            currentSocket.Dispose();
        } // End Sub Invoke 




        private static System.Threading.Tasks.Task SendStringAsync(
            WebSocket socket, string data, System.Threading.CancellationToken ct)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(data);
            System.ArraySegment<byte> segment = new System.ArraySegment<byte>(buffer);

            return socket.SendAsync(segment, System.Net.WebSockets.WebSocketMessageType.Text, true, ct);
        }


        private static System.Threading.Tasks.Task SendStringAsync(WebSocket socket, string data)
        {
            return SendStringAsync(socket, data, default(System.Threading.CancellationToken));
        }


        private static async System.Threading.Tasks.Task<string> ReceiveStringAsync(
            WebSocket socket, System.Threading.CancellationToken ct)
        {
            System.ArraySegment<byte> buffer = new System.ArraySegment<byte>(new byte[8192]);
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                System.Net.WebSockets.WebSocketReceiveResult result;
                do
                {
                    ct.ThrowIfCancellationRequested();

                    result = await socket.ReceiveAsync(buffer, ct);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                } while (!result.EndOfMessage);

                ms.Seek(0, System.IO.SeekOrigin.Begin);
                if (result.MessageType != System.Net.WebSockets.WebSocketMessageType.Text)
                {
                    return null;
                }

                // Encoding UTF8: https://tools.ietf.org/html/rfc6455#section-5.6
                using (System.IO.TextReader reader = new System.IO.StreamReader(ms, System.Text.Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }


        private static async System.Threading.Tasks.Task<string> ReceiveStringAsync(
            WebSocket socket)
        {
            return await ReceiveStringAsync(socket, default(System.Threading.CancellationToken));
        }


    }


}
