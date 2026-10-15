using System;
using System.Security.Cryptography;
using PROYECTO_SUBASTA.Application.Services;

namespace PROYECTO_SUBASTA.Infrastructure.Services
{
    public class PasswordHasher : IPasswordHasher
    {
        private const int Iterations = 100000;
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

        public string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] subkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(subkey)}";
        }

        public bool Verificar(string password, string hash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }

            var parts = hash.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out int iterations) || iterations <= 0)
            {
                return false;
            }

            byte[] salt;
            byte[] expectedSubkey;

            try
            {
                salt = Convert.FromBase64String(parts[1]);
                expectedSubkey = Convert.FromBase64String(parts[2]);
            }
            catch (FormatException)
            {
                return false;
            }

            if (salt.Length != SaltSize || expectedSubkey.Length != KeySize)
            {
                return false;
            }

            byte[] actualSubkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, KeySize);
            return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
        }
    }
}
