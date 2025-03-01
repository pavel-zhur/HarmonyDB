﻿using OneShelf.Videos.Database.Models.Live;
using OneShelf.Videos.Database.Models.Static;

namespace OneShelf.Videos.Database.Models;

public class Media
{
    public int Id { get; set; }

    public long? StaticChatId { get; set; }
    public int? StaticMessageId { get; set; }

    public StaticChat? StaticChat { get; set; }
    public StaticMessage? StaticMessage { get; set; }

    public long? LiveChatId { get; set; }
    public int? LiveMediaId { get; set; }

    public LiveChat? LiveChat { get; set; }
    public LiveMedia? LiveMedia { get; set; }

    public MediaType Type { get; set; }

    public UploadedItem? UploadedItem { get; set; }
    
    public int? TopicId { get; set; }
    public Topic? Topic { get; set; }
}