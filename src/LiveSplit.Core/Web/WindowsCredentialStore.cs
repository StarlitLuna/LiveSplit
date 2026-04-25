using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

using Microsoft.Win32.SafeHandles;

namespace LiveSplit.Web;

/// <summary>
/// Windows credential store backed by the Win32 Credential Manager API
/// (<c>CredReadW</c> / <c>CredWriteW</c> / <c>CredDeleteW</c>). DPAPI-encrypted by the OS; per
/// user, persistence == LocalMachine matches LiveSplit's pre-Phase 7 behavior.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsCredentialStore : ICredentialStore
{
    public Credential Read(string applicationName)
    {
        bool read = CredRead(applicationName, CredentialType.Generic, 0, out IntPtr nCredPtr);
        if (read)
        {
            using var critCred = new CriticalCredentialHandle(nCredPtr);
            CREDENTIAL cred = critCred.GetCredential();
            return ReadCredential(cred);
        }

        return null;
    }

    private static Credential ReadCredential(CREDENTIAL credential)
    {
        string applicationName = Marshal.PtrToStringUni(credential.TargetName);
        string userName = Marshal.PtrToStringUni(credential.UserName);
        string secret = null;
        if (credential.CredentialBlob != IntPtr.Zero)
        {
            secret = Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
        }

        return new Credential(credential.Type, applicationName, userName, secret);
    }

    public void Write(string applicationName, string userName, string secret)
    {
        byte[] byteArray = secret == null ? null : Encoding.Unicode.GetBytes(secret);
        if (Environment.OSVersion.Version < new Version(6, 1) /* Windows 7 */)
        {
            if (byteArray != null && byteArray.Length > 512)
            {
                throw new ArgumentOutOfRangeException(nameof(secret), "The secret message has exceeded 512 bytes.");
            }
        }
        else
        {
            if (byteArray != null && byteArray.Length > 512 * 5)
            {
                throw new ArgumentOutOfRangeException(nameof(secret), "The secret message has exceeded 2560 bytes.");
            }
        }

        var credential = new CREDENTIAL
        {
            AttributeCount = 0,
            Attributes = IntPtr.Zero,
            Comment = IntPtr.Zero,
            TargetAlias = IntPtr.Zero,
            Type = CredentialType.Generic,
            Persist = (uint)CredentialPersistence.LocalMachine,
            CredentialBlobSize = (uint)(byteArray == null ? 0 : byteArray.Length),
            TargetName = Marshal.StringToCoTaskMemUni(applicationName),
            CredentialBlob = Marshal.StringToCoTaskMemUni(secret),
            UserName = Marshal.StringToCoTaskMemUni(userName ?? Environment.UserName),
        };

        bool written = CredWrite(ref credential, 0);
        Marshal.FreeCoTaskMem(credential.TargetName);
        Marshal.FreeCoTaskMem(credential.CredentialBlob);
        Marshal.FreeCoTaskMem(credential.UserName);

        if (!written)
        {
            int lastError = Marshal.GetLastWin32Error();
            throw new Exception($"CredWrite failed with the error code {lastError}.");
        }
    }

    public void Delete(string applicationName)
    {
        if (Read(applicationName) is null)
        {
            return;
        }

        bool success = CredDelete(applicationName, CredentialType.Generic, 0);
        if (!success)
        {
            int lastError = Marshal.GetLastWin32Error();
            throw new Win32Exception(lastError);
        }
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, CredentialType type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, CredentialType type, int reservedFlag);

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
    private static extern bool CredFree([In] IntPtr cred);

    private enum CredentialPersistence : uint
    {
        Session = 1,
        LocalMachine,
        Enterprise,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public CredentialType Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    private sealed class CriticalCredentialHandle : CriticalHandleZeroOrMinusOneIsInvalid
    {
        public CriticalCredentialHandle(IntPtr preexistingHandle)
        {
            SetHandle(preexistingHandle);
        }

        public CREDENTIAL GetCredential()
        {
            if (!IsInvalid)
            {
                var credential = (CREDENTIAL)Marshal.PtrToStructure(handle, typeof(CREDENTIAL));
                return credential;
            }

            throw new InvalidOperationException("Invalid CriticalHandle!");
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                CredFree(handle);
                SetHandleAsInvalid();
                return true;
            }

            return false;
        }
    }
}
