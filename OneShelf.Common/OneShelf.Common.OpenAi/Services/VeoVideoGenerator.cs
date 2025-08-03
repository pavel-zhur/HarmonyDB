using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Billing.Api.Client;
using OneShelf.Common.OpenAi.Models;

namespace OneShelf.Common.OpenAi.Services;

public class VeoVideoGenerator
{
    private readonly ILogger<VeoVideoGenerator> _logger;
    private readonly BillingApiClient _billingApiClient;
    private readonly OpenAiOptions _options;
    private readonly HttpClient _httpClient;

    public VeoVideoGenerator(IOptions<OpenAiOptions> options, ILogger<VeoVideoGenerator> logger, BillingApiClient billingApiClient, HttpClient httpClient)
    {
        _logger = logger;
        _billingApiClient = billingApiClient;
        _options = options.Value;
        _httpClient = httpClient;
    }

    public async Task<VideoGenerationResult> GenerateVideo(VeoVideoGenerationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var started = DateTime.Now;

            // Create Veo video generation operation
            var operationName = await CreateVideoOperation(request, cancellationToken);
            if (operationName == null)
            {
                return new()
                {
                    VideoData = [],
                    Success = false,
                    ErrorMessage = "Failed to create Veo video generation operation"
                };
            }

            _logger.LogInformation("Veo operation created: {operationName}", operationName);

            // Poll for completion (Veo can take 11 seconds to 6 minutes)
            var maxWaitTime = TimeSpan.FromMinutes(10);
            var pollInterval = TimeSpan.FromSeconds(10);
            var endTime = DateTime.Now.Add(maxWaitTime);

            while (DateTime.Now < endTime)
            {
                var operationStatus = await GetOperationStatus(operationName, cancellationToken);
                if (operationStatus == null)
                {
                    return new()
                    {
                        VideoData = [],
                        Success = false,
                        ErrorMessage = "Failed to get Veo operation status"
                    };
                }

                _logger.LogInformation("Veo operation {operationName} status: done={done}", operationName, operationStatus.Done);

                if (operationStatus.Done)
                {
                    if (operationStatus.Error != null)
                    {
                        return new()
                        {
                            VideoData = [],
                            Success = false,
                            ErrorMessage = $"Veo generation failed: {operationStatus.Error.Message}"
                        };
                    }

                    // Download video
                    var videoUri = operationStatus.Response?.GenerateVideoResponse?.GeneratedSamples?.FirstOrDefault()?.Video?.Uri;
                    if (!string.IsNullOrEmpty(videoUri))
                    {
                        var videoData = await DownloadVideo(videoUri, cancellationToken);
                        if (videoData is { Length: > 0 })
                        {
                            _logger.LogInformation("Veo video generated successfully. Took {ms} ms. Size: {size} bytes", 
                                (DateTime.Now - started).TotalMilliseconds, videoData.Length);

                            await _billingApiClient.Add(new()
                            {
                                Count = 8,
                                UserId = request.UserId,
                                Model = request.Model,
                                UseCase = request.UseCase,
                                AdditionalInfo = request.AdditionalBillingInfo,
                                DomainId = request.DomainId,
                                ChatId = request.ChatId,
                            });

                            return new()
                            {
                                VideoData = videoData,
                                Success = true
                            };
                        }
                    }

                    return new()
                    {
                        VideoData = [],
                        Success = false,
                        ErrorMessage = "No video URI found in Veo response"
                    };
                }

                // Still processing, wait and retry
                await Task.Delay(pollInterval, cancellationToken);
            }

            return new()
            {
                VideoData = [],
                Success = false,
                ErrorMessage = "Veo video generation timed out"
            };
        }
        catch (TaskCanceledException)
        {
            return new()
            {
                VideoData = [],
                Success = false,
                ErrorMessage = "Veo generation cancelled"
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error generating Veo video. {prompt}", request.Prompt);
            return new()
            {
                VideoData = [],
                Success = false,
                ErrorMessage = e.Message
            };
        }
    }

    private async Task<string?> CreateVideoOperation(VeoVideoGenerationRequest request, CancellationToken cancellationToken)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{request.Model}:predictLongRunning";

        var instance = new
        {
            prompt = request.Prompt
        };

        object body;

        // Create body with or without parameters based on negative prompt
        if (!string.IsNullOrWhiteSpace(request.NegativePrompt))
        {
            body = new
            {
                instances = new[] { instance },
                parameters = new
                {
                    negativePrompt = request.NegativePrompt
                }
            };
        }
        else
        {
            body = new
            {
                instances = new[] { instance }
            };
        }

        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        httpRequest.Headers.Add("x-goog-api-key", _options.GoogleApiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to create Veo operation: {statusCode} {error}", response.StatusCode, error);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var operationData = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        if (operationData.TryGetProperty("name", out var nameElement))
        {
            return nameElement.GetString();
        }

        return null;
    }

    private async Task<OperationStatus?> GetOperationStatus(string operationName, CancellationToken cancellationToken)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/{operationName}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Add("x-goog-api-key", _options.GoogleApiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get Veo operation status: {statusCode}", response.StatusCode);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<OperationStatus>(responseJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private async Task<byte[]?> DownloadVideo(string videoUri, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, videoUri);
        httpRequest.Headers.Add("x-goog-api-key", _options.GoogleApiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to download Veo video: {statusCode}", response.StatusCode);
            return null;
        }

        var videoBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        _logger.LogInformation("Downloaded Veo video data: {size} bytes", videoBytes.Length);
        
        return videoBytes;
    }

    private class OperationStatus
    {
        public bool Done { get; set; }
        public OperationError? Error { get; set; }
        public OperationResponse? Response { get; set; }
    }

    private class OperationError
    {
        public string Message { get; set; } = "";
    }

    private class OperationResponse
    {
        public GenerateVideoResponse? GenerateVideoResponse { get; set; }
    }

    private class GenerateVideoResponse
    {
        public GeneratedSample[]? GeneratedSamples { get; set; }
    }

    private class GeneratedSample
    {
        public VideoFile? Video { get; set; }
    }

    private class VideoFile
    {
        public string Uri { get; set; } = "";
    }
}