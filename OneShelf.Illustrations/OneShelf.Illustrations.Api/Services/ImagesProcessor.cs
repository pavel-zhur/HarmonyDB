using System.Drawing.Imaging;
using System.Drawing;

namespace OneShelf.Illustrations.Api.Services;

public class ImagesProcessor
{
    public byte[] ConvertToJpeg(byte[] png)
    {
        using var originalStream = new MemoryStream(png);
        using Bitmap bmp1 = new Bitmap(originalStream);
        ImageCodecInfo jgpEncoder = GetEncoder(ImageFormat.Jpeg) ?? throw new("The Jpeg encoder is not found.");

        // Create an Encoder object based on the GUID 
        // for the Quality parameter category.
        var myEncoder = Encoder.Quality;

        // Create an EncoderParameters object. 
        // An EncoderParameters object has an array of EncoderParameter 
        // objects. In this case, there is only one 
        // EncoderParameter object in the array.
        using EncoderParameters myEncoderParameters = new EncoderParameters(1);

        var myEncoderParameter = new EncoderParameter(myEncoder, 80L);
        myEncoderParameters.Param[0] = myEncoderParameter;

        using var outputStream = new MemoryStream();
        bmp1.Save(outputStream, jgpEncoder, myEncoderParameters);
        return outputStream.ToArray();
    }

    public List<byte[]> Resize(byte[] png, IEnumerable<int> sizes)
    {
        using var originalStream = new MemoryStream(png);
        using Bitmap bmp1 = new Bitmap(originalStream);

        return sizes
            .Select(s =>
            {
                using var bmp2 = new Bitmap(bmp1, new(s, s));

                using var outputStream = new MemoryStream();
                bmp2.Save(outputStream, ImageFormat.Jpeg);
                return outputStream.ToArray();
            })
            .ToList();
    }

    private ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
        foreach (ImageCodecInfo codec in codecs)
        {
            if (codec.FormatID == format.Guid)
            {
                return codec;
            }
        }
        return null;
    }
}