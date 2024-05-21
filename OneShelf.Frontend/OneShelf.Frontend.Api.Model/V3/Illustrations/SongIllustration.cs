namespace OneShelf.Frontend.Api.Model.V3.Illustrations;

public class SongIllustration
{
    public required int Version { get; set; }

    public required int I { get; set; }

    public required int J { get; set; }

    public required int K { get; set; }

    public required ImagePublicUrls PublicUrls { get; init; }

    public string? AlterationTitle { get; set; }
}