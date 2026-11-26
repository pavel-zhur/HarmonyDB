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
    /// The scope of the internal clients (configuration <c>SourcesSelf:InternalClients</c>) that may see how the text of
    /// own chords is shown before it is saved (the new shelf's editor).
    /// </summary>
    public const string PreviewScope = "preview";

    public const string IdPlaceholder = "{id}";
    public const string CodePlaceholder = "{code}";
    public const string VersionPlaceholder = "{version}";

    /// <summary>
    /// The opaque name of the source's picture, given in the source label, and the file of the picture. Both or neither.
    /// The name begins with the prefix of this source's ids, so that the index routes it here.
    /// </summary>
    public string? IconName { get; set; }

    public string? IconPath { get; set; }

    /// <summary>
    /// The address of own chords this source gives (their public page), of the same three parts as the store's own
    /// address: <see cref="IdPlaceholder"/> (the item's id), <see cref="CodePlaceholder"/> (the six-digit code) and
    /// <see cref="VersionPlaceholder"/> (the version's number), each once. The store's address is not changed: it is
    /// the old shelf's, and changing it would change every item's current address.
    /// </summary>
    public string? AddressTemplate { get; set; }
}