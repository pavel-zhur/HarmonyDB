using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace OneShelf.Videos.Database.Models.Static;

[PrimaryKey(nameof(StaticChatId), nameof(Id))]
public class StaticMessage
{
    [JsonIgnore]
    public long StaticChatId { get; set; }

    [JsonIgnore]
    public StaticChat StaticChat { get; set; } = null!;

    [JsonIgnore]
    public int? StaticTopicId { get; set; }

    [JsonIgnore]
    public StaticTopic? StaticTopic { get; set; }

    [JsonIgnore]
    public StaticMessageSelectedType? SelectedType { get; set; }

    //[JsonIgnore]
    //public Media? Media { get; set; }

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
    public required JsonElement Text { get; set; }
    public List<string>? Members { get; set; }
    public required JsonElement TextEntities { get; set; }
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
    public JsonElement? Poll { get; set; }
    public string? Performer { get; set; }
    public JsonElement? LocationInformation { get; set; }
    public JsonElement? ContactInformation { get; set; }
    public string? SavedFrom { get; set; }
    public string? ViaBot { get; set; }
    public long? NewIconEmojiId { get; set; }
    public JsonElement? InlineBotButtons { get; set; }
    public int? ScheduleDate { get; set; }
    public int? Duration { get; set; }
    public string? NewTitle { get; set; }
    public string? ReplyToPeerId { get; set; }
    public int? Boosts { get; set; }
    public int? LiveLocationPeriodSeconds { get; set; }
}