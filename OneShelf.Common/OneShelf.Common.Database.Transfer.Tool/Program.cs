using Microsoft.Extensions.DependencyInjection;
using OneShelf.Collectives.Database;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Services;
using OneShelf.Common.Database.Transfer;
using OneShelf.Common.Database.Transfer.Tool;
using OneShelf.Frontend.Database;
using OneShelf.Illustrations.Database;
using OneShelf.Illustrations.Database.Services;
using OneShelf.OneDog.Database;
using OneShelf.OneDragon.Database;

var services = new ServiceCollection()
    .AddLogging()
    .AddSingleton<SongsDatabaseMemory>()
    .AddSingleton<ImagesProcessor>()
    .BuildServiceProvider();

return await TransferTool.RunAsync(args, services,
    new TransferredContext<SongsDatabase>(nameof(SongsDatabase), SongsDatabase.PostgreSqlMigrationsAssembly),
    new TransferredContext<DogDatabase>(nameof(DogDatabase), DogDatabase.PostgreSqlMigrationsAssembly),
    new TransferredContext<DragonDatabase>(nameof(DragonDatabase), DragonDatabase.PostgreSqlMigrationsAssembly),
    new LoadedContext<CollectivesDatabase>(nameof(CollectivesDatabase), CollectivesDatabase.PostgreSqlMigrationsAssembly, CosmosExport.LoadCollectives),
    new LoadedContext<IllustrationsDatabase>(nameof(IllustrationsDatabase), IllustrationsDatabase.PostgreSqlMigrationsAssembly, CosmosExport.LoadIllustrations),
    new LoadedContext<FrontendDatabase>(nameof(FrontendDatabase), FrontendDatabase.PostgreSqlMigrationsAssembly));
