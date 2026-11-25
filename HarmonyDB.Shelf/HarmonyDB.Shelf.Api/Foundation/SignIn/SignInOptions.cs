using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Foundation.SignIn;

/// <summary>
/// The configuration section <c>SignIn</c>: which sign-in providers are on, and their settings. A provider's secrets
/// (bot token, client secret) are added to its own class here by whoever builds the provider.
/// </summary>
public sealed class SignInOptions
{
    public const string SectionName = "SignIn";

    public TelegramSignInOptions Telegram { get; set; } = new();

    public SignInProviderOptions Google { get; set; } = new();

    public SignInProviderOptions Yandex { get; set; } = new();

    /// <summary>
    /// Signs in as a named test account. Works only when the app runs in the Development environment as well.
    /// </summary>
    public SignInProviderOptions Dev { get; set; } = new();
}

public class SignInProviderOptions
{
    public bool Enabled { get; set; }
}

public sealed class TelegramSignInOptions : SignInProviderOptions
{
    /// <summary>
    /// The user name of the bot of the login widget.
    /// </summary>
    public string? BotUsername { get; set; }
}

/// <summary>
/// The provider names, as the API and the database (<c>account_logins.provider</c>) write them.
/// </summary>
public static class SignInProviders
{
    public const string Telegram = "telegram";
    public const string Google = "google";
    public const string Yandex = "yandex";
    public const string Dev = "dev";
}

internal sealed class SignInOptionsValidator : IValidateOptions<SignInOptions>
{
    public ValidateOptionsResult Validate(string? name, SignInOptions options)
        => options.Telegram.Enabled && string.IsNullOrWhiteSpace(options.Telegram.BotUsername)
            ? ValidateOptionsResult.Fail("SignIn:Telegram is enabled without its BotUsername.")
            : ValidateOptionsResult.Success;
}
