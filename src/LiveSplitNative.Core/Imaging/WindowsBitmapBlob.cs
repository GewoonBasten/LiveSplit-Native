using System.Text;

namespace LiveSplitNative.Core.Imaging;

/// <summary>
/// SettingsHelper.CreateSetting(Image) / GetImageFromElement: a layout image is a
/// BinaryFormatter System.Drawing.Bitmap, whose one member "Data" holds the image file
/// (PNG, JPEG, GIF or BMP). This writes the exact records Windows writes, so Windows reads our
/// layouts, and reads Windows' (and older raw image) payloads back to the file bytes.
/// </summary>
public static class WindowsBitmapBlob
{
    private const string DrawingAssembly = "System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";

    /// <summary>The BinaryFormatter stream of a Bitmap holding <paramref name="imageBytes"/>.</summary>
    public static byte[] Encode(byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes.Length + 200);
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        // SerializationHeaderRecord: root object 1, header -1, version 1.0.
        writer.Write((byte)0x00);
        writer.Write(1);
        writer.Write(-1);
        writer.Write(1);
        writer.Write(0);
        // BinaryLibrary 2: System.Drawing.
        writer.Write((byte)0x0C);
        writer.Write(2);
        writer.Write(DrawingAssembly);
        // ClassWithMembersAndTypes 1: System.Drawing.Bitmap { byte[] Data } in library 2.
        writer.Write((byte)0x05);
        writer.Write(1);
        writer.Write("System.Drawing.Bitmap");
        writer.Write(1);
        writer.Write("Data");
        writer.Write((byte)0x07); // BinaryType.PrimitiveArray
        writer.Write((byte)0x02); // PrimitiveType.Byte
        writer.Write(2);
        // Data = MemberReference to object 3, then ArraySinglePrimitive 3 (bytes), MessageEnd.
        writer.Write((byte)0x09);
        writer.Write(3);
        writer.Write((byte)0x0F);
        writer.Write(3);
        writer.Write(imageBytes.Length);
        writer.Write((byte)0x02);
        writer.Write(imageBytes);
        writer.Write((byte)0x0B);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>The image file inside a Bitmap blob (or a raw image payload); null if none.</summary>
    public static byte[]? Decode(byte[] blob)
    {
        if (IconBlobDecoder.LooksLikeImage(blob))
        {
            return blob;
        }

        // The byte array record: 0x0F, object id, length, PrimitiveType.Byte, bytes, then 0x0B.
        for (int i = 0; i + 10 <= blob.Length; i++)
        {
            if (blob[i] != 0x0F || blob[i + 9] != 0x02)
            {
                continue;
            }

            int length = BitConverter.ToInt32(blob, i + 5);
            int start = i + 10;
            if (length > 0 && start + length < blob.Length && blob[start + length] == 0x0B)
            {
                return blob[start..(start + length)];
            }
        }

        return null;
    }

    /// <summary>The element content Windows writes: the blob as base64 in CDATA.</summary>
    public static string ToElementInnerXml(byte[] imageBytes) =>
        $"<![CDATA[{Convert.ToBase64String(Encode(imageBytes))}]]>";

    /// <summary>An element's text (base64 of a Bitmap blob) as the image file bytes; null if none.</summary>
    public static byte[]? FromElementText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        byte[] blob;
        try
        {
            blob = Convert.FromBase64String(string.Concat(text.Where(static c => !char.IsWhiteSpace(c))));
        }
        catch (FormatException)
        {
            return null;
        }

        return Decode(blob) ?? IconBlobDecoder.TryDecodeImageBytes(text);
    }
}
