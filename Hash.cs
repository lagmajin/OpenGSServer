using System;
using System.Security.Cryptography;
using System.Text;

namespace OpenGSServer
{
    public static class Hash
    {
        public static string CreateSalt(int length = 16)
        {
            if (length <= 0)
            {
                length = 16;
            }

            var buffer = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(buffer);
            return Convert.ToBase64String(buffer);
        }

        public static string CreateHashWithSalt(string value, string salt)
        {
            value ??= string.Empty;
            salt ??= string.Empty;

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(value + salt);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
