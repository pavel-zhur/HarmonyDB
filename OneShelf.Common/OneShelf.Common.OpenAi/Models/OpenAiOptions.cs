namespace OneShelf.Common.OpenAi.Models;

public class OpenAiOptions
{
    public required string OpenAiApiKey { get; set; }

    public required string GoogleCloudProjectId { get; set; }

    public required string GoogleCloudLocation { get; set; }

    public required string GoogleServiceAccountJson { get; set; }

    public required string AzureOpenAiEndpoint { get; set; }

    public required string AzureOpenAiApiKey { get; set; }

    public required string AzureOpenAiApiVersion { get; set; }

    public required string GoogleApiKey { get; set; }

    /// <summary>
    /// The chat model that writes the prompts of a song's illustrations (Chat Completions, with function calling and
    /// several choices). Required to make them.
    /// </summary>
    public string? SongImagesPromptsModel { get; set; }

    /// <summary>
    /// The image model that draws a song's illustrations by their prompts (the Images API; its answer is base64).
    /// Required to draw them.
    /// </summary>
    public string? SongImagesModel { get; set; }
}