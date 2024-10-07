using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Videos.BusinessLogic;
using OneShelf.Videos.BusinessLogic.Services;
using OneShelf.Videos.BusinessLogic.Services.Live;
using OneShelf.Videos.Database;

var builder = Host.CreateApplicationBuilder();
builder.Configuration.AddJsonFile("appsettings.Secrets.json");
builder.Services
    .AddVideosBusinessLogic(builder.Configuration, false);
using var host = builder.Build();

var service1 = host.Services.GetRequiredService<Service1>();
var service2 = host.Services.GetRequiredService<Service2>();
var liveDownloader = host.Services.GetRequiredService<LiveDownloader>();
await using var videosDatabase = host.Services.GetRequiredService<VideosDatabase>();

//await videosDatabase.Database.MigrateAsync();

//await liveDownloader.UpdateLive(true, new());
//await videosDatabase.AppendTopics();
//await videosDatabase.AppendMediae();
//await videosDatabase.UpdateMediaTopics();
//await service2.UploadPhotos(await service1.GetExportLivePhoto());
//await service2.UploadVideos(await service1.GetExportLiveVideo());
//await service2.SaveInventory();


//await videosDatabase.CreateMissingStaticTopics();
//await videosDatabase.UpdateStaticMessagesTopics();

//return;

//await service1.SaveChatFolders();
//await service1.SaveMessages();
//await service2.SaveInventory();
//return;

//await service2.CreateAlbums(await service1.GetAlbums());