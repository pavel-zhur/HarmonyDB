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
}