using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Billing.Api.Client;
using OneShelf.Common.OpenAi.Internal;
using OneShelf.Common.OpenAi.Models;
using OneShelf.Common.OpenAi.Models.Memory;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using OpenAI.Models;

namespace OneShelf.Common.OpenAi.Services;

public class DialogRunner
{
    private readonly ILogger<DialogRunner> _logger;
    private readonly BillingApiClient _billingApiClient;
    private readonly OpenAIClient _client;
    private readonly OpenAiOptions _options;
    private readonly VideoGenerator _videoGenerator;
    private readonly MusicGenerator _musicGenerator;

    public DialogRunner(IOptions<OpenAiOptions> options, ILogger<DialogRunner> logger, BillingApiClient billingApiClient, VideoGenerator videoGenerator, MusicGenerator musicGenerator)
    {
        _logger = logger;
        _billingApiClient = billingApiClient;
        _options = options.Value;
        _client = new(new(options.Value.OpenAiApiKey));
        _videoGenerator = videoGenerator;
        _musicGenerator = musicGenerator;
    }

    public async Task<(DialogResult result, ChatBotMemoryPointWithTraces newMessagePoint)> Execute(
        IReadOnlyList<MemoryPoint> existingMemory, DialogConfiguration configuration, CancellationToken cancellationToken, DateTime? imagesUnavailableUntil, DateTime? videosUnavailableUntil, DateTime? musicUnavailableUntil, ValueHolder<MediaAction> mediaAction)
    {
        var lastTopicChange = existingMemory.WithIndices().LastOrDefault(x => x.x is ChatBotMemoryPoint
        {
            IsTopicChangeDetected: true
        });

        if (lastTopicChange.x != null)
        {
            var index = lastTopicChange.i;
            index -= 3;
            if (index > 0)
            {
                existingMemory = existingMemory.Skip(index).ToList();
            }
        }

        var newMessagePoint = new ChatBotMemoryPointWithTraces();

        var messages = RecreateMessages(existingMemory, configuration, false);

        if (cancellationToken.IsCancellationRequested) throw new TaskCanceledException();

        var topicChangeDetection = messages.Count(x => x.Role == Role.User) > 1;
        Message? message;
        if (topicChangeDetection)
        {
            (message, newMessagePoint.IsTopicChangeDetected) = await RequestWithTopicChangeDetection(messages, newMessagePoint, configuration, cancellationToken);

            if (cancellationToken.IsCancellationRequested) throw new TaskCanceledException();

            if (newMessagePoint.IsTopicChangeDetected)
            {
                messages = RecreateMessages(existingMemory, configuration, true);
                (message, _) = await Request(messages, configuration, cancellationToken, "after topic change", newMessagePoint);
            }
        }
        else
        {
            (message, _) = await Request(messages, configuration, cancellationToken, "topic change detection off", newMessagePoint);
        }

        messages.Add(message);
        newMessagePoint.Messages.Add(message);

        // Process all media tools
        var (images, videos, music, newMessages) = LookForMediaTools(message, imagesUnavailableUntil, videosUnavailableUntil, musicUnavailableUntil, newMessagePoint);
        newMessagePoint.Messages.AddRange(newMessages);
        messages.AddRange(newMessages);

        // Process images
        List<string> imagesDeserialized = new();
        Task<List<string?>>? imageUrlsTask = null;
        if (images.Any())
        {
            imagesDeserialized = DeserializeImages(images);
            newMessagePoint.ImageTraces.AddRange(imagesDeserialized);

            if (imagesDeserialized.Any() && !imagesUnavailableUntil.HasValue)
            {
                mediaAction.Value = MediaAction.UploadPhoto;
                imageUrlsTask = GenerateImages(imagesDeserialized, configuration, cancellationToken);
            }
        }

        // Process videos (up to 3)
        List<(VideoArguments args, Task<byte[]?> task)> videoTasks = new();
        List<VideoLimitResult> videoLimits = new();
        if (videos.Any())
        {
            foreach (var video in videos.Take(3))
            {
                var videoDeserialized = DeserializeVideo(video);
                if (videoDeserialized?.Prompt != null)
                {
                    newMessagePoint.VideoTraces.Add(videoDeserialized.Prompt);

                    if (!videosUnavailableUntil.HasValue)
                    {
                        mediaAction.Value = MediaAction.RecordVideo;
                        var task = GenerateVideo(videoDeserialized, configuration, cancellationToken);
                        videoTasks.Add((videoDeserialized, task));
                    }
                    else
                    {
                        videoLimits.Add(new(videoDeserialized.Prompt));
                    }
                }
            }
        }

        // Process music (up to 5)
        List<(MusicArguments args, Task<byte[]?> task)> musicTasks = new();
        List<MusicLimitResult> musicLimits = new();
        if (music.Any())
        {
            foreach (var musicItem in music.Take(5))
            {
                var musicDeserialized = DeserializeMusic(musicItem);
                if (musicDeserialized?.Prompt != null)
                {
                    newMessagePoint.MusicTraces.Add(musicDeserialized.Prompt);

                    if (!musicUnavailableUntil.HasValue)
                    {
                        mediaAction.Value = MediaAction.RecordVoice;
                        var task = GenerateMusicSingle(musicDeserialized, configuration, cancellationToken);
                        musicTasks.Add((musicDeserialized, task));
                    }
                    else
                    {
                        musicLimits.Add(new(musicDeserialized.Prompt));
                    }
                }
            }
        }

        // If no content in message after media tools, request more content
        if (string.IsNullOrWhiteSpace(message?.Content?.ToString()) && (images.Any() || videos.Any() || music.Any()))
        {
            (message, _) = await Request(messages, configuration, cancellationToken, "after media tools detected", newMessagePoint, false);
            var (moreImages, moreVideos, moreMusic, _) = LookForMediaTools(message, imagesUnavailableUntil, videosUnavailableUntil, musicUnavailableUntil, newMessagePoint);
            if (moreImages.Any() || moreVideos.Any() || moreMusic.Any())
            {
                _logger.LogError("More media tools returned. Could not have happened. {message}",
                    JsonSerializer.Serialize(message));
                message = null;
            }
            else
            {
                newMessagePoint.Messages.Add(message);
            }
        }

        // Await media generation tasks (don't cancel - expensive operations should complete)
        if (imageUrlsTask != null)
        {
            try
            {
                imagesDeserialized = (await imageUrlsTask).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()!;
                newMessagePoint.ImageUrlTraces.AddRange(imagesDeserialized);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error completing image generation");
            }
        }

        List<VideoResult> videoResults = new();
        foreach (var videoTask in videoTasks)
        {
            try
            {
                var videoData = await videoTask.task;
                if (videoData is { Length: > 0 })
                {
                    var prompt = videoTask.args.Prompt ?? "video";
                    var duration = videoTask.args.Duration;
                    videoResults.Add(new(videoData, prompt, duration));
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error completing video generation");
            }
        }

        List<MusicResult> musicResults = new();
        foreach (var musicTask in musicTasks)
        {
            try
            {
                var musicData = await musicTask.task;
                if (musicData is { Length: > 0 })
                {
                    var prompt = musicTask.args.Prompt ?? "music";
                    var title = musicTask.args.Title;
                    musicResults.Add(new(musicData, prompt, title));
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error completing music generation");
            }
        }

        return (new(
                ExtractContent(message),
                imagesDeserialized,
                videoResults,
                musicResults,
                videoLimits,
                musicLimits,
                newMessagePoint.IsTopicChangeDetected),
            newMessagePoint);
    }

    public async Task<(List<List<string>> prompts, SongImagesMemoryPoint memoryPoint)> GenerateSongImages(string chords,
        string? systemMessage,
        long? userId,
        string? additionalBillingInfo,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<Message>
        {
            new(Role.System, DialogConstants.SystemImageOpportunityMessage),
            new(Role.System, systemMessage),
            new(Role.User, chords)
        };

        var dialogConfiguration = new DialogConfiguration
        {
            Version = "gpt-4-1106-preview",
            ImagesVersion = 3,
            SystemMessage = null!,
            UserId = userId,
            UseCase = "song images",
            AdditionalBillingInfo = additionalBillingInfo,
            DomainId = null,
            ChatId = null,
        };

        var (responses, trace) = await RequestMany(messages, dialogConfiguration, cancellationToken, "for images", number: 3);

        var prompts = responses.Select(r =>
        {
            var (images, _) = LookForImages(r, null);
            if (!images.Any())
            {
                return new();
            }

            return DeserializeImages(images);
        }).ToList();

        var memoryPoint = new SongImagesMemoryPoint
        {
            Trace = trace,
            ImageTraces = prompts,
        };

        return (prompts, memoryPoint);
    }

    private static string ExtractContent(Message? message)
    {
        return Regex.Replace(message?.Content?.ToString(), "\\!\\[(?<name>[^\\r\\n]+)\\]\\([^\\r\\n]+\\)",
            "**🖼 ${name}**");
    }

    private static List<Message> RecreateMessages(IReadOnlyList<MemoryPoint> existingMemory, DialogConfiguration configuration, bool lastUserMessageChangesTopic)
    {
        existingMemory = existingMemory.SkipWhile(x => x is ChatBotMemoryPoint).ToList();

        if (lastUserMessageChangesTopic)
        {
            var preLastChatBotEndpointIndex = existingMemory.WithIndicesNullable().Reverse().Where(x => x.x is ChatBotMemoryPoint).Skip(1).FirstOrDefault().i;
            if (preLastChatBotEndpointIndex.HasValue)
            {
                existingMemory = existingMemory.Skip(preLastChatBotEndpointIndex.Value + 1).ToList();
                existingMemory = existingMemory.SkipWhile(x => x is ChatBotMemoryPoint).ToList();
            }
        }

        var messages = new List<Message>
        {
            new(Role.System, DialogConstants.SystemImageOpportunityMessage),
            new(Role.System, DialogConstants.SystemVideoOpportunityMessage),
            new(Role.System, DialogConstants.SystemMusicOpportunityMessage),
            new(Role.System, configuration.SystemMessage),
        };

        foreach (var (memoryPoint, i) in existingMemory.WithIndices())
        {
            switch (memoryPoint)
            {
                case UserMessageMemoryPoint userMessageMemoryPoint:
                    messages.Add(new(Role.User, userMessageMemoryPoint.Message));
                    break;
                case ChatBotMemoryPoint chatBotMemoryPoint:
                    messages.AddRange(chatBotMemoryPoint.Messages);
                    break;
                case UserImageMessageMemoryPoint userImageMessageMemoryPoint:
                    var imageDetail = existingMemory.Skip(i).SkipWhile(x => x is not ChatBotMemoryPoint).Any(x => x is UserImageMessageMemoryPoint)
                        ? ImageDetail.Low
                        : ImageDetail.High;

                    messages.Add(new(Role.User, [
                        new ImageUrl(
                            $"data:image/jpeg;base64,{userImageMessageMemoryPoint.Base64}",
                            imageDetail)
                    ]));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(memoryPoint));
            }
        }

        return messages;
    }

    public async Task<List<string?>> GenerateImages(IReadOnlyList<string> prompts, DialogConfiguration configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = prompts
                .WithIndices()
                .Select(x => (task: GenerateImage(x.x, configuration, cancellationToken), x))
                .ToList();

            try
            {
                await Task.WhenAll(tasks.Select(x => x.task));
            }
            catch when (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            return tasks.Select(x => x.task.Result).Select(x => string.IsNullOrWhiteSpace(x) ? null : x).ToList();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error generating the images. {prompts}", prompts);
            return Enumerable.Repeat((string?)null, prompts.Count).ToList();
        }
    }

    private async Task<string?> GenerateImage(string prompt, DialogConfiguration configuration, CancellationToken cancellationToken)
    {
        try
        {
            var started = DateTime.Now;
            var request = configuration.ImagesVersion switch
            {
                2 or null => new ImageGenerationRequest(prompt, Model.DallE_2, size: "512x512"),
                3 => new(prompt, Model.DallE_3, size: "1024x1024"),
                _ => throw new ArgumentOutOfRangeException(nameof(configuration.ImagesVersion)),
            };

            var result = await _client.ImagesEndPoint.GenerateImageAsync(request, cancellationToken);

            _logger.LogInformation("Images generated. Took {ms} ms. Prompt: {prompt}.", DateTime.Now - started, prompt);

            await _billingApiClient.Add(new()
            {
                Count = 1,
                UserId = configuration.UserId,
                Model = request.Model,
                UseCase = configuration.UseCase,
                AdditionalInfo = configuration.AdditionalBillingInfo,
                DomainId = configuration.DomainId,
                ChatId = configuration.ChatId,
            });

            return result.Single();
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error generating the image. {prompt}", prompt);
            return null;
        }
    }

    private async Task<byte[]?> GenerateVideo(VideoArguments arguments, DialogConfiguration configuration, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(arguments.Prompt)) return null;

            var (width, height) = arguments.Ratio switch
            {
                "16:9" => (854, 480),
                "9:16" => (480, 854),
                "Square" => (480, 480),
                _ => (480, 480)
            };

            var request = new VideoGenerationRequest
            {
                Prompt = arguments.Prompt,
                Width = width,
                Height = height,
                Duration = arguments.Duration,
                UserId = configuration.UserId,
                DomainId = configuration.DomainId,
                ChatId = configuration.ChatId,
                UseCase = configuration.UseCase,
                AdditionalBillingInfo = configuration.AdditionalBillingInfo,
                Model = configuration.VideoModel ?? "sora-turbo"
            };

            var result = await _videoGenerator.GenerateVideo(request, cancellationToken);
            if (result is { Success: true, VideoData.Length: > 0 })
            {
                _logger.LogInformation("Video generated successfully. Prompt: {prompt}", arguments.Prompt);
                return result.VideoData;
            }
            else
            {
                _logger.LogWarning("Video generation failed. Prompt: {prompt}, Error: {error}", arguments.Prompt, result.ErrorMessage);
                return null;
            }
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error generating video. Prompt: {prompt}", arguments.Prompt);
            return null;
        }
    }

    private async Task<byte[]?> GenerateMusicSingle(MusicArguments arguments, DialogConfiguration configuration, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(arguments.Prompt)) return null;

            var request = new MusicGenerationRequest
            {
                Prompt = arguments.Prompt,
                NegativePrompt = arguments.NegativePrompt,
                UserId = configuration.UserId,
                DomainId = configuration.DomainId,
                ChatId = configuration.ChatId,
                UseCase = configuration.UseCase,
                AdditionalBillingInfo = configuration.AdditionalBillingInfo,
                Model = configuration.MusicModel ?? "lyria"
            };

            var result = await _musicGenerator.GenerateMusic(request, cancellationToken);
            if (result is { Success: true, AudioData.Length: > 0 })
            {
                _logger.LogInformation("Music generated successfully. Prompt: {prompt}", arguments.Prompt);
                return result.AudioData;
            }
            else
            {
                _logger.LogWarning("Music generation failed. Prompt: {prompt}, Error: {error}", arguments.Prompt, result.ErrorMessage);
                return null;
            }
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error generating music. Prompt: {prompt}", arguments.Prompt);
            return null;
        }
    }

    private List<string> DeserializeImages(List<JsonNode> images)
    {
        var imagesDeserialized = new List<string>();
        foreach (var jsonNode in images)
        {
            try
            {
                var arguments = JsonSerializer.Deserialize<ImageArguments>(jsonNode.ToString());
                imagesDeserialized.AddRange(
                    arguments!.ImagePrompts!.Select(x => x ?? throw new ArgumentOutOfRangeException(nameof(x))));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to deserialize images: {obj}", jsonNode?.ToString());
            }
        }

        return imagesDeserialized;
    }

    private VideoArguments? DeserializeVideo(JsonNode video)
    {
        try
        {
            return JsonSerializer.Deserialize<VideoArguments>(video.ToString());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to deserialize video: {obj}", video?.ToString());
            return null;
        }
    }

    private MusicArguments? DeserializeMusic(JsonNode music)
    {
        try
        {
            return JsonSerializer.Deserialize<MusicArguments>(music.ToString());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to deserialize music: {obj}", music?.ToString());
            return null;
        }
    }

    private (List<JsonNode> images, List<JsonNode> videos, List<JsonNode> music, List<Message> newMessages) LookForMediaTools(Message message, DateTime? imagesUnavailableUntil, DateTime? videosUnavailableUntil, DateTime? musicUnavailableUntil, ChatBotMemoryPointWithTraces newMessagePoint)
    {
        var messages = new List<Message>();
        var images = new List<JsonNode>();
        var videos = new List<JsonNode>();
        var music = new List<JsonNode>();
        var imagesFound = false;
        var videosFound = false;
        var musicFound = false;

        // Count recent tool calls from traces
        var recentVideoCallCount = CountRecentToolCalls(newMessagePoint, DialogConstants.IncludeVideoFunctionName);
        var recentMusicCallCount = CountRecentToolCalls(newMessagePoint, DialogConstants.IncludeMusicFunctionName);

        foreach (var tool in message.ToolCalls ?? Enumerable.Empty<Tool>())
        {
            switch (tool.Function.Name)
            {
                case DialogConstants.IncludeImageFunctionName:
                    if (imagesUnavailableUntil.HasValue)
                    {
                        messages.Add(new(tool, "status = LIMIT EXCEEDED"));
                    }
                    else
                    {
                        messages.Add(new(tool, "status = SUCCESS"));
                    }
                    images.Add(tool.Function.Arguments);
                    imagesFound = true;
                    break;

                case DialogConstants.IncludeVideoFunctionName:
                    if (videosUnavailableUntil.HasValue)
                    {
                        messages.Add(new(tool, "status = LIMIT EXCEEDED"));
                    }
                    else if (recentVideoCallCount >= 2)
                    {
                        messages.Add(new(tool, "status = TOO MANY ATTEMPTS - You have already tried to generate a video 2 times recently. Please wait before trying again."));
                    }
                    else
                    {
                        messages.Add(new(tool, "status = SUCCESS"));
                        videos.Add(tool.Function.Arguments);
                        videosFound = true;
                    }
                    break;

                case DialogConstants.IncludeMusicFunctionName:
                    if (musicUnavailableUntil.HasValue)
                    {
                        messages.Add(new(tool, "status = LIMIT EXCEEDED"));
                    }
                    else if (recentMusicCallCount >= 2)
                    {
                        messages.Add(new(tool, "status = TOO MANY ATTEMPTS - You have already tried to generate music 2 times recently. Please wait before trying again."));
                    }
                    else
                    {
                        messages.Add(new(tool, "status = SUCCESS"));
                        music.Add(tool.Function.Arguments);
                        musicFound = true;
                    }
                    break;

                default:
                    _logger.LogError("unknown function {name}. message: {message}", tool.Function.Name,
                        JsonSerializer.Serialize(message));
                    throw new("Unexpected function call.");
            }
        }

        // Add system messages for each media type found
        if (imagesFound)
        {
            if (imagesUnavailableUntil.HasValue)
            {
                messages.Add(new(Role.System, string.Format(DialogConstants.ImagesLimitMessage, imagesUnavailableUntil.Value.ToString("f"))));
            }
            else
            {
                messages.Add(new(Role.System, DialogConstants.ImagesDisplayedMessage));
            }
        }

        if (videosFound)
        {
            if (videosUnavailableUntil.HasValue)
            {
                messages.Add(new(Role.System, string.Format(DialogConstants.VideosLimitMessage, videosUnavailableUntil.Value.ToString("f"))));
            }
            else
            {
                messages.Add(new(Role.System, DialogConstants.VideosDisplayedMessage));
            }
        }

        if (musicFound)
        {
            if (musicUnavailableUntil.HasValue)
            {
                messages.Add(new(Role.System, string.Format(DialogConstants.MusicLimitMessage, musicUnavailableUntil.Value.ToString("f"))));
            }
            else
            {
                messages.Add(new(Role.System, DialogConstants.MusicDisplayedMessage));
            }
        }

        return (images, videos, music, messages);
    }

    private int CountRecentToolCalls(ChatBotMemoryPointWithTraces memoryPoint, string functionName)
    {
        // Count recent calls from current conversation traces
        var count = 0;
        foreach (var trace in memoryPoint.Traces)
        {
            if (trace.Request?.Tools?.Any(t => 
                t.Function?.Name == functionName) == true)
            {
                // Check if the response had this tool call
                var message = trace.Response?.Choices?.FirstOrDefault()?.Message;
                if (message?.ToolCalls?.Any(tc => tc.Function?.Name == functionName) == true)
                {
                    count++;
                }
            }
        }
        
        // Also count from video/music traces
        if (functionName == DialogConstants.IncludeVideoFunctionName)
        {
            count += memoryPoint.VideoTraces.Count;
        }
        else if (functionName == DialogConstants.IncludeMusicFunctionName)
        {
            count += memoryPoint.MusicTraces.Count;
        }
        
        return count;
    }

    private (List<JsonNode> images, List<Message> newMessages) LookForImages(Message message, DateTime? imagesUnavailableUntil)
    {
        var emptyMemoryPoint = new ChatBotMemoryPointWithTraces();
        var (images, _, _, newMessages) = LookForMediaTools(message, imagesUnavailableUntil, null, null, emptyMemoryPoint);
        return (images, newMessages);
    }

    private async Task<(Message message, MemoryPointTrace trace)> Request(IReadOnlyList<Message> messages,
        DialogConfiguration configuration, CancellationToken cancellationToken, string additionalInfo, ChatBotMemoryPointWithTraces? memoryPoint = null, bool allowImages = true)
    {
        var results = await RequestMany(messages, configuration, cancellationToken, additionalInfo, memoryPoint, allowImages, 1);
        return (results.messages.Single(), results.trace);
    }

    private async Task<(List<Message> messages, MemoryPointTrace trace)> RequestMany(IReadOnlyList<Message> messages, DialogConfiguration configuration, CancellationToken cancellationToken, string additionalInfo, ChatBotMemoryPointWithTraces? memoryPoint = null, bool allowImages = true, int number = 1)
    {
        var started = DateTime.Now;
        var chatRequest = new ChatRequest(messages, DialogConstants.First, number: number, toolChoice: allowImages ? "auto" : "none", model: configuration.Version, frequencyPenalty: configuration.FrequencyPenalty, presencePenalty: configuration.PresencePenalty);
        var response = await _client.ChatEndpoint.GetCompletionAsync(chatRequest, cancellationToken);
        _logger.LogInformation("ChatGPT model {version} request took {time} ms, cost: {cost}.", configuration.Version, (DateTime.Now - started).TotalMilliseconds, JsonSerializer.Serialize(response.Usage));

        await _billingApiClient.Add(new()
        {
            Count = 1,
            UserId = configuration.UserId,
            Model = configuration.Version,
            UseCase = configuration.UseCase,
            InputTokens = response.Usage.PromptTokens,
            OutputTokens = response.Usage.CompletionTokens,
            AdditionalInfo = $"completions count = {number}; {configuration.AdditionalBillingInfo}; {additionalInfo}",
            DomainId = configuration.DomainId,
            ChatId = configuration.ChatId,
        });

        var memoryPointTrace = new MemoryPointTrace(chatRequest, response);
        memoryPoint?.Traces.Add(memoryPointTrace);

        return (response.Choices.Select(x => x.Message).ToList(), memoryPointTrace);
    }

    private async Task<(Message message, bool isTopicChanged)> RequestWithTopicChangeDetection(IReadOnlyList<Message> messages, ChatBotMemoryPointWithTraces memoryPoint, DialogConfiguration configuration, CancellationToken cancellationToken)
    {
        var started = DateTime.Now;
        var newMessages = messages.ToList();
        newMessages.Insert(messages.Count - 1, new(Role.System, DialogConstants.TopicChangeDetectionMessage));

        var chatRequest = new ChatRequest(newMessages, DialogConstants.ChangeTopicAvailable, model: configuration.Version, frequencyPenalty: configuration.FrequencyPenalty, presencePenalty: configuration.PresencePenalty);
        var response = await _client.ChatEndpoint.GetCompletionAsync(chatRequest, cancellationToken);

        await _billingApiClient.Add(new()
        {
            Count = 1,
            UserId = configuration.UserId,
            Model = configuration.Version,
            UseCase = configuration.UseCase,
            InputTokens = response.Usage.PromptTokens,
            OutputTokens = response.Usage.CompletionTokens,
            AdditionalInfo = $"with topic change detection; {configuration.AdditionalBillingInfo}",
            DomainId = configuration.DomainId,
            ChatId = configuration.ChatId,
        });

        memoryPoint.Traces.Add(new(chatRequest, response));
        _logger.LogInformation("ChatGPT model {version} request took {time} ms, cost: {cost}.", configuration.Version, (DateTime.Now - started).TotalMilliseconds, JsonSerializer.Serialize(response.Usage));
        var message = response.FirstChoice.Message;

        var isTopicChanged = IsTopicChanged(message);
        return (message, isTopicChanged);
    }

    private bool IsTopicChanged(Message message)
    {
        // Valid function names that are NOT topic changes
        var validMediaFunctions = new[]
        {
            DialogConstants.IncludeImageFunctionName,
            DialogConstants.IncludeVideoFunctionName,
            DialogConstants.IncludeMusicFunctionName
        };

        if (message.ToolCalls?.Any(x => x.Function?.Name == DialogConstants.UserChangedTopicFunctionName || 
                                      (x.Function?.Name != null && !validMediaFunctions.Contains(x.Function.Name))) == true)
        {
            if (message.ToolCalls.Count > 1 || (object)message.Content != null || message.ToolCalls.Any(x => x.Function?.Name != DialogConstants.UserChangedTopicFunctionName))
            {
                _logger.LogError("multiple tool calls or non-empty content or weird function name. topic change detected. message: {message}",
                    JsonSerializer.Serialize(message));
            }

            return true;
        }

        return false;
    }
}