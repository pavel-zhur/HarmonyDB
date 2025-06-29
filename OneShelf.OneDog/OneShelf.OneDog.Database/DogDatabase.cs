using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OneShelf.OneDog.Database.Model;
using OneShelf.OneDog.Database.Model.Enums;
using OneShelf.Telegram.Ai.Model;

namespace OneShelf.OneDog.Database;

public class DogDatabase : DbContext, IInteractionsRepository<InteractionType>
{
    public DogDatabase(DbContextOptions<DogDatabase> options) : base(options)
    {
    }

    public required DbSet<User> Users { get; set; }

    public required DbSet<Interaction> Interactions { get; set; }

    public required DbSet<Domain> Domains { get; set; }

    public required DbSet<Chat> Chats { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Interaction>()
            .Property(x => x.InteractionType)
            .HasConversion<string?>();
    }

    #region IInteractionsRepository

    private Expression<Func<Interaction, bool>>? _scopeFilter;

    public void InitializeInteractionsRepositoryScope(int domainId)
    {
        if (_scopeFilter != null)
            throw new("Already initialized.");

        _scopeFilter = i => i.DomainId == domainId;
    }

    Task<List<IInteraction<InteractionType>>> IInteractionsRepository<InteractionType>.Get(Func<IQueryable<IInteraction<InteractionType>>, IQueryable<IInteraction<InteractionType>>> query)
    {
        return query(Interactions.Where(_scopeFilter ?? throw new("Not initialized."))).ToListAsync();
    }

    async Task IInteractionsRepository<InteractionType>.Add(List<IInteraction<InteractionType>> interactions)
    {
        Interactions.AddRange(interactions.Cast<Interaction>());
        await SaveChangesAsync();
    }

    async Task IInteractionsRepository<InteractionType>.Update(IInteraction<InteractionType> interaction)
    {
        await SaveChangesAsync();
    }

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterMessage => InteractionType.OwnChatterMessage;

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterImageMessage => InteractionType.OwnChatterImageMessage;

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterMemoryPoint => InteractionType.OwnChatterMemoryPoint;

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterResetDialog => InteractionType.OwnChatterResetDialog;

    InteractionType IInteractionsRepository<InteractionType>.ImagesLimit => InteractionType.ImagesLimit;

    InteractionType IInteractionsRepository<InteractionType>.ImagesSuccess => InteractionType.ImagesSuccess;

    InteractionType IInteractionsRepository<InteractionType>.Audio => InteractionType.OwnChatterAudio;

    InteractionType IInteractionsRepository<InteractionType>.VideosLimit => InteractionType.VideosLimit;

    InteractionType IInteractionsRepository<InteractionType>.VideosSuccess => InteractionType.VideosSuccess;

    InteractionType IInteractionsRepository<InteractionType>.SongsLimit => InteractionType.MusicLimit;

    InteractionType IInteractionsRepository<InteractionType>.SongsSuccess => InteractionType.MusicSuccess;

    #endregion
}