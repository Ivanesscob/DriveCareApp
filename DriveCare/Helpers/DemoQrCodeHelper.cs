using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DriveCare.Helpers
{
    /// <summary>Демо-QR для экрана оплаты (без внешних библиотек).</summary>
    public static class DemoQrCodeHelper
    {
        public static ImageSource Create(string payload, int sizePx = 200)
        {
            var text = string.IsNullOrWhiteSpace(payload) ? "DRIVECARE" : payload.Trim();
            var modules = 25;
            var cell = Math.Max(4, sizePx / modules);
            var bmp = new WriteableBitmap(modules * cell, modules * cell, 96, 96, PixelFormats.Bgr24, null);
            var data = new byte[bmp.BackBufferStride * bmp.PixelHeight];
            var bits = BuildMatrix(text, modules);

            for (var y = 0; y < modules; y++)
            {
                for (var x = 0; x < modules; x++)
                {
                    var dark = bits[y, x];
                    var b = dark ? (byte)20 : (byte)245;
                    var g = dark ? (byte)20 : (byte)245;
                    var r = dark ? (byte)20 : (byte)245;
                    for (var dy = 0; dy < cell; dy++)
                    {
                        for (var dx = 0; dx < cell; dx++)
                        {
                            var px = (y * cell + dy) * bmp.BackBufferStride + (x * cell + dx) * 3;
                            data[px] = b;
                            data[px + 1] = g;
                            data[px + 2] = r;
                        }
                    }
                }
            }

            bmp.WritePixels(new System.Windows.Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight), data, bmp.BackBufferStride, 0);
            bmp.Freeze();
            return bmp;
        }

        static bool[,] BuildMatrix(string payload, int size)
        {
            var bits = new bool[size, size];
            var hash = Sha256(payload);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (IsFinder(x, y, size))
                    {
                        bits[y, x] = IsFinderDark(x, y);
                        continue;
                    }

                    var i = (y * size + x) % hash.Length;
                    bits[y, x] = (hash[i] & 1) == 1;
                }
            }

            return bits;
        }

        static bool IsFinder(int x, int y, int size)
        {
            return (x < 7 && y < 7) || (x >= size - 7 && y < 7) || (x < 7 && y >= size - 7);
        }

        static bool IsFinderDark(int x, int y)
        {
            var dx = x < 7 ? x : x % 7;
            var dy = y < 7 ? y : y % 7;
            if (dx == 0 || dy == 0 || dx == 6 || dy == 6)
                return true;
            if (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4)
                return true;
            return false;
        }

        static byte[] Sha256(string text)
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
        }
    }
}
