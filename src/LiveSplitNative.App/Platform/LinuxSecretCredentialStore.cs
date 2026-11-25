using System.Runtime.InteropServices;
using LiveSplitNative.Core.Racing;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// Saved logins on Linux (Windows' Credential Manager, the Mac's Keychain): the desktop's secret
/// store through libsecret (GNOME Keyring, KDE Wallet), a password per name, account "LiveSplit".
/// Where there is none (no libsecret, no running secret service) the logins are kept for the
/// session only, as before. A call that does not answer (a locked keyring waiting for its
/// password) is given up after a while, so the app keeps working.
/// </summary>
internal sealed class LinuxSecretCredentialStore : ICredentialStore
{
    private const string Secret = "libsecret-1.so.0";
    private const string GLib = "libglib-2.0.so.0";
    private const string Account = "LiveSplit";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

    private readonly MemoryCredentialStore _session = new();
    private readonly object _lock = new();
    private nint _schema;
    private bool _unavailable;

    public string? Read(string name)
    {
        string? saved = Call(() => Lookup(name), out bool done);
        return done ? saved : _session.Read(name);
    }

    public void Write(string name, string? value)
    {
        _session.Write(name, value);
        Call(() => value is null ? Clear(name) : Store(name, value), out _);
    }

    /// <summary>Runs a libsecret call off this thread, at most <see cref="Wait"/>; done: it answered.</summary>
    private T? Call<T>(Func<T> call, out bool done)
    {
        done = false;
        if (_unavailable || !OperatingSystem.IsLinux())
        {
            return default;
        }

        try
        {
            Task<T> task = Task.Run(() =>
            {
                lock (_lock)
                {
                    return call();
                }
            });
            if (!task.Wait(Wait))
            {
                Console.WriteLine("Saved logins: the secret store did not answer; kept for this session only.");
                return default;
            }

            done = true;
            return task.Result;
        }
        catch (AggregateException ex)
        {
            // No libsecret, or no secret service running: the session's store from now on.
            _unavailable = true;
            string reason = ex.InnerException is DllNotFoundException ? "libsecret is not installed" : ex.InnerException?.Message ?? "unknown";
            Console.WriteLine("Saved logins: no secret store (" + reason + "); kept for this session only.");
            return default;
        }
    }

    private string? Lookup(string name)
    {
        nint attributes = Attributes(name, out nint[] strings);
        try
        {
            nint error = 0;
            nint password = secret_password_lookupv_sync(Schema(), attributes, 0, ref error);
            ThrowIf(error);
            if (password == 0)
            {
                return null;
            }

            string? value = Marshal.PtrToStringUTF8(password);
            secret_password_free(password);
            return value;
        }
        finally
        {
            Free(attributes, strings);
        }
    }

    private bool Store(string name, string value)
    {
        nint attributes = Attributes(name, out nint[] strings);
        nint collection = Marshal.StringToCoTaskMemUTF8("default");
        nint label = Marshal.StringToCoTaskMemUTF8("LiveSplit: " + name);
        nint password = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            nint error = 0;
            bool stored = secret_password_storev_sync(Schema(), attributes, collection, label, password, 0, ref error) != 0;
            ThrowIf(error);
            return stored;
        }
        finally
        {
            Marshal.FreeCoTaskMem(collection);
            Marshal.FreeCoTaskMem(label);
            Marshal.FreeCoTaskMem(password);
            Free(attributes, strings);
        }
    }

    private bool Clear(string name)
    {
        nint attributes = Attributes(name, out nint[] strings);
        try
        {
            nint error = 0;
            bool cleared = secret_password_clearv_sync(Schema(), attributes, 0, ref error) != 0;
            ThrowIf(error);
            return cleared;
        }
        finally
        {
            Free(attributes, strings);
        }
    }

    /// <summary>
    /// SecretSchema: name, flags, 32 attributes (name, type), a reserved int and 7 pointers; its
    /// attributes "service" and "account", both strings.
    /// </summary>
    private nint Schema()
    {
        if (_schema != 0)
        {
            return _schema;
        }

        const int size = 8 + 8 + (32 * 16) + 8 + (7 * 8);
        nint schema = Marshal.AllocHGlobal(size);
        for (int i = 0; i < size; i++)
        {
            Marshal.WriteByte(schema, i, 0);
        }

        Marshal.WriteIntPtr(schema, 0, Marshal.StringToCoTaskMemUTF8("org.livesplit.LiveSplitNative.Login"));
        Marshal.WriteIntPtr(schema, 16, Marshal.StringToCoTaskMemUTF8("service"));
        Marshal.WriteIntPtr(schema, 32, Marshal.StringToCoTaskMemUTF8("account"));
        _schema = schema;
        return schema;
    }

    private static nint Attributes(string name, out nint[] strings)
    {
        nint glib = NativeLibrary.Load(GLib);
        nint table = g_hash_table_new(NativeLibrary.GetExport(glib, "g_str_hash"), NativeLibrary.GetExport(glib, "g_str_equal"));
        strings =
        [
            Marshal.StringToCoTaskMemUTF8("service"),
            Marshal.StringToCoTaskMemUTF8(name),
            Marshal.StringToCoTaskMemUTF8("account"),
            Marshal.StringToCoTaskMemUTF8(Account),
        ];
        g_hash_table_insert(table, strings[0], strings[1]);
        g_hash_table_insert(table, strings[2], strings[3]);
        return table;
    }

    private static void Free(nint table, nint[] strings)
    {
        g_hash_table_unref(table);
        foreach (nint s in strings)
        {
            Marshal.FreeCoTaskMem(s);
        }
    }

    /// <summary>GError: its message (after the domain and code), freed; no secret service is an error too.</summary>
    private static void ThrowIf(nint error)
    {
        if (error == 0)
        {
            return;
        }

        string message = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(error, 8)) ?? "error";
        g_error_free(error);
        throw new SecretStoreException(message);
    }

    private sealed class SecretStoreException(string message) : Exception(message);

    [DllImport(Secret)]
    private static extern nint secret_password_lookupv_sync(nint schema, nint attributes, nint cancellable, ref nint error);

    [DllImport(Secret)]
    private static extern int secret_password_storev_sync(nint schema, nint attributes, nint collection, nint label, nint password, nint cancellable, ref nint error);

    [DllImport(Secret)]
    private static extern int secret_password_clearv_sync(nint schema, nint attributes, nint cancellable, ref nint error);

    [DllImport(Secret)]
    private static extern void secret_password_free(nint password);

    [DllImport(GLib)]
    private static extern nint g_hash_table_new(nint hash, nint equal);

    [DllImport(GLib)]
    private static extern int g_hash_table_insert(nint table, nint key, nint value);

    [DllImport(GLib)]
    private static extern void g_hash_table_unref(nint table);

    [DllImport(GLib)]
    private static extern void g_error_free(nint error);
}
