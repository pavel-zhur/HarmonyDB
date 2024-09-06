using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Videos.App.Database;
using OneShelf.Videos.App.Models;
using OneShelf.Videos.App.Services;
using OneShelf.Videos.App.UpdatedGooglePhotosService;

var builder = Host.CreateApplicationBuilder();
builder.Configuration.AddJsonFile("appsettings.Secrets.json");
builder.Services
    .Configure<VideosOptions>(o => builder.Configuration.GetSection(nameof(VideosOptions)).Bind(o))
    .AddScoped<Service1>()
    .AddScoped<Service2>()
    .AddScoped<Service3>()
    .AddDbContext<VideosDatabase>(o => o.UseSqlServer(builder.Configuration.GetConnectionString(nameof(VideosDatabase))))
    .AddMyGooglePhotos();
var host = builder.Build();

var service1 = host.Services.GetRequiredService<Service1>();
var service2 = host.Services.GetRequiredService<Service2>();
var service3 = host.Services.GetRequiredService<Service3>();

await using var videosDatabase = host.Services.GetRequiredService<VideosDatabase>();
await videosDatabase.Database.MigrateAsync();

//await videosDatabase.CreateMissingTopics();
//await videosDatabase.UpdateMessagesTopics();

//return;

//await service1.SaveChatFolders();
//await service1.SaveMessages();
//await service2.SaveInventory();
//return;

////re-login and display auth token
//if (true)
//{
//    var updatedGooglePhotosService = host.Services.GetRequiredService<UpdatedGooglePhotosService>();
//    await updatedGooglePhotosService.LoginAsync();
//    //Console.WriteLine(updatedGooglePhotosService.GetLogin());
//    return;
//}

//await service2.UploadPhotos((await service1.GetExport1()).OrderBy(_ => Random.Shared.NextDouble()).ToList());
//await service2.UploadVideos((await service1.GetExport2()).OrderBy(_ => Random.Shared.NextDouble()).ToList());
//await service2.CreateAlbums(await service1.GetAlbums());