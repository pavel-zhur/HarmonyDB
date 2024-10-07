using CasCap.Common.Extensions;
using CasCap.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Videos.BusinessLogic.Models;
using System.Net.Http.Headers;
using Nito.AsyncEx;

namespace OneShelf.Videos.BusinessLogic.Services.GooglePhotosExtensions.NonInteractive;

public class GoogleNonInteractiveLogin : IDisposable
{
    private static readonly IReadOnlyDictionary<GooglePhotosScope, string> DScopes = new Dictionary<GooglePhotosScope, string>
    {
        { GooglePhotosScope.ReadOnly, "https://www.googleapis.com/auth/photoslibrary.readonly" },
        { GooglePhotosScope.AppendOnly, "https://www.googleapis.com/auth/photoslibrary.appendonly" },
        { GooglePhotosScope.AppCreatedData, "https://www.googleapis.com/auth/photoslibrary.readonly.appcreateddata" },
        { GooglePhotosScope.Access, "https://www.googleapis.com/auth/photoslibrary" },
        { GooglePhotosScope.Sharing, "https://www.googleapis.com/auth/photoslibrary.sharing" }
    };

    private readonly ILogger<GoogleNonInteractiveLogin> _logger;
    private readonly GooglePhotosOptions _options;
    private readonly VideosOptions _videosOptions;
    private readonly AsyncLock _lock = new();

    private (CancellationTokenSource codeReceived, ExtendedCodeReceiver codeReceiver, Task<AuthenticationHeaderValue> mainTask)? _ongoingLogin;

    public GoogleNonInteractiveLogin(ILogger<GoogleNonInteractiveLogin> logger, IOptions<GooglePhotosOptions> options, IOptions<VideosOptions> videosOptions)
    {
        _logger = logger;
        _options = options.Value;
        _videosOptions = videosOptions.Value;
    }

    public async Task<string?> Login(string? response)
    {
        using var _ = await _lock.LockAsync();

        if (_ongoingLogin.HasValue)
        {
            if (response == null)
            {
                return _ongoingLogin.Value.codeReceiver.Question!;
            }

            if (_ongoingLogin == null)
            {
                return null;
            }

            _ongoingLogin.Value.codeReceiver.GotResponse(response);
            await _ongoingLogin.Value.codeReceived.CancelAsync();
            await _ongoingLogin.Value.mainTask;
            _ongoingLogin.Value.codeReceived.Dispose();
            _ongoingLogin = null;
            return null;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        using var cancellationTokenSource2 = new CancellationTokenSource();
        var extendedCodeReceiver = new ExtendedCodeReceiver(async () =>
        {
            await cancellationTokenSource2.CancelAsync();
            cancellationTokenSource.Token.WaitHandle.WaitOne();
        });

        var mainTask = Login(extendedCodeReceiver);
        _ongoingLogin = (cancellationTokenSource, extendedCodeReceiver, mainTask);

        try
        {
            await mainTask.WaitAsync(cancellationTokenSource2.Token);
            _ongoingLogin = null;
            return null;
        }
        catch (TaskCanceledException)
        {
            return extendedCodeReceiver.Question;
        }
    }

    public void Dispose()
    {
        _ongoingLogin?.codeReceived.Cancel();
    }

    public async Task<AuthenticationHeaderValue?> GetCredentials()
    {
        return await Login(new ExceptionCodeReceiver());
    }

    private async Task<AuthenticationHeaderValue> Login(ICodeReceiver codeReceiver)
    {
        await Task.Yield();

        if (!_videosOptions.UseNonInteractiveLogin)
        {
            throw new InvalidOperationException("Could not have happened. The interactive login is configured in the options.");
        }

        if (_options is null) throw new ArgumentNullException(nameof(_options), $"{nameof(GooglePhotosOptions)}.{nameof(_options)} cannot be null!");
        if (string.IsNullOrWhiteSpace(_options.User)) throw new ArgumentNullException(nameof(_options.User), $"{nameof(GooglePhotosOptions)}.{nameof(_options.User)} cannot be null!");
        if (string.IsNullOrWhiteSpace(_options.ClientId)) throw new ArgumentNullException(nameof(_options.ClientId), $"{nameof(GooglePhotosOptions)}.{nameof(_options.ClientId)} cannot be null!");
        if (string.IsNullOrWhiteSpace(_options.ClientSecret)) throw new ArgumentNullException(nameof(_options.ClientSecret), $"{nameof(GooglePhotosOptions)}.{nameof(_options.ClientSecret)} cannot be null!");
        if (_options.Scopes.IsNullOrEmpty()) throw new ArgumentNullException(nameof(_options.Scopes), $"{nameof(GooglePhotosOptions)}.{nameof(_options.Scopes)} cannot be null/empty!");

        var secrets = new ClientSecrets { ClientId = _options.ClientId, ClientSecret = _options.ClientSecret };

        FileDataStore? dataStore = null;
        if (!string.IsNullOrWhiteSpace(_options.FileDataStoreFullPathOverride))
            dataStore = new(_options.FileDataStoreFullPathOverride, true);

        _logger.LogDebug("Requesting authorization...");
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            GetScopes(),
            _options.User,
            CancellationToken.None,
            dataStore,
            codeReceiver);

        _logger.LogDebug("Authorisation granted or not required (if the saved access token already available)");

        if (credential.Token.IsStale)
        {
            _logger.LogWarning("The access token has expired, refreshing it");
            if (await credential.RefreshTokenAsync(CancellationToken.None))
                _logger.LogInformation("The access token is now refreshed");
            else
            {
                throw new("The access token has expired but we can't refresh it :(");
            }
        }
        else
            _logger.LogDebug("The access token is OK, continue");

        return new(credential.Token.TokenType, credential.Token.AccessToken);
    }

    private string[] GetScopes()
    {
        var l = new List<string>(_options.Scopes.Length);
        foreach (var scope in _options.Scopes)
            if (DScopes.TryGetValue(scope, out var s))
                l.Add(s);
        return l.ToArray();
    }
}