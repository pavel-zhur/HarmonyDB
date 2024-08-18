using Microsoft.EntityFrameworkCore;
using OneShelf.OneDragon.Database.Model;
using OneShelf.OneDragon.Database.Model.Enums;
using OneShelf.Telegram.Ai.Model;

namespace OneShelf.OneDragon.Database;

public class DragonDatabase : DbContext, IInteractionsRepository<InteractionType>
{
    public DragonDatabase(DbContextOptions<DragonDatabase> options) : base(options)
    {
    }

    public required DbSet<User> Users { get; set; }

    public required DbSet<Interaction> Interactions { get; set; }

    public required DbSet<Chat> Chats { get; set; }
    
    public required DbSet<Update> Updates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Interaction>()
            .Property(x => x.InteractionType)
            .HasConversion<string?>();
    }

    #region IInteractionsRepository

    Task<List<IInteraction<InteractionType>>> IInteractionsRepository<InteractionType>.Get(Func<IQueryable<IInteraction<InteractionType>>, IQueryable<IInteraction<InteractionType>>> query)
    {
        return query(Interactions).ToListAsync();
    }

    async Task IInteractionsRepository<InteractionType>.Add(List<IInteraction<InteractionType>> interactions)
    {
        Interactions.AddRange(interactions.Cast<Interaction>());
        await SaveChangesAsync();
    }

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterMessage => InteractionType.AiMessage;

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterMemoryPoint => InteractionType.AiMemoryPoint;

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterResetDialog => InteractionType.AiResetDialog;

    InteractionType IInteractionsRepository<InteractionType>.ImagesLimit => InteractionType.AiImagesLimit;

    InteractionType IInteractionsRepository<InteractionType>.ImagesSuccess => InteractionType.AiImagesSuccess;

    #endregion
}