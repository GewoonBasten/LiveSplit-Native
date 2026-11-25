using System.Runtime.InteropServices;
using System.Text;
using LiveSplitNative.Core.Racing;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// racetime.gg's tokens in the login Keychain (Windows keeps them in the Credential Manager as
/// WebCredentials): a generic password per token name, account "LiveSplit".
/// </summary>
internal sealed class MacKeychainCredentialStore : ICredentialStore
{
    private const string Security = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string Account = "LiveSplit";
    private const int ErrSecSuccess = 0;

    public string? Read(string name)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        byte[] service = Encoding.UTF8.GetBytes(name);
        byte[] account = Encoding.UTF8.GetBytes(Account);
        int status = SecKeychainFindGenericPassword(
            0, (uint)service.Length, service, (uint)account.Length, account, out uint length, out nint data, out nint item);
        if (status != ErrSecSuccess)
        {
            return null;
        }

        try
        {
            byte[] bytes = new byte[length];
            Marshal.Copy(data, bytes, 0, (int)length);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            SecKeychainItemFreeContent(0, data);
            if (item != 0)
            {
                CFRelease(item);
            }
        }
    }

    public void Write(string name, string? value)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        byte[] service = Encoding.UTF8.GetBytes(name);
        byte[] account = Encoding.UTF8.GetBytes(Account);
        int found = SecKeychainFindGenericPassword(
            0, (uint)service.Length, service, (uint)account.Length, account, out _, out nint oldData, out nint item);
        if (found == ErrSecSuccess && oldData != 0)
        {
            SecKeychainItemFreeContent(0, oldData);
        }

        try
        {
            if (value is null)
            {
                if (found == ErrSecSuccess && item != 0)
                {
                    _ = SecKeychainItemDelete(item);
                }

                return;
            }

            byte[] password = Encoding.UTF8.GetBytes(value);
            if (found == ErrSecSuccess && item != 0)
            {
                _ = SecKeychainItemModifyAttributesAndData(item, 0, (uint)password.Length, password);
            }
            else
            {
                _ = SecKeychainAddGenericPassword(
                    0, (uint)service.Length, service, (uint)account.Length, account, (uint)password.Length, password, out nint added);
                if (added != 0)
                {
                    CFRelease(added);
                }
            }
        }
        finally
        {
            if (item != 0)
            {
                CFRelease(item);
            }
        }
    }

    [DllImport(Security)]
    private static extern int SecKeychainFindGenericPassword(
        nint keychainOrArray,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        out uint passwordLength,
        out nint passwordData,
        out nint itemRef);

    [DllImport(Security)]
    private static extern int SecKeychainAddGenericPassword(
        nint keychain,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        uint passwordLength,
        byte[] passwordData,
        out nint itemRef);

    [DllImport(Security)]
    private static extern int SecKeychainItemModifyAttributesAndData(nint itemRef, nint attrList, uint length, byte[] data);

    [DllImport(Security)]
    private static extern int SecKeychainItemDelete(nint itemRef);

    [DllImport(Security)]
    private static extern int SecKeychainItemFreeContent(nint attrList, nint data);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(nint value);
}
