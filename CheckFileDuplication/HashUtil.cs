using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CheckFileDuplication
{
    internal static class HashUtil
    {
        public static byte[] GetFileHash(string filePath)
        {
            try
            {
                byte[] hash = SHA256.HashData(File.ReadAllBytes(filePath));
                return hash;
            }
            catch (IOException e)
            {
                Console.WriteLine($"I/O Exception: {e.Message}");
                return Array.Empty<byte>();
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine($"Access Exception: {e.Message}");
                return Array.Empty<byte>();
            }
        }
    }
}
