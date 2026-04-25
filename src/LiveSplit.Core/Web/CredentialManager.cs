/*

MIT License

Copyright(c) 2017 Gérald Barré

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.Runtime.InteropServices;

namespace LiveSplit.Web;

/// <summary>
/// Cross-platform façade for the OS-level credential vault. The Windows backing uses the
/// Win32 <c>CredReadW</c> / <c>CredWriteW</c> family (DPAPI-encrypted under the hood); the
/// Linux backing stores AES-encrypted blobs in <c>~/.config/LiveSplit/credentials/{name}</c>
/// with a key derived from <c>/etc/machine-id</c>. macOS isn't yet wired up — falls through
/// to the Linux file-backed implementation when invoked there.
/// </summary>
public static class CredentialManager
{
    private static readonly ICredentialStore Backend = SelectBackend();

    private static ICredentialStore SelectBackend()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsCredentialStore();
        }

        // Default to the file-backed store for any non-Windows OS — works on Linux today and is
        // a reasonable fallback on macOS until a proper Keychain backing is added.
        return new LinuxCredentialStore();
    }

    public static Credential ReadCredential(string applicationName) => Backend.Read(applicationName);

    public static void WriteCredential(string applicationName, string userName, string secret) => Backend.Write(applicationName, userName, secret);

    public static bool CredentialExists(string applicationName) => Backend.Read(applicationName) != null;

    public static void DeleteCredential(string applicationName)
    {
        if (applicationName == null)
        {
            throw new ArgumentNullException(nameof(applicationName));
        }

        Backend.Delete(applicationName);
    }
}

internal interface ICredentialStore
{
    Credential Read(string applicationName);
    void Write(string applicationName, string userName, string secret);
    void Delete(string applicationName);
}

public enum CredentialType
{
    Generic = 1,
    DomainPassword,
    DomainCertificate,
    DomainVisiblePassword,
    GenericCertificate,
    DomainExtended,
    Maximum,
    MaximumEx = Maximum + 1000,
}

public class Credential
{
    public CredentialType CredentialType { get; }

    public string ApplicationName { get; }

    public string UserName { get; }

    public string Password { get; }

    public Credential(CredentialType credentialType, string applicationName, string userName, string password)
    {
        ApplicationName = applicationName;
        UserName = userName;
        Password = password;
        CredentialType = credentialType;
    }

    public override string ToString()
    {
        return string.Format("CredentialType: {0}, ApplicationName: {1}, UserName: {2}, Password: {3}", CredentialType, ApplicationName, UserName, Password);
    }
}
