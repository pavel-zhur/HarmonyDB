using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Clients.Pdfs;

/// <summary>
/// The PDF service, called as an internal client with the shelf's key: an HTML page into a PDF, and PDFs of songs into
/// a book. Not configured, or not answering: 503 <c>unavailable</c>.
/// </summary>
public sealed partial class PdfsClient(HttpClient http, IOptions<PdfsClientOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PdfsClientOptions _options = options.Value;

    /// <summary>
    /// The page (a whole HTML document) as an A4 PDF.
    /// </summary>
    public Task<MadePdf> ConvertAsync(string html, CancellationToken cancellationToken)
        => PostAsync("Convert", new StringContent(html, Encoding.UTF8, "text/html"), cancellationToken);

    /// <summary>
    /// The book of the songs' PDFs: with a caption, a title page, the songs' numbers on their pages and the empty pages
    /// that let a song of several pages open as a spread; without one, the PDFs one after another.
    /// </summary>
    /// <param name="qrLink">Where the QR code on the title page leads.</param>
    public Task<MadePdf> BookAsync(string? caption, string? year, Uri? qrLink, IReadOnlyList<(byte[] Pdf, int? Number)> documents, CancellationToken cancellationToken)
        => PostAsync("Book", JsonContent.Create(new
        {
            caption,
            year,
            qrLink,
            documents = documents.Select(x => new { pdf = x.Pdf, number = x.Number }),
        }, options: Json), cancellationToken);

    private async Task<MadePdf> PostAsync(string operation, HttpContent content, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured) throw Unavailable();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.Endpoint!, operation));
            message.Headers.Add(InternalKeyDefaults.HeaderName, _options.Key);
            message.Content = content;
            using var response = await http.SendAsync(message, timeout.Token);
            if (!response.IsSuccessStatusCode) throw Unavailable();

            var name = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
            var pages = name == null ? null : PageCount().Match(name);
            if (pages is not { Success: true }) throw Unavailable();

            return new(await response.Content.ReadAsByteArrayAsync(timeout.Token), int.Parse(pages.Groups[1].Value));
        }
        catch (Exception exception) when (exception is HttpRequestException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw Unavailable();
        }
    }

    private static ShelfException Unavailable() => new(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);

    [GeneratedRegex(@"^(\d+)\.pdf$")]
    private static partial Regex PageCount();
}

public sealed record MadePdf(byte[] Data, int PageCount);

/// <summary>
/// The configuration section <c>Clients:Pdfs</c>.
/// </summary>
public sealed class PdfsClientOptions
{
    public const string SectionName = "Clients:Pdfs";

    /// <summary>
    /// The base address of the service's operations, ending with a slash (<c>http://host/pdfs/api/</c>).
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// The shelf's internal key there. Secret.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// How long one conversion, or putting a book together, may take.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    public bool IsConfigured => Endpoint != null;
}

internal sealed class PdfsClientOptionsValidator : IValidateOptions<PdfsClientOptions>
{
    public ValidateOptionsResult Validate(string? name, PdfsClientOptions options)
    {
        var section = PdfsClientOptions.SectionName;
        if (options.Timeout <= TimeSpan.Zero)
            return ValidateOptionsResult.Fail($"{section}:Timeout is not positive.");
        if (options.Endpoint == null && string.IsNullOrEmpty(options.Key))
            return ValidateOptionsResult.Success;
        if (options.Endpoint is not { IsAbsoluteUri: true } || !options.Endpoint.AbsolutePath.EndsWith('/'))
            return ValidateOptionsResult.Fail($"{section}:Endpoint is not an absolute address ending with a slash.");

        return string.IsNullOrEmpty(options.Key) || options.Key.Length < InternalClientsOptions.MinimumKeyLength
            ? ValidateOptionsResult.Fail($"{section}:Key is shorter than {InternalClientsOptions.MinimumKeyLength} characters.")
            : ValidateOptionsResult.Success;
    }
}

public static class PdfsClientExtensions
{
    /// <summary>
    /// Adds <see cref="PdfsClient"/>; safe to call from every area that needs it.
    /// </summary>
    public static IServiceCollection AddPdfsClient(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(PdfsClient)))
            return services;

        services.AddOptions<PdfsClientOptions>().Bind(configuration.GetSection(PdfsClientOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<PdfsClientOptions>, PdfsClientOptionsValidator>();
        services.AddHttpClient<PdfsClient>(x => x.Timeout = Timeout.InfiniteTimeSpan);
        return services;
    }
}
