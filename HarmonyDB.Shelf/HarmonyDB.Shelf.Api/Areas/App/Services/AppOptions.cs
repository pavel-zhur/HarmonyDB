using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.App.Services;

/// <summary>
/// The configuration section <c>App</c>: what the frontends are told at start beyond the sign-in.
/// </summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// The id of the playlist the public page shows as its showcase. Not set (or empty): no showcase. It is not
    /// required to exist when the app starts.
    /// </summary>
    public string? ShowcasePlaylistId
    {
        get => _showcasePlaylistId;
        set => _showcasePlaylistId = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string? _showcasePlaylistId;

    /// <summary>
    /// <see cref="ShowcasePlaylistId"/> as an id; null when it is not set (the validator refuses one that is no id).
    /// </summary>
    public Guid? ShowcasePlaylist => Guid.TryParse(ShowcasePlaylistId, out var id) ? id : null;
}

internal sealed class AppOptionsValidator : IValidateOptions<AppOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options)
        => options.ShowcasePlaylistId != null && !Guid.TryParse(options.ShowcasePlaylistId, out _)
            ? ValidateOptionsResult.Fail($"{AppOptions.SectionName}:{nameof(AppOptions.ShowcasePlaylistId)} is not a playlist's id.")
            : ValidateOptionsResult.Success;
}
