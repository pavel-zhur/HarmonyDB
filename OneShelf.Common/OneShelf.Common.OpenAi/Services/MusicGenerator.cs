using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.AIPlatform.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Billing.Api.Client;
using OneShelf.Common.OpenAi.Models;
using Value = Google.Protobuf.WellKnownTypes.Value;

namespace OneShelf.Common.OpenAi.Services;

public class MusicGenerator
{
    private readonly ILogger<MusicGenerator> _logger;
    private readonly BillingApiClient _billingApiClient;
    private readonly OpenAiOptions _options;
    private readonly PredictionServiceClient _predictionClient;

    public MusicGenerator(IOptions<OpenAiOptions> options, ILogger<MusicGenerator> logger, BillingApiClient billingApiClient)
    {
        _logger = logger;
        _billingApiClient = billingApiClient;
        _options = options.Value;

        var credential = GoogleCredential.FromJson(_options.GoogleServiceAccountJson);
        var clientBuilder = new PredictionServiceClientBuilder
        {
            Credential = credential
        };
        _predictionClient = clientBuilder.Build();
    }

    public async Task<MusicGenerationResult> GenerateMusic(MusicGenerationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var started = DateTime.Now;

            var instanceFields = new Dictionary<string, Value>
            {
                ["prompt"] = Value.ForString(request.Prompt)
            };

            if (!string.IsNullOrEmpty(request.NegativePrompt))
            {
                instanceFields["negative_prompt"] = Value.ForString(request.NegativePrompt);
            }

            var instances = new List<Value>
            {
                Value.ForStruct(new()
                {
                    Fields = { instanceFields }
                })
            };

            var parameters = Value.ForStruct(new()
            {
                Fields =
                {
                    ["sample_count"] = Value.ForNumber(1),
                }
            });

            var endpoint = $"projects/{_options.GoogleCloudProjectId}/locations/{_options.GoogleCloudLocation}/publishers/google/models/{request.Model}";

            var predictRequest = new PredictRequest
            {
                Endpoint = endpoint,
                Instances = { instances },
                Parameters = parameters
            };

            var response = await _predictionClient.PredictAsync(predictRequest, cancellationToken);

            _logger.LogInformation("Music generated. Took {ms} ms. Model: {model}.", 
                (DateTime.Now - started).TotalMilliseconds, request.Model);

            await _billingApiClient.Add(new()
            {
                Count = 1,
                UserId = request.UserId,
                Model = request.Model,
                UseCase = request.UseCase,
                AdditionalInfo = request.AdditionalBillingInfo,
                DomainId = request.DomainId,
                ChatId = request.ChatId,
            });

            // Extract audio content from response (Lyria returns base64 encoded audio, not URLs)
            var prediction = response.Predictions.FirstOrDefault();
            if (prediction?.StructValue?.Fields?.TryGetValue("bytesBase64Encoded", out var audioContentValue) == true)
            {
                var base64Audio = audioContentValue.StringValue;
                if (!string.IsNullOrEmpty(base64Audio))
                {
                    _logger.LogInformation("Found base64 audio content, length: {length} characters", base64Audio.Length);
                    
                    // Convert base64 to raw bytes for proper file upload
                    var audioBytes = Convert.FromBase64String(base64Audio);
                    _logger.LogInformation("Converted to {size} bytes of audio data", audioBytes.Length);
                    
                    return new()
                    {
                        AudioData = audioBytes,
                        Success = true
                    };
                }
            }

            // Log the response structure for debugging if audioContent not found
            _logger.LogWarning("audioContent not found. Response structure: {response}", response.ToString());

            return new()
            {
                AudioData = [],
                Success = false,
                ErrorMessage = "No audio content found in response"
            };
        }
        catch (TaskCanceledException)
        {
            return new()
            {
                AudioData = [],
                Success = false,
                ErrorMessage = "Generation cancelled"
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error generating music. {prompt}", request.Prompt);
            return new()
            {
                AudioData = [],
                Success = false,
                ErrorMessage = e.Message
            };
        }
    }
}