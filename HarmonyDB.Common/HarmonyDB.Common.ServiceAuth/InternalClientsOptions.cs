using Microsoft.Extensions.Options;

namespace HarmonyDB.Common.ServiceAuth;

/// <summary>
/// The internal clients of this service, bound from the configuration section <see cref="SectionName"/>.
/// </summary>
public sealed class InternalClientsOptions
{
    public const string SectionName = "InternalClients";

    /// <summary>
    /// The shortest key accepted: a key is a random secret, not a password a person remembers.
    /// </summary>
    public const int MinimumKeyLength = 32;

    public List<InternalClient> Clients { get; set; } = [];
}

internal sealed class InternalClientsOptionsValidator : IValidateOptions<InternalClientsOptions>
{
    public ValidateOptionsResult Validate(string? name, InternalClientsOptions options)
    {
        var failures = new List<string>();

        for (var i = 0; i < options.Clients.Count; i++)
        {
            var client = options.Clients[i];
            var where = $"{InternalClientsOptions.SectionName}:{i}";

            if (string.IsNullOrWhiteSpace(client.Name))
                failures.Add($"{where}: the client has no name.");

            if (string.IsNullOrEmpty(client.Key) || client.Key.Length < InternalClientsOptions.MinimumKeyLength)
                failures.Add($"{where} ({client.Name}): the key is shorter than {InternalClientsOptions.MinimumKeyLength} characters.");

            if (client.Scopes.Any(string.IsNullOrWhiteSpace))
                failures.Add($"{where} ({client.Name}): a scope is empty.");
        }

        failures.AddRange(options.Clients
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{InternalClientsOptions.SectionName}: the name '{g.Key}' is given to more than one client."));

        if (options.Clients.Where(x => !string.IsNullOrEmpty(x.Key)).GroupBy(x => x.Key, StringComparer.Ordinal).Any(g => g.Count() > 1))
            failures.Add($"{InternalClientsOptions.SectionName}: two clients have the same key.");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
