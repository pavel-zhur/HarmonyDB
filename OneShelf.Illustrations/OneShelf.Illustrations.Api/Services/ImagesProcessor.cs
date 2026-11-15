using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace OneShelf.Illustrations.Api.Services;

public class ImagesProcessor
{
    public byte[] ConvertToJpeg(byte[] png)
    {
        using var image = Image.Load(png);

        using var outputStream = new MemoryStream();
        image.Save(outputStream, new JpegEncoder { Quality = 80 });
        return outputStream.ToArray();
    }

    public List<byte[]> Resize(byte[] png, IEnumerable<int> sizes)
    {
        using var image = Image.Load(png);

        return sizes
            .Select(s =>
            {
                using var resized = image.Clone(x => x.Resize(s, s));

                using var outputStream = new MemoryStream();
                resized.Save(outputStream, new JpegEncoder { Quality = 75 });
                return outputStream.ToArray();
            })
            .ToList();
    }
}
