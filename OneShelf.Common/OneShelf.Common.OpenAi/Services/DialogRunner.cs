using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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

    public DialogRunner(IOptions<OpenAiOptions> options, ILogger<DialogRunner> logger, BillingApiClient billingApiClient)
    {
        _logger = logger;
        _billingApiClient = billingApiClient;
        _options = options.Value;
        _client = new(new(options.Value.OpenAiApiKey));
    }

    public async Task<(DialogResult result, ChatBotMemoryPointWithTraces newMessagePoint)> Execute(
        IReadOnlyList<MemoryPoint> existingMemory, DialogConfiguration configuration, CancellationToken cancellationToken = default, DateTime? imagesUnavailableUntil = null)
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

        var (images, newMessages) = LookForImages(message, imagesUnavailableUntil);
        newMessagePoint.Messages.AddRange(newMessages);
        messages.AddRange(newMessages);

        List<string> imagesDeserialized = new();
        Task<List<string?>>? urlsTask = null;
        if (images.Any())
        {
            imagesDeserialized = DeserializeImages(images);
            newMessagePoint.ImageTraces.AddRange(imagesDeserialized);

            if (cancellationToken.IsCancellationRequested) throw new TaskCanceledException();
            if (imagesDeserialized.Any() && !imagesUnavailableUntil.HasValue)
            {
                urlsTask = GenerateImages(imagesDeserialized, configuration, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested) throw new TaskCanceledException();

            if (string.IsNullOrWhiteSpace(message?.Content?.ToString()))
            {
                (message, _) = await Request(messages, configuration, cancellationToken, "after images detected" + (imagesUnavailableUntil.HasValue ? " but unavailable" : null), newMessagePoint, false);
                if (LookForImages(message, imagesUnavailableUntil).images.Any())
                {
                    _logger.LogError("More images returned. Could not have happened. {message}",
                        JsonSerializer.Serialize(message));
                    message = null;
                }
                else
                {
                    newMessagePoint.Messages.Add(message);
                }
            }
        }

        if (urlsTask != null)
        {
            imagesDeserialized = (await urlsTask).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()!;
            newMessagePoint.ImageUrlTraces.AddRange(imagesDeserialized);
        }

        return (new(
                ExtractContent(message),
                imagesDeserialized,
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
            new(Role.System, configuration.SystemMessage),
        };

        foreach (var memoryPoint in existingMemory)
        {
            switch (memoryPoint)
            {
                case UserMessageMemoryPoint userMessageMemoryPoint:
                    messages.Add(new(Role.User, userMessageMemoryPoint.Message));
                    break;
                case ChatBotMemoryPoint chatBotMemoryPoint:
                    messages.AddRange(chatBotMemoryPoint.Messages);
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

    private (List<JsonNode> images, List<Message> newMessages) LookForImages(Message message, DateTime? imagesUnavailableUntil)
    {
        var messages = new List<Message>();
        var images = new List<JsonNode>();
        var any = false;
        foreach (var tool in message.ToolCalls ?? Enumerable.Empty<Tool>())
        {
            if (tool.Function.Name != DialogConstants.IncludeImageFunctionName)
            {
                _logger.LogError("unknown function {name}. message: {message}", tool.Function.Name,
                    JsonSerializer.Serialize(message));
                throw new("Unexpected function call.");
            }

            if (imagesUnavailableUntil.HasValue)
            {
                messages.Add(new(tool, "status = LIMIT EXCEEDED"));
            }
            else
            {
                messages.Add(new(tool, "status = SUCCESS"));
            }

            images.Add(tool.Function.Arguments);
            any = true;
        }

        if (any)
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

        return (images, messages);
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
        if (message.ToolCalls?.Any(x => x.Function?.Name == DialogConstants.UserChangedTopicFunctionName || x.Function?.Name is not (null or DialogConstants.IncludeImageFunctionName)) == true)
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