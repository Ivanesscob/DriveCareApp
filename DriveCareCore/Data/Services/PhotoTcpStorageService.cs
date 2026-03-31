using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace DriveCareCore.Data.Services
{
    /// <summary>
    /// TCP-клиент для получения фото с сервера по имени файла.
    /// Формат протокола:
    /// 1) int32 длина команды, затем UTF8 команда ("GET")
    /// 2) int32 длина имени файла, затем UTF8 имя файла
    /// 3) int64 размер файла
    /// 4) байты файла
    /// </summary>
    public static class PhotoTcpStorageService
    {
        public static string DefaultServerIp = "ivanessco.servebeer.com";
        public static int DefaultPort = 5000;

        public static string DownloadPhotoFromServer(string serverFileName)
        {
            return DownloadPhotoFromServer(serverFileName, DefaultServerIp, DefaultPort, Path.GetTempPath());
        }

        public static string DownloadPhotoFromServer(string serverFileName, string serverIp, int port, string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(serverFileName))
                return null;

            if (string.IsNullOrWhiteSpace(serverIp))
                return null;

            if (port <= 0)
                return null;

            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = Path.GetTempPath();

            try
            {
                if (!Directory.Exists(outputFolder))
                    Directory.CreateDirectory(outputFolder);
            }
            catch
            {
                return null;
            }

            string safeName = Path.GetFileName(serverFileName);
            string localFilePath = Path.Combine(outputFolder, safeName);

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(serverIp, port);
                    using (NetworkStream stream = client.GetStream())
                    {
                        // --- отправляем команду GET ---
                        string command = "GET";
                        byte[] cmdBytes = Encoding.UTF8.GetBytes(command);
                        byte[] cmdLength = BitConverter.GetBytes(cmdBytes.Length);
                        stream.Write(cmdLength, 0, 4);
                        stream.Write(cmdBytes, 0, cmdBytes.Length);

                        // --- отправляем имя файла ---
                        byte[] nameBytes = Encoding.UTF8.GetBytes(serverFileName);
                        byte[] nameLength = BitConverter.GetBytes(nameBytes.Length);
                        stream.Write(nameLength, 0, 4);
                        stream.Write(nameBytes, 0, nameBytes.Length);

                        // --- читаем размер файла (8 байт) ---
                        byte[] fileSizeBytes = new byte[8];
                        int readSize = ReadExactly(stream, fileSizeBytes, 8);
                        if (readSize != 8)
                            return null;

                        long fileSize = BitConverter.ToInt64(fileSizeBytes, 0);
                        if (fileSize <= 0)
                            return null;

                        // --- читаем сам файл ---
                        using (FileStream fs = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
                        {
                            byte[] buffer = new byte[8192];
                            long totalRead = 0;
                            while (totalRead < fileSize)
                            {
                                int toRead = (int)Math.Min(buffer.Length, fileSize - totalRead);
                                int bytesRead = stream.Read(buffer, 0, toRead);
                                if (bytesRead == 0)
                                    break;

                                fs.Write(buffer, 0, bytesRead);
                                totalRead += bytesRead;
                            }

                            if (totalRead != fileSize)
                            {
                                fs.Flush();
                                fs.Close();
                                try { File.Delete(localFilePath); } catch { }
                                return null;
                            }
                        }

                        return localFilePath;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static int ReadExactly(NetworkStream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read <= 0)
                    break;
                offset += read;
            }
            return offset;
        }
    }
}
