using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LiveSplit.Web;

/// <summary>
/// File-backed credential store used on non-Windows platforms. Each credential is written to
/// <c>~/.config/LiveSplit/credentials/{sha-of-name}.cred</c> as an AES-GCM-encrypted blob, with
/// a key derived from the host's <c>/etc/machine-id</c> + a fixed salt.
///
/// Uses <c>/etc/machine-id</c> to bind blobs to this physical machine, so a credential file
/// copied to another box won't decrypt. We do NOT bind to the user's password — that would
/// require GNOME Keyring or KWallet integration and a hard system dependency on those
/// services.
/// </summary>
internal sealed class LinuxCredentialStore : ICredentialStore
{
    private const int SaltLength = 16;
    private const string KdfSalt = "LiveSplit-Linux-Credential-v1";

    private static string DirectoryPath
    {
        get
        {
            string xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(xdgConfig))
            {
                string home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
                xdgConfig = Path.Combine(home, ".config");
            }

            return Path.Combine(xdgConfig, "LiveSplit", "credentials");
        }
    }

    public Credential Read(string applicationName)
    {
        string path = PathFor(applicationName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            byte[] payload = File.ReadAllBytes(path);
            (string user, string secret) = Decrypt(payload);
            return new Credential(CredentialType.Generic, applicationName, user, secret);
        }
        catch
        {
            // Corrupted or written by an older format — treat as missing.
            return null;
        }
    }

    public void Write(string applicationName, string userName, string secret)
    {
        Directory.CreateDirectory(DirectoryPath);
        string path = PathFor(applicationName);
        byte[] encrypted = Encrypt(userName ?? Environment.UserName, secret ?? string.Empty);
        File.WriteAllBytes(path, encrypted);

        // Tighten permissions to 0600 — this is best-effort; on filesystems without POSIX modes
        // (e.g. NTFS via ntfs-3g) the chmod is a no-op and the file falls back to umask defaults.
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Ignore — best-effort hardening only.
        }
    }

    public void Delete(string applicationName)
    {
        string path = PathFor(applicationName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string PathFor(string applicationName)
    {
        // Hash the name so file names are filesystem-safe and don't leak secret target labels
        // into directory listings.
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(applicationName ?? string.Empty));
        return Path.Combine(DirectoryPath, Convert.ToHexString(hash) + ".cred");
    }

    // Encrypted layout:
    //   [16 bytes salt][12 bytes IV][16 bytes auth tag][N bytes ciphertext]
    // Plaintext is "username\0password" UTF-8.

    private static byte[] Encrypt(string user, string secret)
    {
        byte[] salt = new byte[SaltLength];
        RandomNumberGenerator.Fill(salt);
        byte[] key = DeriveKey(salt);

        byte[] plain = Encoding.UTF8.GetBytes(user + "\0" + secret);
        byte[] iv = new byte[12];
        RandomNumberGenerator.Fill(iv);

        byte[] cipher = new byte[plain.Length];
        byte[] tag = new byte[16];

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(iv, plain, cipher, tag);

        byte[] result = new byte[salt.Length + iv.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(iv, 0, result, salt.Length, iv.Length);
        Buffer.BlockCopy(tag, 0, result, salt.Length + iv.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, result, salt.Length + iv.Length + tag.Length, cipher.Length);

        Array.Clear(key, 0, key.Length);
        return result;
    }

    private static (string user, string secret) Decrypt(byte[] payload)
    {
        if (payload.Length < SaltLength + 12 + 16)
        {
            throw new CryptographicException("Credential file is truncated.");
        }

        byte[] salt = new byte[SaltLength];
        byte[] iv = new byte[12];
        byte[] tag = new byte[16];
        Buffer.BlockCopy(payload, 0, salt, 0, SaltLength);
        Buffer.BlockCopy(payload, SaltLength, iv, 0, 12);
        Buffer.BlockCopy(payload, SaltLength + 12, tag, 0, 16);

        int cipherLen = payload.Length - SaltLength - 12 - 16;
        byte[] cipher = new byte[cipherLen];
        Buffer.BlockCopy(payload, SaltLength + 12 + 16, cipher, 0, cipherLen);

        byte[] key = DeriveKey(salt);
        byte[] plain = new byte[cipherLen];

        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(iv, cipher, tag, plain);

        Array.Clear(key, 0, key.Length);

        string text = Encoding.UTF8.GetString(plain);
        int sep = text.IndexOf('\0');
        if (sep < 0)
        {
            return (string.Empty, text);
        }

        return (text[..sep], text[(sep + 1)..]);
    }

    private static byte[] DeriveKey(byte[] salt)
    {
        // Mix /etc/machine-id (32 hex chars on systemd hosts) with a fixed app-level salt so
        // credentials are bound to the host but not predictable from the salt alone. If the
        // machine-id read fails we fall through to the username; log it once so users with
        // unusual systemd setups can diagnose lost credentials after a host change.
        string machineId = ReadMachineId();
        if (string.IsNullOrEmpty(machineId))
        {
            if (!_machineIdFallbackLogged)
            {
                _machineIdFallbackLogged = true;
                Options.Log.Warning(
                    "LinuxCredentialStore: machine-id unavailable; deriving key from username instead. Saved credentials will rotate if the username changes.");
            }

            machineId = Environment.UserName;
        }

        byte[] secret = Encoding.UTF8.GetBytes(machineId + KdfSalt);

        using var pbkdf = new Rfc2898DeriveBytes(secret, salt, 100_000, HashAlgorithmName.SHA256);
        return pbkdf.GetBytes(32);
    }

    private static bool _machineIdFallbackLogged;

    private static string ReadMachineId()
    {
        try
        {
            // Standard systemd path; falls back to /var/lib/dbus/machine-id on older distros.
            foreach (string path in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
            {
                if (File.Exists(path))
                {
                    string content = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        return content;
                    }
                }
            }
        }
        catch
        {
            // Permissions or transient I/O — fall through.
        }

        return null;
    }
}
