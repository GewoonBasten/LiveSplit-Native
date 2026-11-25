using System.Buffers.Binary;
using System.Text;

namespace LiveSplitNative.Core.Serialization;

/// <summary>
/// A layout font as Windows LiveSplit stores it: SettingsHelper.CreateSetting(Font) puts a
/// BinaryFormatter (NRBF) serialization of System.Drawing.Font, base64, in a CDATA section, and
/// GetFontFromElement reads it back. Font.GetObjectData writes Name, Size, Style and Unit. This
/// reads and writes exactly that stream, so a layout saved here opens on Windows and the other
/// way round (the writer produces byte for byte what .NET Framework writes for a Font).
/// </summary>
public static class WindowsFontBlob
{
    /// <summary>System.Drawing.FontStyle bits.</summary>
    public const int Bold = 1;
    public const int Italic = 2;

    /// <summary>System.Drawing.GraphicsUnit: Pixel (what LiveSplit's font dialog makes) and Point.</summary>
    public const int UnitPixel = 2;
    public const int UnitPoint = 3;

    private const string DrawingAssembly =
        "System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";

    public readonly record struct Font(string Family, float Size, int Style, int Unit)
    {
        public bool IsBold => (Style & WindowsFontBlob.Bold) != 0;

        public bool IsItalic => (Style & WindowsFontBlob.Italic) != 0;

        /// <summary>The size in pixels: points at Windows' 96 DPI (LiveSplit draws at 96).</summary>
        public float SizePx => Unit == UnitPoint ? Size * 96f / 72f : Size;
    }

