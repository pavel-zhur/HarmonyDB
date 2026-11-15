using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Services;
using OneShelf.Common.Database.Transfer;
using OneShelf.OneDog.Database;
using OneShelf.OneDragon.Database;

var services = new ServiceCollection()
    .AddLogging()
    .AddSingleton<SongsDatabaseMemory>()
    .BuildServiceProvider();

return await TransferTool.RunAsync(args, services,
    new TransferredContext<SongsDatabase>(nameof(SongsDatabase), SongsDatabase.PostgreSqlMigrationsAssembly),
    new TransferredContext<DogDatabase>(nameof(DogDatabase), DogDatabase.PostgreSqlMigrationsAssembly),
    new TransferredContext<DragonDatabase>(nameof(DragonDatabase), DragonDatabase.PostgreSqlMigrationsAssembly));
