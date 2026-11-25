namespace OneShelf.Sources.Self.Api.Model.V2;

public class PreviewRequest
{
    /// <summary>
    /// The text of own chords, in the editor's markup.
    /// </summary>
    public required string Contents { get; init; }
}
