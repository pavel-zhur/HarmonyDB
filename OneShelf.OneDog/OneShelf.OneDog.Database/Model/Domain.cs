using Microsoft.EntityFrameworkCore;

namespace OneShelf.OneDog.Database.Model;

[Index(nameof(WebHooksSecretToken))]
public class Domain
{
    public int Id { get; set; }

    public long ChatId { get; init; }

    public int TopicId { get; init; }

    public required ICollection<User> Administrators { get; init; }

    public required ICollection<Chat> Chats { get; init; }

    public string? PrivateDescription { get; init; }

    public required string SystemMessage { get; set; }
    
    public required string GptVersion { get; set; }
    
    public required int DalleVersion { get; set; }

    public float? FrequencyPenalty { get; set; }
    
    public float? PresencePenalty { get; set; }

    public required string BotToken { get; set; }

    public string? WebHooksSecretToken { get; set; }

    public required bool IsEnabled { get; set; }

    public float? BillingRatio { get; set; }
}