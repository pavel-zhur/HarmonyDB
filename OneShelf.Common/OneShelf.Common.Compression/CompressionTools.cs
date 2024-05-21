using System.IO.Compression;
using System.Text;

namespace OneShelf.Common.Compression;

public static class CompressionTools
{
    public static async Task<byte[]> Compress(string data) => await Compress(Encoding.UTF8.GetBytes(data));

    public static async Task<byte[]> Compress(byte[] data)
    {
        using var outputStream = new MemoryStream();
        await using var gzipStream = new GZipStream(outputStream, CompressionLevel.SmallestSize);
        await gzipStream.WriteAsync(new(data));
        await gzipStream.FlushAsync();
        return outputStream.ToArray();
    }

    public static async Task<byte[]> DecompressToBytes(byte[] data)
        => await DecompressToBytes(new BinaryData(data));

    public static async Task<string> DecompressToString(byte[] data)
        => Encoding.UTF8.GetString(await DecompressToBytes(data));

    public static async Task<BinaryData> CompressToBinaryData(string data)
        => new(await Compress(data));

    public static async Task<BinaryData> CompressToBinaryData(byte[] data)
        => new(await Compress(data));

    public static async Task<byte[]> DecompressToBytes(BinaryData data)
    {
        await using var memoryStream = data.ToStream();
        await using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        await gzipStream.CopyToAsync(outputStream);
        await gzipStream.FlushAsync();

        return outputStream.ToArray();
    }

    public static async Task<string> DecompressToString(BinaryData data)
        => Encoding.UTF8.GetString(await DecompressToBytes(data));
}