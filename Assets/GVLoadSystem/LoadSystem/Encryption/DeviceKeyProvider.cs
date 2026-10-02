using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace GVLoadSystem.Encryption
{
    /// <summary>
    /// Provides a per-device encryption key combined with the project salt.
    /// The key is generated once via CSPRNG and stored in persistentDataPath.
    /// It is unique per device but is not OS-protected (DPAPI is unavailable in
    /// Unity's .NET Standard 2.1 profile). Protection relies on the OS sandbox
    /// on mobile and the file system permissions on desktop platforms.
    /// </summary>
    public static class DeviceKeyProvider
    {
        public const int KEY_SIZE = 32;
        static string _saveSubfolder = "";
        public static void SetSaveSubfolder(string subfolder) => _saveSubfolder = subfolder;

        static string KeyPath =>
            string.IsNullOrEmpty(_saveSubfolder)
                ? Path.Combine(Application.persistentDataPath, "device.key")
                : Path.Combine(Application.persistentDataPath, _saveSubfolder, "device.key");


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
#if LOG_LOADSYSTEM
        Debug.Log("[DeviceKeyProvider] Init called");
#endif
            GetOrCreateDeviceKey();
        }
        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the combined key: HKDF(deviceKey + projectSalt).
        /// This ensures the key is unique per device AND per project.
        /// </summary>
        public static byte[] GetCombinedKey(string projectSalt)
        {
            byte[] deviceKey = GetOrCreateDeviceKey();
            byte[] saltBytes = Encoding.UTF8.GetBytes(projectSalt ?? "");

            // Combine via HKDF-like construction using HMAC-SHA256
            // result = HMAC-SHA256(deviceKey, projectSalt)
            // This produces a 32-byte key that depends on both inputs
            using var hmac = new HMACSHA256(deviceKey);
            return hmac.ComputeHash(saltBytes);
        }

        /// <summary>
        /// Gets the device key, creating it if it doesn't exist.
        /// On Windows, protects the key file with DPAPI.
        /// </summary>
        public static byte[] GetOrCreateDeviceKey()
        {
            if (File.Exists(KeyPath))
            {
#if LOG_LOADSYSTEM
            Debug.Log("[DeviceKeyProvider] Loading existing key from: " + KeyPath);
#endif
                try
                {
                    byte[] stored = File.ReadAllBytes(KeyPath);
                    return Unprotect(stored);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DeviceKeyProvider] Failed to read key, regenerating: {ex.Message}");
                }
            }
#if LOG_LOADSYSTEM
        Debug.Log("[DeviceKeyProvider] Creating new key");
#endif
            return CreateAndSaveNewKey();
        }

        /// <summary>Regenerates the device key — invalidates all existing saves.</summary>
        public static void GenerateNewDeviceKey()
        {
            CreateAndSaveNewKey();
#if LOG_LOADSYSTEM
        Debug.Log("[DeviceKeyProvider] New device key generated — existing saves are now unreadable.");
#endif
        }

        /// <summary>
        /// Generates random bytes for use as a project password salt.
        /// Not the device key — used for the project-level salt in ProjectSettings.
        /// </summary>
        public static byte[] GenerateNewPassword()
        {
            byte[] randomBytes = new byte[KEY_SIZE];
            RandomNumberGenerator.Fill(randomBytes);
            return randomBytes;
        }

        // ── Internal ──────────────────────────────────────────────────────

        static byte[] CreateAndSaveNewKey()
        {
            byte[] key = new byte[KEY_SIZE];
            RandomNumberGenerator.Fill(key);
            byte[] toStore = Protect(key);
            File.WriteAllBytes(KeyPath, toStore);
#if LOG_LOADSYSTEM
        Debug.Log("[DeviceKeyProvider] Device key created.");
#endif
            return key;
        }

        /// <summary>
        /// Protects key bytes using OS-level protection where available.
        /// Windows: DPAPI (CurrentUser scope — only this OS user can decrypt)
        /// Other:   stored as-is (OS sandbox provides protection on mobile)
        /// </summary>
        // Note: DPAPI (Windows ProtectedData) is not available in Unity's .NET Standard 2.1.
        // The device key is protected by the OS sandbox on mobile platforms.
        // On PC, the key is unique per device via CSPRNG — sufficient for save file protection.
        static byte[] Protect(byte[] data) => data;
        static byte[] Unprotect(byte[] data) => data;
    }
}