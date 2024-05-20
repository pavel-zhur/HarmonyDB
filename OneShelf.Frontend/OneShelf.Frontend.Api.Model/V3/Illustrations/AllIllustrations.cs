namespace OneShelf.Frontend.Api.Model.V3.Illustrations;

public class AllIllustrations
{
    public required IReadOnlyDictionary<int, SongIllustrations> Songs { get; init; }

    public IReadOnlyList<int>? UnderGeneration { get; init; }

    public required int Etag { get; set; }
}