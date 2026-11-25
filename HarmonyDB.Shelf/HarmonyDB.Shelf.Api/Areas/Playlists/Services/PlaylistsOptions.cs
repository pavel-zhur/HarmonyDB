using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Playlists.Services;

/// <summary>
/// The configuration section <c>Playlists</c>. Every setting has a default.
/// </summary>
public sealed class PlaylistsOptions
{
    public const string SectionName = "Playlists";

    /// <summary>
    /// A playlist of more songs than this, joined, starts out of the person's library (they show it there themselves).
    /// </summary>
    public int LibraryShowLimit { get; set; } = 2000;
}

internal sealed class PlaylistsOptionsValidator : IValidateOptions<PlaylistsOptions>
{
    public ValidateOptionsResult Validate(string? name, PlaylistsOptions options)
        => options.LibraryShowLimit < 0
            ? ValidateOptionsResult.Fail($"{PlaylistsOptions.SectionName}:{nameof(PlaylistsOptions.LibraryShowLimit)} is negative.")
            : ValidateOptionsResult.Success;
}
