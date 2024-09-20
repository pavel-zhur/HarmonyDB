using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using OneShelf.Videos.Database.Models;
using OneShelf.Videos.Database.Models.Json;

namespace OneShelf.Videos.Database;

public class VideosDatabase : DbContext
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public VideosDatabase(DbContextOptions<VideosDatabase> options)
        : base(options)
    {
    }

    public required DbSet<UploadedItem> UploadedItems { get; set; }
    public required DbSet<ChatFolder> ChatFolders { get; set; }
    public required DbSet<Chat> Chats { get; set; }
    public required DbSet<Message> Messages { get; set; }
    public required DbSet<InventoryItem> InventoryItems { get; set; }
    public required DbSet<Topic> Topics { get; set; }
    public required DbSet<Album> Albums { get; set; }
    public required DbSet<AlbumConstraint> AlbumConstraints { get; set; }
    public required DbSet<UploadedAlbum> UploadedAlbums { get; set; }
    public required DbSet<LiveChat> LiveChats { get; set; }
    public required DbSet<LiveTopic> LiveTopics { get; set; }
    public required DbSet<LiveMedia> LiveMediae { get; set; }
    public required DbSet<DownloadedItem> DownloadedItems { get; set; }

    public async Task CreateMissingTopics()
    {
        await Database.ExecuteSqlAsync(@$"

with m as (
	select *
	from messages m
	where selectedtype is not null
), r as (
	select chatid, id childid, replytomessageid parentid, 1 as level, 0 as isfinal
	from m
	where replytomessageid is not null
	union all
	select p.chatid, r.childid, isnull(p.replytomessageid, p.id) parentid, level + 1 as level, case when p.replytomessageid is null then 1 else 0 end isfinal
	from messages p
	inner join r on r.parentid = p.id and r.chatid = p.chatid
	where r.isfinal = 0
), roots as (
	select r.*, p.title
	from r
	inner join messages p on r.parentid = p.id and r.chatid = p.chatid
	where isfinal = 1 and action = 'topic_created'
), newtopics as (
	select *
	from (
		select 
			-- c.*, r.*, m.*
			c.name, r.title, m.chatid, isnull(r.parentid, 0) rootmessageidor0
		from m
		left join roots r on r.childid = m.id and r.chatid = m.chatid
		inner join chats c on c.id = m.chatid
		group by c.name, r.title, m.chatid, r.parentid
	) x
)

insert into topics (chatid, rootmessageidor0, originaltitle, title)
select nt.chatid, nt.rootmessageidor0, case when nt.title is null then nt.name else nt.name + ' / ' + nt.title end, case when nt.title is null then nt.name else nt.name + ' / ' + nt.title end
from newtopics nt
left join topics t on t.chatid = nt.chatid and nt.rootmessageidor0 = t.rootmessageidor0
where t.id is null
ORDER BY nt.chatid, nt.rootmessageidor0

");
    }

    public async Task UpdateMessagesTopics()
    {
        await Database.ExecuteSqlAsync(@$"

with m as (
	select *
	from messages m
	where selectedtype is not null
), r as (
	select chatid, id childid, replytomessageid parentid, 1 as level, 0 as isfinal
	from m
	where replytomessageid is not null
	union all
	select p.chatid, r.childid, isnull(p.replytomessageid, p.id) parentid, level + 1 as level, case when p.replytomessageid is null then 1 else 0 end isfinal
	from messages p
	inner join r on r.parentid = p.id and r.chatid = p.chatid
	where r.isfinal = 0
), roots as (
	select r.*, p.title
	from r
	inner join messages p on r.parentid = p.id and r.chatid = p.chatid
	where isfinal = 1 and action = 'topic_created'
)

update messages
set topicid = t.id
from messages m
inner join m mm on mm.DatabaseMessageId = m.DatabaseMessageId
left join roots r on r.chatid = m.chatid and r.childid = m.id
left join topics t on t.chatid = m.chatid and t.rootmessageidor0 = isnull(r.parentid, 0)

");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Message>()
            .Property(x => x.ContactInformation)
            .HasConversion<string?>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<Message>()
            .Property(x => x.InlineBotButtons)
            .HasConversion<string?>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<Message>()
            .Property(x => x.LocationInformation)
            .HasConversion<string?>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<Message>()
            .Property(x => x.Poll)
            .HasConversion<string?>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<Message>()
            .Property(x => x.Text)
            .HasConversion<string>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<Message>()
            .Property(x => x.TextEntities)
            .HasConversion<string>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<AlbumConstraint>()
            .Property(x => x.MessageSelectedType)
            .HasConversion<string>();

        modelBuilder.Entity<Message>()
            .Property(x => x.SelectedType)
            .HasConversion<string>()
            .HasComputedColumnSql(
                "case when photo is not null then 'photo' when mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null') then 'video' else null end");

        modelBuilder.Entity<LiveMedia>()
            .Property(x => x.Type)
            .HasConversion<string>();
    }
}