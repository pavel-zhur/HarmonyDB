using OneShelf.Frontend.Api.Model.V3.Illustrations;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class IllustrationsResponse
{
    public required AllIllustrations? Illustrations { get; init; }

    public required Dictionary<int, string>? Titles { get; init; }
}