namespace HarmonyDB.Source.Api.Model.V2.Api;

/// <summary>
/// A source's picture by the opaque name of <see cref="SourceLabel.Icon"/>. Answered with the picture itself and its
/// content type, or 404.
/// </summary>
public record GetIconRequest
{
    public required string Name { get; init; }
}
