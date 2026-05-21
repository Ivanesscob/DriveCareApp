using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// TCP-сервер: фото (UPLOAD/GET) и онлайн-уведомления чата (CHAT_SUBSCRIBE / CHAT_PUSH).
/// Запуск на Linux: см. start-server.sh или dotnet run в папке PhotoServer.
/// </summary>
class PhotoServer
{
    static string saveFolder = "/home/ivanesscob/PhotoServer/PhotoServerApp/SalesPhotos";
    static int port = 5000;

    const int MaxFileNameUtf8Bytes = 512;
    const int MaxChatPayloadBytes = 4096;

    static async Task Main()
    {
        if (!Directory.Exists(saveFolder))
            Directory.CreateDirectory(saveFolder);

        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"DriveCare TCP: порт {port}, фото + чат.");
        Console.WriteLine($"Папка фото: {saveFolder}");
        Console.WriteLine("Команды: UPLOAD, GET, CHAT_SUBSCRIBE, CHAT_PUSH, CHAT_PING");

        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            _ = HandleClient(client);
        }
    }

    static async Task HandleClient(TcpClient client)
    {
        Console.WriteLine("Клиент подключен.");
        NetworkStream stream = client.GetStream();
        ChatHub.ChatSubscriber chatSub = null;
        try
        {
            while (true)
            {
                byte[] cmdLengthBytes = new byte[4];
                if (!await TryReadExactAsync(stream, cmdLengthBytes, 4))
                    break;

                int cmdLength = BitConverter.ToInt32(cmdLengthBytes, 0);
                if (cmdLength <= 0 || cmdLength > 256)
                {
                    Console.WriteLine($"Некорректная длина команды: {cmdLength}");
                    break;
                }

                byte[] cmdBytes = new byte[cmdLength];
                if (!await TryReadExactAsync(stream, cmdBytes, cmdLength))
                    break;
                string command = Encoding.UTF8.GetString(cmdBytes).ToUpperInvariant();

                if (command == "UPLOAD")
                {
                    await HandleUploadAsync(stream).ConfigureAwait(false);
                }
                else if (command == "GET")
                {
                    await HandleGetAsync(stream).ConfigureAwait(false);
                }
                else if (command == "CHAT_SUBSCRIBE")
                {
                    if (chatSub != null)
                    {
                        Console.WriteLine("CHAT_SUBSCRIBE: повторная подписка");
                        break;
                    }

                    string payload = await ReadUtf8PayloadAsync(stream).ConfigureAwait(false);
                    if (payload == null)
                        break;

                    chatSub = ChatHub.TryRegister(stream, payload);
                    if (chatSub == null)
                    {
                        await WriteInt32Async(stream, 0).ConfigureAwait(false);
                        Console.WriteLine("CHAT_SUBSCRIBE: отклонено — " + payload);
                        break;
                    }

                    await WriteInt32Async(stream, 1).ConfigureAwait(false);
                    Console.WriteLine("CHAT_SUBSCRIBE: " + chatSub.Label);
                }
                else if (command == "CHAT_PUSH")
                {
                    string payload = await ReadUtf8PayloadAsync(stream).ConfigureAwait(false);
                    if (payload == null)
                        break;

                    int sent = await ChatHub.BroadcastNewMessageAsync(payload, stream).ConfigureAwait(false);
                    await WriteInt32Async(stream, 1).ConfigureAwait(false);
                    Console.WriteLine($"CHAT_PUSH: уведомление отправлено {sent} активным TCP-подпискам (открытые приложения/вкладки, не число людей)");
                }
                else if (command == "CHAT_PING")
                {
                    await WriteInt32Async(stream, 1).ConfigureAwait(false);
                }
                else
                {
                    Console.WriteLine($"Неизвестная команда: {command}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
        finally
        {
            if (chatSub != null)
                ChatHub.Unregister(chatSub);
            client.Close();
            Console.WriteLine("Клиент отключен.");
        }
    }

    static async Task HandleUploadAsync(NetworkStream stream)
    {
        byte[] lengthBytes = new byte[4];
        if (!await TryReadExactAsync(stream, lengthBytes, 4))
            return;
        int nameLength = BitConverter.ToInt32(lengthBytes, 0);
        if (nameLength <= 0 || nameLength > MaxFileNameUtf8Bytes)
        {
            Console.WriteLine($"UPLOAD: некорректная длина имени: {nameLength}");
            return;
        }

        byte[] nameBytes = new byte[nameLength];
        if (!await TryReadExactAsync(stream, nameBytes, nameLength))
            return;
        string originalFileName = Encoding.UTF8.GetString(nameBytes);

        byte[] fileSizeBytes = new byte[8];
        if (!await TryReadExactAsync(stream, fileSizeBytes, 8))
            return;
        long fileSize = BitConverter.ToInt64(fileSizeBytes, 0);
        if (fileSize < 0 || fileSize > int.MaxValue)
        {
            Console.WriteLine($"UPLOAD: некорректный размер файла: {fileSize}");
            return;
        }

        string extension = Path.GetExtension(originalFileName);
        string generatedFileName = Guid.NewGuid().ToString() + extension;
        string fullPath = Path.Combine(saveFolder, generatedFileName);

        using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
        {
            byte[] buffer = new byte[8192];
            long totalRead = 0;
            while (totalRead < fileSize)
            {
                int toRead = (int)Math.Min(buffer.Length, fileSize - totalRead);
                int bytesRead = await stream.ReadAsync(buffer, 0, toRead).ConfigureAwait(false);
                if (bytesRead == 0)
                    break;
                await fs.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                totalRead += bytesRead;
            }
        }

        Console.WriteLine($"Файл сохранён: {generatedFileName}");

        byte[] generatedNameBytes = Encoding.UTF8.GetBytes(generatedFileName);
        await stream.WriteAsync(BitConverter.GetBytes(generatedNameBytes.Length), 0, 4).ConfigureAwait(false);
        await stream.WriteAsync(generatedNameBytes, 0, generatedNameBytes.Length).ConfigureAwait(false);
    }

    static async Task HandleGetAsync(NetworkStream stream)
    {
        try
        {
            byte[] lengthBytes = new byte[4];
            if (!await TryReadExactAsync(stream, lengthBytes, 4))
                return;
            int nameLength = BitConverter.ToInt32(lengthBytes, 0);
            if (nameLength <= 0 || nameLength > MaxFileNameUtf8Bytes)
            {
                await stream.WriteAsync(new byte[8], 0, 8).ConfigureAwait(false);
                Console.WriteLine($"GET: некорректная длина имени: {nameLength}");
                return;
            }

            byte[] nameBytes = new byte[nameLength];
            if (!await TryReadExactAsync(stream, nameBytes, nameLength))
                return;
            string requestedFile = Encoding.UTF8.GetString(nameBytes);
            string safeName = Path.GetFileName(requestedFile);
            string filePath = Path.Combine(saveFolder, safeName);

            if (File.Exists(filePath))
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                byte[] sizeOut = BitConverter.GetBytes((long)fileData.Length);
                await stream.WriteAsync(sizeOut, 0, sizeOut.Length).ConfigureAwait(false);
                if (fileData.Length > 0)
                    await stream.WriteAsync(fileData, 0, fileData.Length).ConfigureAwait(false);
                Console.WriteLine($"Файл отправлен: {safeName}, размер: {fileData.Length}");
            }
            else
            {
                await stream.WriteAsync(new byte[8], 0, 8).ConfigureAwait(false);
                Console.WriteLine($"Файл не найден: {safeName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при отправке файла: {ex.Message}");
        }
    }

    static async Task<string> ReadUtf8PayloadAsync(NetworkStream stream)
    {
        byte[] lenBytes = new byte[4];
        if (!await TryReadExactAsync(stream, lenBytes, 4))
            return null;
        int len = BitConverter.ToInt32(lenBytes, 0);
        if (len <= 0 || len > MaxChatPayloadBytes)
            return null;
        byte[] data = new byte[len];
        if (!await TryReadExactAsync(stream, data, len))
            return null;
        return Encoding.UTF8.GetString(data);
    }

    static async Task WriteInt32Async(NetworkStream stream, int value)
    {
        byte[] b = BitConverter.GetBytes(value);
        await stream.WriteAsync(b, 0, 4).ConfigureAwait(false);
    }

    static async Task<bool> TryReadExactAsync(NetworkStream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int bytesRead = await stream.ReadAsync(buffer, offset, count - offset).ConfigureAwait(false);
            if (bytesRead == 0)
                return false;
            offset += bytesRead;
        }
        return true;
    }
}

/// <summary>
/// Подписчики чата и рассылка NEWMSG.
/// Payload: conversationId|workshopId|userId|senderKind (0=клиент, 1=мастерская)
/// </summary>
static class ChatHub
{
    public sealed class ChatSubscriber
    {
        public NetworkStream Stream;
        public readonly object WriteLock = new object();
        public bool ForUser;
        public Guid UserId;
        public HashSet<Guid> WorkshopIds = new HashSet<Guid>();
        public string Label;
    }

    static readonly List<ChatSubscriber> Subscribers = new List<ChatSubscriber>();
    static readonly object SubscribersLock = new object();

    public static ChatSubscriber TryRegister(NetworkStream stream, string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        payload = payload.Trim();
        var sub = new ChatSubscriber { Stream = stream };

        if (payload.StartsWith("U:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(payload.Substring(2).Trim(), out var userId) || userId == Guid.Empty)
                return null;
            sub.ForUser = true;
            sub.UserId = userId;
            sub.Label = "user " + userId;
        }
        else if (payload.StartsWith("W:", StringComparison.OrdinalIgnoreCase))
        {
            var part = payload.Substring(2);
            foreach (var token in part.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Guid.TryParse(token.Trim(), out var ws) && ws != Guid.Empty)
                    sub.WorkshopIds.Add(ws);
            }
            if (sub.WorkshopIds.Count == 0)
                return null;
            sub.ForUser = false;
            sub.Label = "workshops x" + sub.WorkshopIds.Count;
        }
        else
        {
            return null;
        }

        lock (SubscribersLock)
            Subscribers.Add(sub);
        return sub;
    }

    public static void Unregister(ChatSubscriber sub)
    {
        if (sub == null)
            return;
        lock (SubscribersLock)
            Subscribers.Remove(sub);
    }

    public static bool TryParsePushPayload(string payload, out Guid conversationId, out Guid workshopId, out Guid userId, out int senderKind)
    {
        conversationId = workshopId = userId = Guid.Empty;
        senderKind = 0;
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        var parts = payload.Split('|');
        if (parts.Length < 4)
            return false;
        if (!Guid.TryParse(parts[0].Trim(), out conversationId))
            return false;
        if (!Guid.TryParse(parts[1].Trim(), out workshopId))
            return false;
        if (!Guid.TryParse(parts[2].Trim(), out userId))
            return false;
        if (!int.TryParse(parts[3].Trim(), out senderKind))
            return false;
        return conversationId != Guid.Empty;
    }

    public static async Task<int> BroadcastNewMessageAsync(string payload, NetworkStream excludeStream)
    {
        if (!TryParsePushPayload(payload, out var convId, out var workshopId, out var userId, out _))
            return 0;

        List<ChatSubscriber> copy;
        lock (SubscribersLock)
            copy = Subscribers.ToList();

        int sent = 0;
        foreach (var sub in copy)
        {
            bool match = sub.ForUser && sub.UserId == userId
                           || !sub.ForUser && sub.WorkshopIds.Contains(workshopId);
            if (!match)
                continue;
            if (excludeStream != null && ReferenceEquals(sub.Stream, excludeStream))
                continue;

            try
            {
                await SendNewMessageEventAsync(sub, payload).ConfigureAwait(false);
                sent++;
            }
            catch
            {
                Unregister(sub);
            }
        }
        return sent;
    }

    static async Task SendNewMessageEventAsync(ChatSubscriber sub, string payload)
    {
        byte[] eventName = Encoding.UTF8.GetBytes("NEWMSG");
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);

        lock (sub.WriteLock)
        {
            sub.Stream.Write(BitConverter.GetBytes(eventName.Length), 0, 4);
            sub.Stream.Write(eventName, 0, eventName.Length);
            sub.Stream.Write(BitConverter.GetBytes(payloadBytes.Length), 0, 4);
            sub.Stream.Write(payloadBytes, 0, payloadBytes.Length);
            sub.Stream.Flush();
        }
        await Task.CompletedTask;
    }
}
