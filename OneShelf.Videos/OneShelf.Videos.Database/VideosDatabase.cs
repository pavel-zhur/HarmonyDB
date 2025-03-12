using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using OneShelf.Videos.Database.Models;
using OneShelf.Videos.Database.Models.Live;
using OneShelf.Videos.Database.Models.Static;

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

    public required DbSet<StaticChatFolder> StaticChatFolders { get; set; }
    public required DbSet<StaticChat> StaticChats { get; set; }
    public required DbSet<StaticMessage> StaticMessages { get; set; }
    public required DbSet<StaticTopic> StaticTopics { get; set; }

    public required DbSet<LiveChat> LiveChats { get; set; }
    public required DbSet<LiveTopic> LiveTopics { get; set; }
    public required DbSet<LiveMedia> LiveMediae { get; set; }
    public required DbSet<LiveDownloadedItem> LiveDownloadedItems { get; set; }

    public required DbSet<Media> Mediae { get; set; }
    public required DbSet<Topic> Topics { get; set; }

    public required DbSet<UploadedItem> UploadedItems { get; set; }
    public required DbSet<InventoryItem> InventoryItems { get; set; }
    public required DbSet<Album> Albums { get; set; }
    public required DbSet<AlbumConstraint> AlbumConstraints { get; set; }
    public required DbSet<UploadedAlbum> UploadedAlbums { get; set; }
    
    public required DbSet<TelegramUpdate> TelegramUpdates { get; set; }

    public async Task UpdateMediaTopics()
    {
        await Database.ExecuteSqlAsync(@$"

update mediae
set topicid = t.id
from mediae m
left join StaticMessages sm on m.staticmessageid = sm.id and m.staticchatid = sm.StaticChatId
left join livemediae lm on m.livemediaid = lm.id and m.livechatid = lm.livetopiclivechatid and lm.lastinventoryexists=1
left join statictopics st on st.StaticChatId = sm.StaticChatId and st.RootMessageIdOr0 = sm.StaticTopicRootMessageIdOr0
left join livetopics lt on lt.LiveChatId = lm.LiveTopicLiveChatId and lt.id = lm.LiveTopicId
left join topics t on (t.staticchatid = t.StaticChatId and t.StaticTopicRootMessageIdOr0 = st.RootMessageIdOr0)
	or (t.livechatid = lt.LiveChatId and t.livetopicid = lt.Id)

");
    }

    public async Task AppendTopics()
    {
        var staticTopics = await StaticTopics.Where(x => x.Topic == null).ToListAsync();
        Topics.AddRange(staticTopics.Select(x => new Topic
        {
            StaticTopic = x,
        }));

        var liveTopics = await LiveTopics.Where(x => x.Topic == null).ToListAsync();
        Topics.AddRange(liveTopics.Select(x => new Topic
        {
            LiveTopic = x,
        }));

        await SaveChangesAsync();
    }

    public async Task AppendMediae()
    {
        await Database.ExecuteSqlAsync(@$"

insert into mediae (staticchatid, staticmessageid, type)
select sm.staticchatid, sm.id, sm.selectedtype
from StaticMessages sm
left join mediae m on m.staticmessageid = sm.id and m.staticchatid = sm.StaticChatId
where sm.SelectedType is not null and m.id is null

insert into mediae (livechatid, livemediaid, type)
select lm.livetopiclivechatid, lm.id, lm.selectedtype
from livemediae lm
left join mediae m on m.livemediaid = lm.id and m.livechatid = lm.livetopiclivechatid
where lm.SelectedType is not null and m.id is null

");
    }

    public async Task CleanupStaticTopics()
    {
        await Database.ExecuteSqlAsync(@$"

update staticmessages set statictopicid = null
delete from statictopics

");
    }

    public async Task CreateMissingStaticTopics()
    {
        await Database.ExecuteSqlAsync(@$"

with m as (
	select *
	from staticmessages m
	where selectedtype is not null
), r as (
	select staticchatid, id childid, replytomessageid parentid, 1 as level, 0 as isfinal
	from m
	where replytomessageid is not null
	union all
	select p.staticchatid, r.childid, isnull(p.replytomessageid, p.id) parentid, level + 1 as level, case when p.replytomessageid is null then 1 else 0 end isfinal
	from staticmessages p
	inner join r on r.parentid = p.id and r.staticchatid = p.staticchatid
	where r.isfinal = 0
), roots as (
	select r.*, p.title
	from r
	inner join staticmessages p on r.parentid = p.id and r.staticchatid = p.staticchatid
	where isfinal = 1 and action = 'topic_created'
), newtopics as (
	select *
	from (
		select 
			-- c.*, r.*, m.*
			c.name, r.title, m.staticchatid, isnull(r.parentid, 0) rootmessageidor0
		from m
		left join roots r on r.childid = m.id and r.staticchatid = m.staticchatid
		inner join staticchats c on c.id = m.staticchatid
		group by c.name, r.title, m.staticchatid, r.parentid
	) x
)

insert into statictopics (staticchatid, rootmessageidor0, originaltitle, title)
select nt.staticchatid, nt.rootmessageidor0, case when nt.title is null then nt.name else nt.name + ' / ' + nt.title end, case when nt.title is null then nt.name else nt.name + ' / ' + nt.title end
from newtopics nt
left join statictopics t on t.staticchatid = nt.staticchatid and nt.rootmessageidor0 = t.rootmessageidor0
where t.staticchatid is null
ORDER BY nt.staticchatid, nt.rootmessageidor0

");
    }

    public async Task UpdateStaticMessagesTopics()
    {
        await Database.ExecuteSqlAsync(@$"

with m as (
	select *
	from staticmessages m
	where selectedtype is not null
), r as (
	select staticchatid, id childid, replytomessageid parentid, 1 as level, 0 as isfinal
	from m
	where replytomessageid is not null
	union all
	select p.staticchatid, r.childid, isnull(p.replytomessageid, p.id) parentid, level + 1 as level, case when p.replytomessageid is null then 1 else 0 end isfinal
	from staticmessages p
	inner join r on r.parentid = p.id and r.staticchatid = p.staticchatid
	where r.isfinal = 0
), roots as (
	select r.*, p.title
	from r
	inner join staticmessages p on r.parentid = p.id and r.staticchatid = p.staticchatid
	where isfinal = 1 and action = 'topic_created'
)

update staticmessages
set statictopicrootmessageidor0 = t.RootMessageIdOr0
from staticmessages m
inner join m mm on mm.staticchatid = m.staticchatid and mm.id = m.id
left join roots r on r.staticchatid = m.staticchatid and r.childid = m.id
left join statictopics t on t.staticchatid = m.staticchatid and t.rootmessageidor0 = isnull(r.parentid, 0)

");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StaticMessage>()
            .Property(x => x.ContactInformation)
            .HasConversion<string?>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<StaticMessage>()
            .Property(x => x.InlineBotButtons)
            .HasConversion<string?>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<StaticMessage>()
            .Property(x => x.LocationInformation)
            .HasConversion<string?>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<StaticMessage>()
            .Property(x => x.Poll)
            .HasConversion<string?>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<StaticMessage>()
            .Property(x => x.Text)
            .HasConversion<string>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<StaticMessage>()
            .Property(x => x.TextEntities)
            .HasConversion<string>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<AlbumConstraint>()
            .Property(x => x.StaticMessageSelectedType)
            .HasConversion<string>();

        modelBuilder.Entity<StaticMessage>()
            .Property(x => x.SelectedType)
            .HasConversion<string>()
            .HasComputedColumnSql(
                "case when photo is not null then 'photo' when mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null') then 'video' else null end");

        modelBuilder.Entity<LiveMedia>()
            .Property(x => x.Type)
            .HasConversion<string>();

        modelBuilder.Entity<Media>()
            .HasOne(x => x.StaticMessage)
            .WithOne(x => x.Media)
            .HasPrincipalKey<StaticMessage>(x => new { x.StaticChatId, x.Id })
            .HasForeignKey<Media>(x => new { x.StaticChatId, x.StaticMessageId })
            .IsRequired(false);

        modelBuilder.Entity<Media>()
            .HasOne(x => x.LiveMedia)
            .WithOne(x => x.Media)
            .HasPrincipalKey<LiveMedia>(x => new { x.Id, x.LiveTopicLiveChatId })
            .HasForeignKey<Media>(x => new { x.LiveMediaId, x.LiveChatId })
            .IsRequired(false);

        modelBuilder.Entity<LiveMedia>()
            .Property(x => x.SelectedType)
            .HasConversion<string>()
            .HasComputedColumnSql(
                @"
case 
when type = 'Photo' then 'Photo' 
when MimeType like 'video/%' then 'Video' 
when mimetype = 'image/webp' then null 
when mimetype = 'application/x-tgsticker' then null
when MimeType like 'image/%' then 'Photo' 
else null
end
");

        modelBuilder.Entity<Media>()
            .Property(x => x.Type)
            .HasConversion<string>();

        modelBuilder.Entity<StaticMessage>()
            .HasOne(x => x.StaticTopic)
            .WithMany(x => x.StaticMessages)
            .IsRequired(false)
            .HasForeignKey(x => new { x.StaticChatId, x.StaticTopicRootMessageIdOr0 });

        modelBuilder.Entity<Topic>()
            .HasOne(x => x.StaticTopic)
            .WithOne(x => x.Topic)
            .HasPrincipalKey<StaticTopic>(x => new { x.StaticChatId, x.RootMessageIdOr0 })
            .HasForeignKey<Topic>(x => new { x.StaticChatId, x.StaticTopicRootMessageIdOr0 })
            .IsRequired(false);

        modelBuilder.Entity<Topic>()
            .HasOne(x => x.LiveTopic)
            .WithOne(x => x.Topic)
            .HasPrincipalKey<LiveTopic>(x => new { x.Id, x.LiveChatId })
            .HasForeignKey<Topic>(x => new { x.LiveTopicId, x.LiveChatId })
            .IsRequired(false);
    }
}