    /// <summary>Reads a font element's text: base64, optionally inside a CDATA section.</summary>
    public static bool TryRead(string? elementText, out Font font)
    {
        font = default;
        if (string.IsNullOrWhiteSpace(elementText))
        {
            return false;
        }

        string raw = elementText.Trim();
        if (raw.StartsWith("<![CDATA[", StringComparison.Ordinal) && raw.EndsWith("]]>", StringComparison.Ordinal))
        {
            raw = raw["<![CDATA[".Length..^"]]>".Length].Trim();
        }

        try
        {
            return TryReadBytes(Convert.FromBase64String(raw), out font);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// The NRBF stream: header, the System.Drawing library, the Font class with its four members,
    /// then the values: the family string, the size (Single), and FontStyle and GraphicsUnit,
    /// each a class record with one Int32 "value__".
    /// </summary>
    public static bool TryReadBytes(ReadOnlySpan<byte> data, out Font font)
    {
        font = default;
        try
        {
            int p = 0;
            if (data[p++] != 0x00)
            {
                return false;
            }

            p += 16; // root id, header id, major, minor
            while (p < data.Length && data[p] == 0x0C)
            {
                p += 5; // record type, library id
                ReadString(data, ref p);
            }

            if (data[p++] != 0x05)
            {
                return false;
            }

            p += 4; // object id
            if (ReadString(data, ref p) != "System.Drawing.Font")
            {
                return false;
            }

            int count = BinaryPrimitives.ReadInt32LittleEndian(data[p..]);
            p += 4;
            var names = new string[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = ReadString(data, ref p);
            }

            byte[] types = data.Slice(p, count).ToArray();
            p += count;
            for (int i = 0; i < count; i++)
            {
                switch (types[i])
                {
                    case 0: // Primitive: its type code
                        p += 1;
                        break;
                    case 3: // SystemClass: its name
                        ReadString(data, ref p);
                        break;
                    case 4: // Class: its name and library id
                        ReadString(data, ref p);
                        p += 4;
                        break;
                }
            }

            p += 4; // library id
            string family = "";
            float size = 0;
            int style = 0;
            int unit = UnitPoint;
            for (int i = 0; i < count; i++)
            {
                switch (names[i])
                {
                    case "Name":
                        if (data[p++] != 0x06)
                        {
                            return false;
                        }

                        p += 4;
                        family = ReadString(data, ref p);
                        break;
                    case "Size":
                        size = BinaryPrimitives.ReadSingleLittleEndian(data[p..]);
                        p += 4;
                        break;
                    case "Style":
                        style = ReadEnum(data, ref p);
                        break;
                    case "Unit":
                        unit = ReadEnum(data, ref p);
                        break;
                    default:
                        return false;
                }
            }

            if (family.Length == 0 || !(size > 0) || float.IsInfinity(size))
            {
                return false;
            }

            font = new Font(family, size, style, unit);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>The element text Windows writes: a CDATA section holding the base64 stream.</summary>
    public static string ToElementInnerXml(string family, float sizePx, bool bold, bool italic) =>
        "<![CDATA[" + Convert.ToBase64String(Write(family, sizePx, (bold ? Bold : 0) | (italic ? Italic : 0), UnitPixel)) + "]]>";

    /// <summary>What .NET Framework's BinaryFormatter writes for new Font(family, size, style, unit).</summary>
    public static byte[] Write(string family, float size, int style, int unit)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8);
        // SerializationHeaderRecord: root object 1, header id -1, version 1.0.
        w.Write((byte)0x00);
        w.Write(1);
        w.Write(-1);
        w.Write(1);
        w.Write(0);
        // BinaryLibrary 2: System.Drawing.
        w.Write((byte)0x0C);
        w.Write(2);
        w.Write(DrawingAssembly);
        // ClassWithMembersAndTypes 1: System.Drawing.Font { Name, Size, Style, Unit }.
        w.Write((byte)0x05);
        w.Write(1);
        w.Write("System.Drawing.Font");
        w.Write(4);
        w.Write("Name");
        w.Write("Size");
        w.Write("Style");
        w.Write("Unit");
        w.Write([(byte)1, (byte)0, (byte)4, (byte)4]); // String, Primitive, Class, Class
        w.Write((byte)11); // Single
        w.Write("System.Drawing.FontStyle");
        w.Write(2);
        w.Write("System.Drawing.GraphicsUnit");
        w.Write(2);
        w.Write(2);
        // Values: BinaryObjectString 3, the Single, then the two enums as their own classes.
        w.Write((byte)0x06);
        w.Write(3);
        w.Write(family);
        w.Write(size);
        WriteEnum(w, -4, "System.Drawing.FontStyle", style);
        WriteEnum(w, -5, "System.Drawing.GraphicsUnit", unit);
        w.Write((byte)0x0B); // MessageEnd
        w.Flush();
        return ms.ToArray();
    }

    private static void WriteEnum(BinaryWriter w, int objectId, string typeName, int value)
    {
        w.Write((byte)0x05);
        w.Write(objectId);
        w.Write(typeName);
        w.Write(1);
        w.Write("value__");
        w.Write((byte)0); // Primitive
        w.Write((byte)8); // Int32
        w.Write(2);
        w.Write(value);
    }

    /// <summary>An enum value: a class record with one Int32 member, or a reference to its class.</summary>
    private static int ReadEnum(ReadOnlySpan<byte> data, ref int p)
    {
        byte record = data[p++];
        if (record == 0x05)
        {
            p += 4; // object id
            ReadString(data, ref p);
            int members = BinaryPrimitives.ReadInt32LittleEndian(data[p..]);
            p += 4;
            for (int i = 0; i < members; i++)
            {
                ReadString(data, ref p);
            }

            p += members * 2; // binary type Primitive + Int32 per member
            p += 4; // library id
        }
        else if (record == 0x01)
        {
            p += 8; // object id, metadata id (ClassWithId)
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(data));
        }

        int value = BinaryPrimitives.ReadInt32LittleEndian(data[p..]);
        p += 4;
        return value;
    }

    /// <summary>A length-prefixed (7-bit encoded) UTF-8 string, as BinaryWriter writes it.</summary>
    private static string ReadString(ReadOnlySpan<byte> data, ref int p)
    {
        int length = 0;
        int shift = 0;
        byte b;
        do
        {
            b = data[p++];
            length |= (b & 0x7F) << shift;
            shift += 7;
        }
        while ((b & 0x80) != 0 && shift < 35);

        string value = Encoding.UTF8.GetString(data.Slice(p, length));
        p += length;
        return value;
    }
}
