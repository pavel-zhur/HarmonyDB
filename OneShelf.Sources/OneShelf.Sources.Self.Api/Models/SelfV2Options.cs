namespace OneShelf.Sources.Self.Api.Models;

/// <summary>
/// The settings of the source contract, version 2, of the own chords (configuration section <see cref="SectionName"/>).
/// </summary>
public class SelfV2Options
{
    public const string SectionName = "SelfV2";

    /// <summary>
    /// The scope of the internal clients (configuration <c>SourcesSelf:InternalClients</c>) that may call it.
    /// </summary>
    public const string InternalScope = "source";

    /// <summary>
    /// The opaque name of the source's picture, given in the source label, and the file of the picture. Both or neither.
    /// The name begins with the prefix of this source's ids, so that the index routes it here.
    /// </summary>
    public string? IconName { get; set; }

    public string? IconPath { get; set; }

    /// <summary>
    /// Where the shelf tells which Telegram identities are linked to an account (see
    /// <see cref="Services.V2.ShelfAccountsClient"/>), and this source's internal key there. Without them the private
    /// chords cannot be opened to anyone: a call made for a person that needs them answers "unavailable".
    /// </summary>
    public Uri? ShelfAccountsEndpoint { get; set; }

    public string? ShelfAccountsKey { get; set; }

    public TimeSpan ShelfAccountsTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long the shelf's answer for an account is kept: a sign-in linked or unlinked shows after this time at most.
    /// </summary>
    public TimeSpan ShelfAccountsCacheTime { get; set; } = TimeSpan.FromMinutes(1);
}
