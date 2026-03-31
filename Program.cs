using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class PhotoServer
{
    static string saveFolder = "/home/ivanesscob/PhotoServer/PhotoServerApp/SalesPhotos";
    static int port = 5000;

    const int MaxFileNameUtf8Bytes = 512;

    static async Task Main()
    {
        if (!Directory.Exists(saveFolder))
            Directory.CreateDirectory(saveFolder);

        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"Сервер запущен. Прослушивание порта {port}...");

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
                    byte[] lengthBytes = new byte[4];
                    if (!await TryReadExactAsync(stream, lengthBytes, 4))
                        break;
                    int nameLength = BitConverter.ToInt32(lengthBytes, 0);
                    if (nameLength <= 0 || nameLength > MaxFileNameUtf8Bytes)
                    {
                        Console.WriteLine($"UPLOAD: некорректная длина имени: {nameLength}");
                        break;
                    }

                    byte[] nameBytes = new byte[nameLength];
                    if (!await TryReadExactAsync(stream, nameBytes, nameLength))
                        break;
                    string originalFileName = Encoding.UTF8.GetString(nameBytes);

                    byte[] fileSizeBytes = new byte[8];
                    if (!await TryReadExactAsync(stream, fileSizeBytes, 8))
                        break;
                    long fileSize = BitConverter.ToInt64(fileSizeBytes, 0);
                    if (fileSize < 0 || fileSize > int.MaxValue)
                    {
                        Console.WriteLine($"UPLOAD: некорректный размер файла: {fileSize}");
                        break;
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
                            int bytesRead = await stream.ReadAsync(buffer, 0, toRead);
                            if (bytesRead == 0)
                                break;
                            await fs.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;
                        }
                    }

                    Console.WriteLine($"Файл сохранён: {generatedFileName}");

                    byte[] generatedNameBytes = Encoding.UTF8.GetBytes(generatedFileName);
                    byte[] generatedNameLength = BitConverter.GetBytes(generatedNameBytes.Length);
                    await stream.WriteAsync(generatedNameLength, 0, 4);
                    await stream.WriteAsync(generatedNameBytes, 0, generatedNameBytes.Length);
                }
                else if (command == "GET")
                {
                    try
                    {
                        byte[] lengthBytes = new byte[4];
                        if (!await TryReadExactAsync(stream, lengthBytes, 4))
                            break;
                        int nameLength = BitConverter.ToInt32(lengthBytes, 0);
                        if (nameLength <= 0 || nameLength > MaxFileNameUtf8Bytes)
                        {
                            await stream.WriteAsync(new byte[8], 0, 8);
                            Console.WriteLine($"GET: некорректная длина имени: {nameLength}");
                            continue;
                        }

                        byte[] nameBytes = new byte[nameLength];
                        if (!await TryReadExactAsync(stream, nameBytes, nameLength))
                            break;
                        string requestedFile = Encoding.UTF8.GetString(nameBytes);
                        string safeName = Path.GetFileName(requestedFile);
                        string filePath = Path.Combine(saveFolder, safeName);

                        if (File.Exists(filePath))
                        {
                            byte[] fileData = File.ReadAllBytes(filePath);
                            // Клиент читает Int64 — всегда 8 байт (как при UPLOAD).
                            byte[] sizeOut = BitConverter.GetBytes((long)fileData.Length);
                            await stream.WriteAsync(sizeOut, 0, sizeOut.Length);
                            if (fileData.Length > 0)
                                await stream.WriteAsync(fileData, 0, fileData.Length);
                            Console.WriteLine($"Файл отправлен: {safeName}, размер: {fileData.Length}");
                        }
                        else
                        {
                            await stream.WriteAsync(new byte[8], 0, 8);
                            Console.WriteLine($"Файл не найден: {safeName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при отправке файла: {ex.Message}");
                    }
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
            client.Close();
            Console.WriteLine("Клиент отключен.");
        }
    }

    /// <summary>
    /// Читает ровно count байт. false — соединение закрыто раньше (корректный выход из цикла).
    /// </summary>
    static async Task<bool> TryReadExactAsync(NetworkStream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int bytesRead = await stream.ReadAsync(buffer, offset, count - offset);
            if (bytesRead == 0)
                return false;
            offset += bytesRead;
        }
        return true;
    }
}
