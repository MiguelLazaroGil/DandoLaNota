using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace GVLoadSystem.Encryption
{
    public enum EncryptionMethod
    {
        None,
        Aes256_HmacSha256,
        Aes256_Only,
        Xor
    }

    /// <summary>
    /// Encrypts/decrypts JSON save files.
    /// Password parameter is the project salt from GroupValuesProjectSettings.
    /// The actual encryption key is derived from: HMAC(deviceKey, projectSalt)
    /// making it unique per device AND per project.
    /// </summary>
    public static class JsonEncrypter
    {
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int HmacSize = 32;
        private const int Iterations = 100_000;
        private static int KeySize => DeviceKeyProvider.KEY_SIZE;

        // ── Public API ────────────────────────────────────────────────────

        public static void EncryptToFile(
            string path, string json, string projectSalt, EncryptionMethod method)
        {
            if (Directory.Exists(path))
            {
                Debug.LogError("[JsonEncrypter] Path is a directory, not a file.");
                return;
            }

            string combinedKey = DeriveCombinedKeyString(projectSalt);

            byte[] data = method switch
            {
                EncryptionMethod.None => Encoding.UTF8.GetBytes(json),
                EncryptionMethod.Aes256_HmacSha256 => EncryptAesHmac(json, combinedKey),
                EncryptionMethod.Aes256_Only => EncryptAesOnly(json, combinedKey),
                EncryptionMethod.Xor => EncryptXor(json, combinedKey),
                _ => throw new NotSupportedException()
            };

            File.WriteAllBytes(path, data);
#if LOG_LOADSYSTEM
        Debug.Log($"[JsonEncrypter] Saved with {method}");
#endif
        }

        public static string DecryptFromFile(
            string path, string projectSalt, EncryptionMethod method)
        {
            byte[] fileData = File.ReadAllBytes(path);
            string combinedKey = DeriveCombinedKeyString(projectSalt);
#if LOG_LOADSYSTEM
        Debug.Log($"[JsonEncrypter] Loaded with {method}");
#endif

            return method switch
            {
                EncryptionMethod.None => Encoding.UTF8.GetString(fileData),
                EncryptionMethod.Aes256_HmacSha256 => DecryptAesHmac(fileData, combinedKey),
                EncryptionMethod.Aes256_Only => DecryptAesOnly(fileData, combinedKey),
                EncryptionMethod.Xor => DecryptXor(fileData, combinedKey),
                _ => throw new NotSupportedException()
            };
        }
        /// <summary>
        /// Overload for async use — combinedKey already derived on main thread.
        /// </summary>
        public static void EncryptToFile(
            string path, string json, EncryptionMethod method, string combinedKey)
        {
            if (Directory.Exists(path))
            {
                Debug.LogError("[JsonEncrypter] Path is a directory, not a file.");
                return;
            }

            byte[] data = method switch
            {
                EncryptionMethod.None => Encoding.UTF8.GetBytes(json),
                EncryptionMethod.Aes256_HmacSha256 => EncryptAesHmac(json, combinedKey),
                EncryptionMethod.Aes256_Only => EncryptAesOnly(json, combinedKey),
                EncryptionMethod.Xor => EncryptXor(json, combinedKey),
                _ => throw new NotSupportedException()
            };

            File.WriteAllBytes(path, data);
        }

        /// <summary>
        /// Overload for async use — combinedKey already derived on main thread.
        /// </summary>
        public static string DecryptFromFile(
            string path, EncryptionMethod method, string combinedKey)
        {
            byte[] fileData = File.ReadAllBytes(path);

            return method switch
            {
                EncryptionMethod.None => Encoding.UTF8.GetString(fileData),
                EncryptionMethod.Aes256_HmacSha256 => DecryptAesHmac(fileData, combinedKey),
                EncryptionMethod.Aes256_Only => DecryptAesOnly(fileData, combinedKey),
                EncryptionMethod.Xor => DecryptXor(fileData, combinedKey),
                _ => throw new NotSupportedException()
            };
        }
        // ── Key derivation ────────────────────────────────────────────────

        /// <summary>
        /// Derives the combined key from deviceKey + projectSalt and returns it
        /// as a Base64 string suitable for use as a PBKDF2 password.
        /// </summary>
        static string DeriveCombinedKeyString(string projectSalt)
        {
            byte[] combined = DeviceKeyProvider.GetCombinedKey(projectSalt);
            string key = Convert.ToBase64String(combined);
#if LOG_LOADSYSTEM
        Debug.Log($"[JsonEncrypter] Combined key hash: {key.Substring(0, 8)}... salt: '{projectSalt?.Substring(0, Math.Min(8, projectSalt?.Length ?? 0))}'");
#endif
            return key;
        }

        // ── AES-256 + HMAC-SHA256 ─────────────────────────────────────────

        private static byte[] EncryptAesHmac(string json, string password)
        {
            byte[] salt = new byte[SaltSize];
            RandomNumberGenerator.Fill(salt);

            using var kdf = new Rfc2898DeriveBytes(
                password, salt, Iterations, HashAlgorithmName.SHA256);

            byte[] aesKey = kdf.GetBytes(KeySize);
            byte[] hmacKey = kdf.GetBytes(KeySize);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = aesKey;
            aes.GenerateIV();

            byte[] plainBytes;
            byte[] cipherBytes;

            plainBytes = Encoding.UTF8.GetBytes(json);
            using (var enc = aes.CreateEncryptor())
                cipherBytes = enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            byte[] dataToAuth = Combine(salt, aes.IV, cipherBytes);
            byte[] hmac;
            using (var hmacSha = new HMACSHA256(hmacKey))
                hmac = hmacSha.ComputeHash(dataToAuth);

            return Combine(dataToAuth, hmac);
        }

        private static string DecryptAesHmac(byte[] fileData, string password)
        {
            if (fileData.Length < SaltSize + IvSize + HmacSize)
                throw new CryptographicException("File too small or corrupted.");

            byte[] salt = fileData[..SaltSize];
            byte[] iv = fileData[SaltSize..(SaltSize + IvSize)];
            byte[] hmacStored = fileData[^HmacSize..];
            byte[] cipherBytes = fileData[(SaltSize + IvSize)..^HmacSize];

            using var kdf = new Rfc2898DeriveBytes(
                password, salt, Iterations, HashAlgorithmName.SHA256);

            byte[] aesKey = kdf.GetBytes(KeySize);
            byte[] hmacKey = kdf.GetBytes(KeySize);

            byte[] dataToAuth = Combine(salt, iv, cipherBytes);
            using (var hmacSha = new HMACSHA256(hmacKey))
            {
                byte[] computed = hmacSha.ComputeHash(dataToAuth);
                if (!CryptographicOperations.FixedTimeEquals(hmacStored, computed))
                    throw new CryptographicException(
                        "HMAC validation failed. File may be corrupted or wrong device.");
            }

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = aesKey;
            aes.IV = iv;

            using var dec = aes.CreateDecryptor();
            byte[] plain = dec.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plain);
        }

        // ── AES-256 Only ──────────────────────────────────────────────────

        private static byte[] EncryptAesOnly(string json, string password)
        {
            byte[] salt = new byte[SaltSize];
            RandomNumberGenerator.Fill(salt);

            using var kdf = new Rfc2898DeriveBytes(
                password, salt, Iterations, HashAlgorithmName.SHA256);
            byte[] key = kdf.GetBytes(KeySize);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            byte[] plain = Encoding.UTF8.GetBytes(json);
            using var enc = aes.CreateEncryptor();
            byte[] cipher = enc.TransformFinalBlock(plain, 0, plain.Length);

            return Combine(salt, aes.IV, cipher);
        }

        private static string DecryptAesOnly(byte[] fileData, string password)
        {
            byte[] salt = fileData[..SaltSize];
            byte[] iv = fileData[SaltSize..(SaltSize + IvSize)];
            byte[] cipherBytes = fileData[(SaltSize + IvSize)..];

            using var kdf = new Rfc2898DeriveBytes(
                password, salt, Iterations, HashAlgorithmName.SHA256);
            byte[] key = kdf.GetBytes(KeySize);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var dec = aes.CreateDecryptor();
            byte[] plain = dec.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plain);
        }

        // ── XOR (ofuscación) ──────────────────────────────────────────────

        private static byte[] EncryptXor(string json, string password)
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            byte[] key = Encoding.UTF8.GetBytes(password);
            for (int i = 0; i < data.Length; i++)
                data[i] ^= key[i % key.Length];
            return data;
        }

        private static string DecryptXor(byte[] fileData, string password)
        {
            byte[] key = Encoding.UTF8.GetBytes(password);
            for (int i = 0; i < fileData.Length; i++)
                fileData[i] ^= key[i % key.Length];
            return Encoding.UTF8.GetString(fileData);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static byte[] Combine(params byte[][] arrays)
        {
            int total = 0;
            foreach (var a in arrays) total += a.Length;
            byte[] result = new byte[total];
            int offset = 0;
            foreach (var a in arrays)
            {
                Buffer.BlockCopy(a, 0, result, offset, a.Length);
                offset += a.Length;
            }
            return result;
        }
    }
}