using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Billing.Api.Client;
using OneShelf.Common.OpenAi.Models;
using OpenAI;
using OpenAI.Audio;

namespace OneShelf.Common.OpenAi.Services;

public class Transcriber
{
    private readonly ILogger<Transcriber> _logger;
    private readonly BillingApiClient _billingApiClient;
    private readonly OpenAIClient _client;
    private readonly OpenAiOptions _options;

    public Transcriber(IOptions<OpenAiOptions> options, ILogger<Transcriber> logger, BillingApiClient billingApiClient)
    {
        _logger = logger;
        _billingApiClient = billingApiClient;
        _options = options.Value;
        _client = new(new(options.Value.OpenAiApiKey));
    }

    public async Task<string> TranscribeAudio(byte[] audio, DialogConfiguration configuration)
    {
        var started = DateTime.Now;
        using var stream = new MemoryStream(audio);
        var model = "whisper-1";
        var response = await _client.AudioEndpoint.CreateTranscriptionJsonAsync(new(stream, "stream.webm", model, responseFormat: AudioResponseFormat.Verbose_Json));
        _logger.LogInformation("Audio transcribed. Took {ms} ms.", DateTime.Now - started);
        await _billingApiClient.Add(new()
        {
            Count = (int)Math.Ceiling(response.Duration ?? 0),
            UserId = configuration.UserId,
            Model = model,
            UseCase = configuration.UseCase,
            AdditionalInfo = configuration.AdditionalBillingInfo,
            DomainId = configuration.DomainId,
            ChatId = configuration.ChatId,
        });

        return response.Text;
    }
}