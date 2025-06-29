using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Billing.Api.Client;
using OneShelf.Common.OpenAi.Models;

namespace OneShelf.Common.OpenAi.Services;

public class VideoGenerator
{
    private readonly ILogger<VideoGenerator> _logger;
    private readonly BillingApiClient _billingApiClient;
    private readonly OpenAiOptions _options;
    private readonly HttpClient _httpClient;

    public VideoGenerator(IOptions<OpenAiOptions> options, ILogger<VideoGenerator> logger, BillingApiClient billingApiClient, HttpClient httpClient)
    {
        _logger = logger;
        _billingApiClient = billingApiClient;
        _options = options.Value;
        _httpClient = httpClient;
    }

    public async Task<VideoGenerationResult> GenerateVideo(VideoGenerationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var started = DateTime.Now;

            // Create video generation job
            var jobId = await CreateVideoJob(request, cancellationToken);
            if (jobId == null)
            {
                return new()
                {
                    VideoData = [],
                    Success = false,
                    ErrorMessage = "Failed to create video generation job"
                };
            }

            _logger.LogInformation("Video job created: {jobId}", jobId);

            // Poll for completion
            var maxWaitTime = TimeSpan.FromMinutes(10);
            var pollInterval = TimeSpan.FromSeconds(5);
            var endTime = DateTime.Now.Add(maxWaitTime);

            while (DateTime.Now < endTime)
            {
                var status = await GetJobStatus(jobId, cancellationToken);
                if (status == null)
                {
                    return new()
                    {
                        VideoData = [],
                        Success = false,
                        ErrorMessage = "Failed to get job status"
                    };
                }

                _logger.LogInformation("Job {jobId} status: {status}", jobId, status.Status);

                if (status.Status == "succeeded")
                {
                    // Download video
                    var generationId = status.Generations?.FirstOrDefault()?.Id;
                    if (generationId != null)
                    {
                        var videoData = await DownloadVideoData(generationId, cancellationToken);
                        if (videoData is { Length: > 0 })
                        {
                            _logger.LogInformation("Video generated successfully. Took {ms} ms. Size: {size} bytes", 
                                (DateTime.Now - started).TotalMilliseconds, videoData.Length);

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
                        ErrorMessage = "No generation ID or video data found"
                    };
                }
                else if (status.Status == "failed")
                {
                    var errorMessage = status.Error?.Message ?? "Unknown error";
                    return new()
                    {
                        VideoData = [],
                        Success = false,
                        ErrorMessage = $"Video generation failed: {errorMessage}"
                    };
                }
                else if (status.Status == "cancelled")
                {
                    return new()
                    {
                        VideoData = [],
                        Success = false,
                        ErrorMessage = "Video generation was cancelled"
                    };
                }

                // Still processing, wait and retry
                await Task.Delay(pollInterval, cancellationToken);
            }

            return new()
            {
                VideoData = [],
                Success = false,
                ErrorMessage = "Video generation timed out"
            };
        }
        catch (TaskCanceledException)
        {
            return new()
            {
                VideoData = [],
                Success = false,
                ErrorMessage = "Generation cancelled"
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error generating video. {prompt}", request.Prompt);
            return new()
            {
                VideoData = [],
                Success = false,
                ErrorMessage = e.Message
            };
        }
    }

    private async Task<string?> CreateVideoJob(VideoGenerationRequest request, CancellationToken cancellationToken)
    {
        var url = $"{_options.AzureOpenAiEndpoint.TrimEnd('/')}/openai/v1/video/generations/jobs?api-version={_options.AzureOpenAiApiVersion}";

        var body = new
        {
            prompt = request.Prompt,
            width = request.Width,
            height = request.Height,
            n_seconds = request.Duration,
            model = request.Model
        };

        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        httpRequest.Headers.Add("api-key", _options.AzureOpenAiApiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to create video job: {statusCode} {error}", response.StatusCode, error);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var jobData = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        if (jobData.TryGetProperty("id", out var idElement))
        {
            return idElement.GetString();
        }

        return null;
    }

    private async Task<JobStatus?> GetJobStatus(string jobId, CancellationToken cancellationToken)
    {
        var url = $"{_options.AzureOpenAiEndpoint.TrimEnd('/')}/openai/v1/video/generations/jobs/{jobId}?api-version={_options.AzureOpenAiApiVersion}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Add("api-key", _options.AzureOpenAiApiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get job status: {statusCode}", response.StatusCode);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<JobStatus>(responseJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private async Task<byte[]?> DownloadVideoData(string generationId, CancellationToken cancellationToken)
    {
        var url = $"{_options.AzureOpenAiEndpoint.TrimEnd('/')}/openai/v1/video/generations/{generationId}/content/video?api-version={_options.AzureOpenAiApiVersion}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Add("api-key", _options.AzureOpenAiApiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to download video data: {statusCode}", response.StatusCode);
            return null;
        }

        // Download the video data as bytes
        var videoBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        _logger.LogInformation("Downloaded video data: {size} bytes", videoBytes.Length);
        
        return videoBytes;
    }

    private class JobStatus
    {
        public string Status { get; set; } = "";
        public Generation[]? Generations { get; set; }
        public ErrorInfo? Error { get; set; }
    }

    private class Generation
    {
        public string Id { get; set; } = "";
    }

    private class ErrorInfo
    {
        public string Message { get; set; } = "";
    }
}