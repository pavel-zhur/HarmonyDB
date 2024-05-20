using System.Reflection;
using PdfSharp.Fonts;

namespace OneShelf.Pdfs.Generation.Volumes.Services;

internal class MyFontResolver : IFontResolver
{
    public static string Consolas = "fontcheck123";
    
    private readonly byte[] _consolas;

    private MyFontResolver()
    {
        _consolas = ReadFont().Result;
    }

    private const string FontResourceName = "OneShelf.Pdfs.Generation.Volumes.Resources.CONSOLA.TTF";

    private static async Task<byte[]> ReadFont()
    {
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(FontResourceName);
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);
        return copy.ToArray();
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (familyName == Consolas) return new(familyName);
        
        return PlatformFontResolver.ResolveTypeface(familyName, isBold, isItalic);
    }

    public byte[] GetFont(string faceName)
    {
        if (faceName != Consolas) throw new ArgumentOutOfRangeException(nameof(faceName), faceName);

        return _consolas;
    }

    public static IFontResolver Instance { get; } = new MyFontResolver();
}