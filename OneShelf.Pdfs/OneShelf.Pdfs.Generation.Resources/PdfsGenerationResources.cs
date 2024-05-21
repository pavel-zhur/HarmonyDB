namespace OneShelf.Pdfs.Generation.Resources;

public static class PdfsGenerationResources
{
    private const string QrResourceName = "OneShelf.Pdfs.Generation.Resources.App_Data.qr.png";

    public static async Task<byte[]> ReadQr()
    {
        await using var stream = typeof(PdfsGenerationResources).Assembly.GetManifestResourceStream(QrResourceName);
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);
        return copy.ToArray();
    }
}