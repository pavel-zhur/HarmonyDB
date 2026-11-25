namespace HarmonyDB.Shelf.Model.App;

public record VersionResponse
{
    /// <summary>
    /// The version of the backend's build.
    /// </summary>
    public required string Version { get; init; }
}
