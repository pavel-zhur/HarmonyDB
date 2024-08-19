namespace OneShelf.Telegram.Ai.Model;

public interface IInteraction<TInteractionType>
{
    int Id { get; }
    TInteractionType InteractionType { get; set; }
    DateTime CreatedOn { get; set; }
    long UserId { get; set; }
    string? ShortInfoSerialized { get; set; }
    string Serialized { get; set; }
}