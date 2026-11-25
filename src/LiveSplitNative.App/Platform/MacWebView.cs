using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// A WKWebView in an Avalonia window (Windows' race window uses a WebView2). <see cref="Load"/>
/// opens a page with an Authorization header, as ChannelForm adds the racetime.gg token to every
/// racetime.gg request: the page request carries it, and the page's own fetch / XMLHttpRequest
/// calls to that site get it from a script added before the page's scripts run.
/// </summary>
internal sealed class MacWebView : NativeControlHost
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const string WebKit = "/System/Library/Frameworks/WebKit.framework/WebKit";
    private static bool _webKitLoaded;
    private nint _webView;
    private (string Url, string? Origin, string? Token)? _pending;

    /// <summary>The page shown (WKWebView.URL), or null.</summary>
    public string? CurrentUrl
    {
        get
        {
            if (_webView == 0)
            {
                return null;
            }

            nint url = Send(_webView, Sel("URL"));
            return url == 0 ? null : Marshal.PtrToStringUTF8(Send(Send(url, Sel("absoluteString")), Sel("UTF8String")));
        }
    }

    public bool IsLoading => _webView != 0 && SendBool(_webView, Sel("isLoading"));

    /// <summary>The page's title (WKWebView.title), or null.</summary>
    public string? PageTitle
    {
        get
        {
            if (_webView == 0)
            {
                return null;
            }

            nint title = Send(_webView, Sel("title"));
            return title == 0 ? null : Marshal.PtrToStringUTF8(Send(title, Sel("UTF8String")));
        }
    }

    /// <summary>Whether the WKWebView exists (made when the control is shown in a window).</summary>
    public bool IsCreated => _webView != 0;

    /// <summary>Whether a web view could be made here (macOS).</summary>
    public static bool IsSupported => OperatingSystem.IsMacOS();

    /// <summary>Opens <paramref name="url"/>; with a token, requests to <paramref name="origin"/> carry "Authorization: Bearer token".</summary>
    public void Load(string url, string? origin, string? token)
    {
        if (_webView == 0)
        {
            _pending = (url, origin, token);
            return;
        }

        nint pool = objc_autoreleasePoolPush();
        try
        {
            nint controller = Send(Send(_webView, Sel("configuration")), Sel("userContentController"));
            SendVoid(controller, Sel("removeAllUserScripts"));
            if (token is not null && origin is not null)
            {
                nint script = Send(objc_getClass("WKUserScript"), Sel("alloc"));
                // WKUserScriptInjectionTimeAtDocumentStart, all frames.
                script = SendScript(script, Sel("initWithSource:injectionTime:forMainFrameOnly:"), NsString(AuthorizationScript(origin, token)), 0, false);
                SendVoid(controller, Sel("addUserScript:"), script);
                SendVoid(script, Sel("release"));
            }

            nint nsUrl = Send(objc_getClass("NSURL"), Sel("URLWithString:"), NsString(url));
            if (nsUrl == 0)
            {
                return;
            }

            nint request = Send(objc_getClass("NSMutableURLRequest"), Sel("requestWithURL:"), nsUrl);
            if (token is not null && origin is not null && url.StartsWith(origin, StringComparison.OrdinalIgnoreCase))
            {
                SendVoid(request, Sel("setValue:forHTTPHeaderField:"), NsString("Bearer " + token), NsString("Authorization"));
            }

            Send(_webView, Sel("loadRequest:"), request);
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    /// <summary>Adds the header to the page's fetch and XMLHttpRequest calls to <paramref name="origin"/>.</summary>
    internal static string AuthorizationScript(string origin, string token)
    {
        string o = System.Text.Json.JsonSerializer.Serialize(origin.TrimEnd('/'));
        string t = System.Text.Json.JsonSerializer.Serialize("Bearer " + token);
        return "(function(){var o=" + o + ",a=" + t + ";"
            + "function s(u){try{return new URL(u,location.href).origin===o}catch(e){return false}}"
            + "var f=window.fetch;if(f){window.fetch=function(i,n){var u=typeof i==='string'?i:(i&&i.url);"
            + "if(s(u)){n=Object.assign({},n||{});var h=new Headers(n.headers||(typeof i!=='string'&&i?i.headers:undefined));"
            + "if(!h.has('Authorization'))h.set('Authorization',a);n.headers=h}return f.call(this,i,n)}}"
            + "var p=XMLHttpRequest.prototype,op=p.open,sd=p.send;"
            + "p.open=function(m,u){this.__lsAuth=s(u);return op.apply(this,arguments)};"
            + "p.send=function(){if(this.__lsAuth){try{this.setRequestHeader('Authorization',a)}catch(e){}}return sd.apply(this,arguments)}})();";
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (!IsSupported)
        {
            return base.CreateNativeControlCore(parent);
        }

        if (!_webKitLoaded)
        {
            NativeLibrary.Load(WebKit);
            _webKitLoaded = true;
        }

        nint configuration = Send(Send(objc_getClass("WKWebViewConfiguration"), Sel("alloc")), Sel("init"));
        _webView = SendFrame(Send(objc_getClass("WKWebView"), Sel("alloc")), Sel("initWithFrame:configuration:"), new CGRect(0, 0, 100, 100), configuration);
        SendVoid(configuration, Sel("release"));
        Console.WriteLine($"MacWebView: WKWebView {(_webView == 0 ? "not created" : "created")}");
        if (_pending is { } pending)
        {
            _pending = null;
            Load(pending.Url, pending.Origin, pending.Token);
        }

        return new PlatformHandle(_webView, "NSView");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (_webView != 0 && control.Handle == _webView)
        {
            SendVoid(_webView, Sel("stopLoading"));
            SendVoid(_webView, Sel("release"));
            _webView = 0;
            return;
        }

        base.DestroyNativeControlCore(control);
    }

    private static nint Sel(string name) => sel_registerName(name);

    private static nint NsString(string value)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value + "\0");
        nint ptr = Marshal.AllocHGlobal(utf8.Length);
        try
        {
            Marshal.Copy(utf8, 0, ptr, utf8.Length);
            return Send(objc_getClass("NSString"), Sel("stringWithUTF8String:"), ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGRect(double x, double y, double width, double height)
    {
        public readonly double X = x;
        public readonly double Y = y;
        public readonly double Width = width;
        public readonly double Height = height;
    }

    [DllImport(ObjC)]
    private static extern nint objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern nint sel_registerName(string name);

    [DllImport(ObjC)]
    private static extern nint objc_autoreleasePoolPush();

    [DllImport(ObjC)]
    private static extern void objc_autoreleasePoolPop(nint pool);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector, nint arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(nint receiver, nint selector, nint arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(nint receiver, nint selector, nint arg1, nint arg2);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBool(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint SendFrame(nint receiver, nint selector, CGRect frame, nint arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint SendScript(nint receiver, nint selector, nint source, nint injectionTime, [MarshalAs(UnmanagedType.I1)] bool mainFrameOnly);
}
