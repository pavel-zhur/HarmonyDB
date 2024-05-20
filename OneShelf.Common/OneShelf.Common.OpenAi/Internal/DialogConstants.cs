using System.Text.Json.Nodes;
using OpenAI;

namespace OneShelf.Common.OpenAi.Internal;

internal static class DialogConstants
{
    public const string UserChangedTopicFunctionName = "user_changed_topic";
    public const string IncludeImageFunctionName = "include_image";

    public const string ImagesDisplayedMessage = "The images have been displayed.";
    public const string TopicChangeDetectionMessage =
        $"If the next user message has no relation to the previous conversation, immediately call the '{UserChangedTopicFunctionName}' function.";

    public const string SystemImageOpportunityMessage =
        $"If you wish to display the images to the user, call the '{IncludeImageFunctionName}' function.";

    private static readonly Tool UserChangedTopicTool = new(new(
        UserChangedTopicFunctionName,
        "Call this function whenever the next user message has no relation to the previous conversation, i.e. it feels like they start the conversation anew.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(),
        }));

    private static readonly Tool IncludeImageTool = new(new(
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

    public static IReadOnlyList<Tool> First { get; } = new List<Tool>
    {
        IncludeImageTool,
    };

    public static IReadOnlyList<Tool> ChangeTopicAvailable { get; } = new List<Tool>
    {
        UserChangedTopicTool,
        IncludeImageTool,
    };
}