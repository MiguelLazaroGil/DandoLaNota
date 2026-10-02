using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace GVLoadSystem.Encryption
{
    public static class PasswordGenerator
    {
        public static string Generate(string projectSalt)
        {
            byte[] deviceKey = DeviceKeyProvider.GetOrCreateDeviceKey();

            string deviceId = SystemInfo.deviceUniqueIdentifier;

            string combined =
                Convert.ToBase64String(deviceKey) +
                deviceId +
                projectSalt;

            byte[] bytes = Encoding.UTF8.GetBytes(combined);

            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);

            return Convert.ToBase64String(hash);
        }
        public static string GenerateNewPassword(string pS)
        {
            DeviceKeyProvider.GenerateNewDeviceKey();
            return Generate(pS);
        }

    }
}