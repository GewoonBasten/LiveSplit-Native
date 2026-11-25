using LiveSplitNative.Core.Imaging;

namespace LiveSplitNative.Core.Tests;

public class IconBlobDecoderTests
{
    // 8x8 solid RGB PNG
    private const string PortablePngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEUlEQVR42mMwnvkfK2IYWhIAo1xywQnsJsYAAAAASUVORK5CYII=";

    [Fact]
    public void Decodes_Portable_Png_Base64()
    {
        byte[]? bytes = IconBlobDecoder.TryDecodeImageBytes(PortablePngBase64);
        Assert.NotNull(bytes);
        Assert.True(IconBlobDecoder.LooksLikeImage(bytes));
        Assert.Equal(0x89, bytes![0]);
        Assert.Equal((byte)'P', bytes[1]);
    }

    [Fact]
    public void Decodes_Cdata_Wrapped_Portable_Png()
    {
        string xml = IconBlobDecoder.ToPortableIconXml(Convert.FromBase64String(PortablePngBase64));
        byte[]? bytes = IconBlobDecoder.TryDecodeImageBytes(xml);
        Assert.NotNull(bytes);
        Assert.True(IconBlobDecoder.LooksLikeImage(bytes));
    }

    [Fact]
    public void Extracts_Png_Magic_From_BinaryFormatter_Like_Blob()
    {
        byte[] png = Convert.FromBase64String(PortablePngBase64);
        byte[] fakeBf = [0x00, 0x01, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, .. "PADDING"u8, .. png];
        string b64 = Convert.ToBase64String(fakeBf);
        byte[]? bytes = IconBlobDecoder.TryDecodeImageBytes(b64);
        Assert.NotNull(bytes);
        Assert.Equal(0x89, bytes![0]);
        Assert.Equal((byte)'P', bytes[1]);
        // Trimmed to IEND — should equal original PNG length
        Assert.Equal(png.Length, bytes.Length);
    }

    [Fact]
    public void Extracts_Complete_Png_From_Celeste_Upstream_Bf_Fixture()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "icon-bf-sample.b64");
        Assert.True(File.Exists(path), "missing fixture " + path);
        string b64 = File.ReadAllText(path);
        byte[]? bytes = IconBlobDecoder.TryDecodeImageBytes("<![CDATA[" + b64 + "]]>");
        Assert.NotNull(bytes);
        Assert.Equal(0x89, bytes![0]);
        Assert.True(bytes.Length > 1000);
        // Ends with IEND+CRC
        Assert.Equal((byte)'I', bytes[^8]);
        Assert.Equal((byte)'E', bytes[^7]);
        Assert.Equal((byte)'N', bytes[^6]);
        Assert.Equal((byte)'D', bytes[^5]);
    }

    [Fact]
    public void Undecodable_Garbage_Returns_Null()
    {
        Assert.Null(IconBlobDecoder.TryDecodeImageBytes(Convert.ToBase64String([1, 2, 3, 4, 5])));
        Assert.Null(IconBlobDecoder.TryDecodeImageBytes(""));
        Assert.Null(IconBlobDecoder.TryDecodeImageBytes(null));
    }
}
