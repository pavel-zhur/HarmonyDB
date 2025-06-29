using System.Text.Json.Nodes;
using OpenAI;

namespace OneShelf.Common.OpenAi.Internal;

internal static class DialogConstants
{
    public const string UserChangedTopicFunctionName = "user_changed_topic";
    public const string IncludeImageFunctionName = "include_image";
    public const string IncludeVideoFunctionName = "include_video";
    public const string IncludeMusicFunctionName = "include_music";

    public const string ImagesDisplayedMessage = "The images have been displayed.";
    public const string ImagesLimitMessage = "The images generation limit is exceeded until {0}.";
    public const string VideosDisplayedMessage = "The video has been generated and sent.";
    public const string VideosLimitMessage = "The video generation limit is exceeded until {0}.";
    public const string MusicDisplayedMessage = "The music has been generated and sent.";
    public const string MusicLimitMessage = "The music generation limit is exceeded until {0}.";
    public const string TopicChangeDetectionMessage =
        $"If the next user message has no relation to the previous conversation, immediately call the '{UserChangedTopicFunctionName}' function.";

    public const string SystemImageOpportunityMessage =
        $"If you wish to display the images to the user, call the '{IncludeImageFunctionName}' function.";
    public const string SystemVideoOpportunityMessage =
        $"If you wish to generate a video for the user, call the '{IncludeVideoFunctionName}' function. Always confirm the details (prompt, ratio, duration) before generating. 5 seconds is probably the best default duration. If generation fails, you can try again but you are limited to 2 consecutive attempts - then you must wait.";
    public const string SystemMusicOpportunityMessage =
        $"If you wish to generate music for the user, call the '{IncludeMusicFunctionName}' function. Music prompts must be in English. If generation fails, you can try again but you are limited to 2 consecutive attempts - then you must wait.";

    private static readonly Tool UserChangedTopicTool = new(new Function(
        UserChangedTopicFunctionName,
        "Call this function whenever the next user message has no relation to the previous conversation, i.e. it feels like they start the conversation anew.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(),
        }));

    private static readonly Tool IncludeImageTool = new(new Function(
        IncludeImageFunctionName,
        "Call this function if you wish to display pictures or images to the user.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["image_prompts"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] =
                        "Images prompts. The function will call the DALL-E model to generate those images for each of the array elements that you provide, and the user will see them with your response",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "string"
                    }
                }
            },
        }));

    private static readonly Tool IncludeVideoTool = new(new Function(
        IncludeVideoFunctionName,
        "Call this function if you wish to generate a video for the user. Always confirm details with user first in a brief message asking about prompt, ratio, and duration. Limited to 2 consecutive attempts.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["prompt"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Video generation prompt describing what the video should show"
                },
                ["ratio"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray { "16:9", "9:16", "Square" },
                    ["description"] = "Video aspect ratio: 16:9 (landscape), 9:16 (portrait), or Square"
                },
                ["duration"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["minimum"] = 1,
                    ["maximum"] = 20,
                    ["description"] = "Video duration in seconds (1-20). 5 seconds is recommended as the default."
                }
            },
            ["required"] = new JsonArray { "prompt", "ratio", "duration" }
        }));

    private static readonly Tool IncludeMusicTool = new(new Function(
        IncludeMusicFunctionName,
        "Call this function if you wish to generate music for the user. Music prompts must be in English. Limited to 2 consecutive attempts.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["prompt"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Music generation prompt in English describing the style, mood, or characteristics of the music"
                },
                ["negative_prompt"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional negative prompt in English describing what should NOT be in the music"
                },
                ["title"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Creative filename for the music file in English. Make it creative, unusual, interesting, can be playful. Should end with .wav extension"
                }
            },
            ["required"] = new JsonArray { "prompt", "title" }
        }));

    public static IReadOnlyList<Tool> First { get; } = new List<Tool>
    {
        IncludeImageTool,
        IncludeVideoTool,
        IncludeMusicTool,
    };

    public static IReadOnlyList<Tool> ChangeTopicAvailable { get; } = new List<Tool>
    {
        UserChangedTopicTool,
        IncludeImageTool,
        IncludeVideoTool,
        IncludeMusicTool,
    };
}