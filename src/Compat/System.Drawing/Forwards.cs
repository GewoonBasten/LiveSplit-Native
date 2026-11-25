// The System.Drawing types .NET keeps in System.Drawing.Primitives: a component that asks
// System.Drawing for them gets the framework's own (and so the app's).
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(System.Drawing.Color))]
[assembly: TypeForwardedTo(typeof(System.Drawing.ColorTranslator))]
[assembly: TypeForwardedTo(typeof(System.Drawing.KnownColor))]
[assembly: TypeForwardedTo(typeof(System.Drawing.Point))]
[assembly: TypeForwardedTo(typeof(System.Drawing.PointF))]
[assembly: TypeForwardedTo(typeof(System.Drawing.Rectangle))]
[assembly: TypeForwardedTo(typeof(System.Drawing.RectangleF))]
[assembly: TypeForwardedTo(typeof(System.Drawing.Size))]
[assembly: TypeForwardedTo(typeof(System.Drawing.SizeF))]
[assembly: TypeForwardedTo(typeof(System.Drawing.SystemColors))]
