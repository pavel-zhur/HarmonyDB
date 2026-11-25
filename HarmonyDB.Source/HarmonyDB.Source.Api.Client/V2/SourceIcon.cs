namespace HarmonyDB.Source.Api.Client.V2;

/// <summary>
/// A source's picture, as the source gave it.
/// </summary>
public record SourceIcon(byte[] Content, string ContentType);
