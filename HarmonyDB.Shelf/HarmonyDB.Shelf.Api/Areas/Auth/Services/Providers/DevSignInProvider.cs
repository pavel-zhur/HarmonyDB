using System.Text.RegularExpressions;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services.Providers;

/// <summary>
/// Signs in as a named test account, with no check: it is how the local stack is walked. It exists only when the app
/// runs in the Development environment and <c>SignIn:Dev:Enabled</c> is on.
/// </summary>
public sealed partial class DevSignInProvider(IOptionsMonitor<SignInOptions> options, IHostEnvironment environment) : ISignInProvider
{
    public string Name => SignInProviders.Dev;

    public bool IsEnabled => options.CurrentValue.Dev.Enabled && environment.IsDevelopment();

    public bool SendsAway => false;

    public Task<string> GetAuthorizationUrlAsync(SignInFlow flow, CancellationToken cancellationToken)
        => throw new NotSupportedException("The dev sign-in is answered in the app.");

    /// <summary>
    /// The answer: <c>name</c>, the test account's name (letters, digits, "-", "_"; up to 64).
    /// </summary>
    public Task<SignInIdentity> CompleteAsync(SignInFlow flow, IReadOnlyDictionary<string, string> answer, CancellationToken cancellationToken)
    {
        if (!answer.TryGetValue("name", out var name) || !NamePattern().IsMatch(name))
            throw SignInProviderException.Invalid("Dev: the name is missing or not allowed.");

        return Task.FromResult(new SignInIdentity(name, name) { Username = name });
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex NamePattern();
}
