using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Foundation.Permissions;

/// <summary>
/// The configuration section <c>Permissions</c>: the custom permissions and the defaults.
/// </summary>
public sealed class PermissionsOptions
{
    public const string SectionName = "Permissions";

    /// <summary>
    /// Permissions unknown to the code: keys with labels for the admin site. A person's custom keys travel with every
    /// call to the sources (<c>Caller.Keys</c>); nothing else is done with them here.
    /// </summary>
    public List<CustomPermission> Custom { get; set; } = [];

    public PermissionDefaults Defaults { get; set; } = new();
}

public sealed class CustomPermission
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

public sealed class PermissionDefaults
{
    /// <summary>
    /// What a guest (no sign-in) has.
    /// </summary>
    public List<string> Guest { get; set; } = [];

    /// <summary>
    /// What a new account is given when it is made.
    /// </summary>
    public List<string> NewAccount { get; set; } = [];
}

internal sealed class PermissionsOptionsValidator : IValidateOptions<PermissionsOptions>
{
    public ValidateOptionsResult Validate(string? name, PermissionsOptions options)
    {
        var failures = new List<string>();
        var builtIn = BuiltInPermissions.All.ToHashSet(StringComparer.Ordinal);

        foreach (var custom in options.Custom)
        {
            if (string.IsNullOrWhiteSpace(custom.Key))
                failures.Add("Permissions:Custom: a permission has no key.");
            else if (builtIn.Contains(custom.Key))
                failures.Add($"Permissions:Custom: '{custom.Key}' is a built-in permission.");

            if (string.IsNullOrWhiteSpace(custom.Label))
                failures.Add($"Permissions:Custom: '{custom.Key}' has no label.");
        }

        failures.AddRange(options.Custom
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => $"Permissions:Custom: '{x.Key}' is declared more than once."));

        var known = builtIn.Concat(options.Custom.Select(x => x.Key)).ToHashSet(StringComparer.Ordinal);
        failures.AddRange(options.Defaults.Guest.Where(x => !known.Contains(x))
            .Select(x => $"Permissions:Defaults:Guest: '{x}' is not a known permission."));
        failures.AddRange(options.Defaults.NewAccount.Where(x => !known.Contains(x))
            .Select(x => $"Permissions:Defaults:NewAccount: '{x}' is not a known permission."));

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
