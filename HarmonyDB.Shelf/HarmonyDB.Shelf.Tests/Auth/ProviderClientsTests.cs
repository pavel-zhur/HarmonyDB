using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HarmonyDB.Shelf.Api.Areas.Auth;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using HarmonyDB.Shelf.Model.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Shelf.Tests.Auth;

/// <summary>
/// The sign-in providers' sites as the shelf's clients meet them: an answer compressed though not asked for is read,
/// and a provider failing a sign-in when it starts is logged with its cause.
/// </summary>
public sealed class ProviderClientsTests
{
    private const string Document = """{"issuer":"https://provider.example","keys":[]}""";

    [Fact]
    public async Task EveryProvidersClientReadsAnAnswerCompressedWithoutBeingAsked()
    {
        var port = FreePort();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Logging.ClearProviders();
        await using var site = builder.Build();
        // as Telegram sends its keys: gzipped, whatever the request asked
        site.Run(async context =>
        {
            context.Response.ContentType = "application/json";
            context.Response.Headers.ContentEncoding = "gzip";
            await using var gzip = new GZipStream(context.Response.Body, CompressionLevel.Fastest, leaveOpen: true);
            await gzip.WriteAsync(Encoding.UTF8.GetBytes(Document));
        });
        await site.StartAsync();

        await using var api = new AuthApiFactory(null, "Production");
        var clients = api.Services.GetRequiredService<IHttpClientFactory>();
        var names = api.Services.GetRequiredService<SignInProviderClients>().Names;
        Assert.NotEmpty(names);
        foreach (var name in names)
            Assert.Equal(Document, await clients.CreateClient(name).GetStringAsync($"http://127.0.0.1:{port}/.well-known/jwks.json"));
    }

    [Fact]
    public async Task AProviderFailingASignInWhenItStartsIsLoggedWithItsCause()
    {
        var logs = new Logs();
        await using var api = new AuthApiFactory(null, "Production", new Dictionary<string, string?>
        {
            ["SignIn:Telegram:Enabled"] = "true",
            ["SignIn:Telegram:ClientId"] = "123456",
            ["SignIn:Telegram:ClientSecret"] = "a-secret-of-the-tests",
        });
        using var failing = api.WithWebHostBuilder(x => x
            .ConfigureLogging(l => l.AddProvider(logs))
            .ConfigureTestServices(services => services.AddHttpClient($"{SignInProviders.Telegram}-discovery")
                .ConfigurePrimaryHttpMessageHandler(() => new Unreachable())));
        var browser = new Browser(failing.CreateClient(new() { HandleCookies = false, AllowAutoRedirect = false }));
        await browser.SendAsync(HttpMethod.Get, "/api/app/configuration");

        await Browser.AssertError(
            await browser.SendAsync(HttpMethod.Post, "/api/auth/flows", new StartSignInRequest { Provider = SignInProviders.Telegram, Purpose = SignInPurpose.SignIn }),
            HttpStatusCode.ServiceUnavailable,
            AuthErrorCodes.ProviderUnavailable);

        var logged = Assert.Single(logs.Messages, x => x.Contains("A sign-in by telegram failed", StringComparison.Ordinal));
        Assert.Contains("discovery document", logged);
        Assert.Contains("the provider is down", logged);
        Assert.DoesNotContain("a-secret-of-the-tests", logged);
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("the provider is down");
    }

    private sealed class Logs : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Logger(this);

        public void Dispose()
        {
        }

        private sealed class Logger(Logs logs) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (logs.Messages)
                    logs.Messages.Add(formatter(state, exception));
            }
        }
    }
}
