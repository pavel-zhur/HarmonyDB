using HarmonyDB.Shelf.Api.Foundation.SignIn;
using HarmonyDB.Shelf.Model.App;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.App.Services;

/// <summary>
/// What the frontend needs at start. Nothing secret, and nothing of the permissions (their keys and labels are for the
/// admin site only).
/// </summary>
public sealed class PublicConfigurationService(IOptionsMonitor<SignInOptions> signIn, IHostEnvironment environment)
{
    /// <summary>
    /// The languages of the interface; the first is the default.
    /// </summary>
    public static readonly IReadOnlyList<string> Locales = ["en", "ru"];

    public PublicConfiguration Get()
    {
        var options = signIn.CurrentValue;
        var providers = new List<SignInProviderInfo>();

        if (options.Telegram.Enabled)
            providers.Add(new() { Name = SignInProviders.Telegram, TelegramBotUsername = options.Telegram.BotUsername });

        if (options.Google.Enabled)
            providers.Add(new() { Name = SignInProviders.Google });

        if (options.Yandex.Enabled)
            providers.Add(new() { Name = SignInProviders.Yandex });

        if (options.Dev.Enabled && environment.IsDevelopment())
            providers.Add(new() { Name = SignInProviders.Dev });

        return new()
        {
            SignInProviders = providers,
            Locales = Locales,
            DefaultLocale = Locales[0],
        };
    }
}
