using DriveCareCore.Data.Services;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DriveCareCore.Messaging
{
    /// <summary>
    /// TCP-уведомления чата через тот же сервер, что и фото (порт 5000).
    /// Сообщения по-прежнему в SQL; сервер только рассылает «пришло новое».
    /// </summary>
    public static class WorkshopChatRealtimeClient
    {
        public static string DefaultServerIp => PhotoTcpStorageService.DefaultServerIp;
        public static int DefaultPort => PhotoTcpStorageService.DefaultPort;

        public static event Action<ChatPushEventArgs> MessageReceived;

        static CancellationTokenSource _cts;
        static Task _listenTask;

        public static void StartForUser(Guid userId)
        {
            if (userId == Guid.Empty)
                return;
            StartInternal("U:" + userId);
        }

        public static void StartForWorkshops(IEnumerable<Guid> workshopIds)
        {
            if (workshopIds == null)
                return;
            var ids = new List<Guid>();
            foreach (var id in workshopIds)
            {
                if (id != Guid.Empty && !ids.Contains(id))
                    ids.Add(id);
            }
            if (ids.Count == 0)
                return;
            StartInternal("W:" + string.Join(";", ids));
        }

        public static void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            _cts = null;
            _listenTask = null;
        }

        public static void NotifyNewMessage(Guid conversationId, Guid workshopId, Guid userId, MessageSenderKind senderKind)
        {
            if (conversationId == Guid.Empty)
                return;
            var payload = BuildPayload(conversationId, workshopId, userId, senderKind);
            Task.Run(() => SendPushAsync(payload));
        }

        public static string BuildPayload(Guid conversationId, Guid workshopId, Guid userId, MessageSenderKind senderKind)
        {
            return conversationId + "|" + workshopId + "|" + userId + "|" + (int)senderKind;
        }

        static void StartInternal(string subscribePayload)
        {
            Stop();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _listenTask = Task.Run(() => ListenLoopAsync(subscribePayload, token), token);
        }

        static async Task ListenLoopAsync(string subscribePayload, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await RunSubscribeSessionAsync(subscribePayload, token).ConfigureAwait(false);
                }
                catch
                {
                }

                if (token.IsCancellationRequested)
                    break;
                try
                {
                    await Task.Delay(3000, token).ConfigureAwait(false);
                }
                catch
                {
                    break;
                }
            }
        }

        static async Task RunSubscribeSessionAsync(string subscribePayload, CancellationToken token)
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(DefaultServerIp, DefaultPort).ConfigureAwait(false);
                using (var stream = client.GetStream())
                {
                    await SendCommandWithPayloadAsync(stream, "CHAT_SUBSCRIBE", subscribePayload).ConfigureAwait(false);
                    var ok = await ReadInt32Async(stream).ConfigureAwait(false);
                    if (ok != 1)
                        return;

                    while (!token.IsCancellationRequested)
                    {
                        if (!client.Connected)
                            break;

                        if (!await WaitForDataAsync(stream, 25000, token).ConfigureAwait(false))
                        {
                            await SendCommandAsync(stream, "CHAT_PING").ConfigureAwait(false);
                            await ReadInt32Async(stream).ConfigureAwait(false);
                            continue;
                        }

                        byte[] cmdLenBytes = new byte[4];
                        if (!await ReadExactAsync(stream, cmdLenBytes, 4).ConfigureAwait(false))
                            break;
                        int cmdLen = BitConverter.ToInt32(cmdLenBytes, 0);
                        if (cmdLen <= 0 || cmdLen > 64)
                            break;

                        byte[] cmdBytes = new byte[cmdLen];
                        if (!await ReadExactAsync(stream, cmdBytes, cmdLen).ConfigureAwait(false))
                            break;
                        string cmd = Encoding.UTF8.GetString(cmdBytes);
                        if (!string.Equals(cmd, "NEWMSG", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string payload = await ReadUtf8PayloadAsync(stream).ConfigureAwait(false);
                        if (payload == null)
                            continue;

                        if (!TryParsePayload(payload, out var args))
                            continue;

                        MessageReceived?.Invoke(args);
                    }
                }
            }
        }

        static async Task SendPushAsync(string payload)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect(DefaultServerIp, DefaultPort);
                    using (var stream = client.GetStream())
                    {
                        await SendCommandWithPayloadAsync(stream, "CHAT_PUSH", payload).ConfigureAwait(false);
                        await ReadInt32Async(stream).ConfigureAwait(false);
                    }
                }
            }
            catch
            {
            }
        }

        static async Task SendCommandAsync(NetworkStream stream, string command)
        {
            byte[] cmd = Encoding.UTF8.GetBytes(command);
            await stream.WriteAsync(BitConverter.GetBytes(cmd.Length), 0, 4).ConfigureAwait(false);
            await stream.WriteAsync(cmd, 0, cmd.Length).ConfigureAwait(false);
        }

        static async Task SendCommandWithPayloadAsync(NetworkStream stream, string command, string payload)
        {
            await SendCommandAsync(stream, command).ConfigureAwait(false);
            byte[] data = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            await stream.WriteAsync(BitConverter.GetBytes(data.Length), 0, 4).ConfigureAwait(false);
            await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
        }

        static async Task<string> ReadUtf8PayloadAsync(NetworkStream stream)
        {
            byte[] lenBytes = new byte[4];
            if (!await ReadExactAsync(stream, lenBytes, 4).ConfigureAwait(false))
                return null;
            int len = BitConverter.ToInt32(lenBytes, 0);
            if (len <= 0 || len > 4096)
                return null;
            byte[] buf = new byte[len];
            if (!await ReadExactAsync(stream, buf, len).ConfigureAwait(false))
                return null;
            return Encoding.UTF8.GetString(buf);
        }

        static async Task<int> ReadInt32Async(NetworkStream stream)
        {
            byte[] b = new byte[4];
            if (!await ReadExactAsync(stream, b, 4).ConfigureAwait(false))
                return 0;
            return BitConverter.ToInt32(b, 0);
        }

        static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer, offset, count - offset).ConfigureAwait(false);
                if (read <= 0)
                    return false;
                offset += read;
            }
            return true;
        }

        static async Task<bool> WaitForDataAsync(NetworkStream stream, int timeoutMs, CancellationToken token)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline && !token.IsCancellationRequested)
            {
                if (stream.DataAvailable)
                    return true;
                await Task.Delay(200, token).ConfigureAwait(false);
            }
            return stream.DataAvailable;
        }

        public static bool TryParsePayload(string payload, out ChatPushEventArgs args)
        {
            args = null;
            if (string.IsNullOrWhiteSpace(payload))
                return false;

            var parts = payload.Split('|');
            if (parts.Length < 4)
                return false;
            if (!Guid.TryParse(parts[0].Trim(), out var convId))
                return false;
            if (!Guid.TryParse(parts[1].Trim(), out var workshopId))
                return false;
            if (!Guid.TryParse(parts[2].Trim(), out var userId))
                return false;
            if (!int.TryParse(parts[3].Trim(), out var kind))
                return false;

            args = new ChatPushEventArgs(convId, workshopId, userId, (MessageSenderKind)kind);
            return true;
        }
    }

    public sealed class ChatPushEventArgs
    {
        public Guid ConversationId { get; }
        public Guid WorkshopId { get; }
        public Guid UserId { get; }
        public MessageSenderKind SenderKind { get; }

        public ChatPushEventArgs(Guid conversationId, Guid workshopId, Guid userId, MessageSenderKind senderKind)
        {
            ConversationId = conversationId;
            WorkshopId = workshopId;
            UserId = userId;
            SenderKind = senderKind;
        }
    }
}
