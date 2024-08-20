namespace OneShelf.Videos.App.ChatModels;

public class Message
{
    public required int Id { get; set; }
    public int? MessageId { get; set; }
    public int? ReplyToMessageId { get; set; }
    public required string Type { get; set; }
    public string? Actor { get; set; }
    public string? ActorId { get; set; }
    public string? From { get; set; }
    public string? FromId { get; set; }
    public required DateTime Date { get; set; }
    public required string DateUnixtime { get; set; }
    public DateTime Edited { get; set; }
    public string? EditedUnixtime { get; set; }
    public string? Action { get; set; }
    public string? Title { get; set; }
    public string? Inviter { get; set; }
    public required dynamic Text { get; set; }
    public List<string>? Members { get; set; }
    public required List<TextEntity> TextEntities { get; set; }
    public string? Photo { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? File { get; set; }
    public string? FileName { get; set; }
    public string? Thumbnail { get; set; }
    public string? MediaType { get; set; }
    public string? MimeType { get; set; }
    public int? DurationSeconds { get; set; }
    public string? ForwardedFrom { get; set; }
    public string? StickerEmoji { get; set; }
    public dynamic? Poll { get; set; }
    public string? Performer { get; set; }
    public dynamic? LocationInformation { get; set; }
    public dynamic? ContactInformation { get; set; }
    public string? SavedFrom { get; set; }
    public string? ViaBot { get; set; }
    public long? NewIconEmojiId { get; set; }
    public dynamic? InlineBotButtons { get; set; }
    public int? ScheduleDate { get; set; }
    public int? Duration { get; set; }
    public string? NewTitle { get; set; }
    public string? ReplyToPeerId { get; set; }
    public int? Boosts { get; set; }
    public int? LiveLocationPeriodSeconds { get; set; }
}