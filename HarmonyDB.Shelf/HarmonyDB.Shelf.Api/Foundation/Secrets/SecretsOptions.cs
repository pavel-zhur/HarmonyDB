using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Foundation.Secrets;

/// <summary>
/// The configuration section <c>Shelf:Secrets</c>: the key of what is kept encrypted at rest (music links), with its
/// version, and the keys before it, so the key can be rotated: what was encrypted under an old key is read with it
/// (the row says its key's version) and written anew under the current one.
/// </summary>
public sealed class SecretsOptions
{
    public const string SectionName = "Shelf:Secrets";

    /// <summary>
    /// The current key: 32 random bytes, base64.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The current key's version.
    /// </summary>
    public int KeyVersion { get; set; } = 1;

    /// <summary>
    /// The keys before the current one, by version (<c>Shelf:Secrets:OldKeys:1</c> = …).
    /// </summary>
    public Dictionary<int, string> OldKeys { get; set; } = [];

    /// <summary>
    /// The key of a version, current or old.
    /// </summary>
    public byte[] GetKey(int version)
        => Convert.FromBase64String(version == KeyVersion
            ? Key
            : OldKeys.TryGetValue(version, out var old) ? old : throw new InvalidOperationException($"No secrets key of version {version}."));
}

internal sealed class SecretsOptionsValidator : IValidateOptions<SecretsOptions>
{
    public ValidateOptionsResult Validate(string? name, SecretsOptions options)
    {
        var failures = new List<string>();
        if (!IsKey(options.Key))
            failures.Add("Shelf:Secrets:Key is not 32 bytes in base64.");

        if (options.OldKeys.ContainsKey(options.KeyVersion))
            failures.Add("Shelf:Secrets:OldKeys has the version of the current key.");

        failures.AddRange(options.OldKeys.Where(x => !IsKey(x.Value)).Select(x => $"Shelf:Secrets:OldKeys:{x.Key} is not 32 bytes in base64."));

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static bool IsKey(string text)
    {
        var bytes = new byte[64];
        return Convert.TryFromBase64String(text, bytes, out var length) && length == 32;
    }
}
