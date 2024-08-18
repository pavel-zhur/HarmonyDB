using Microsoft.EntityFrameworkCore;
using OneShelf.OneDragon.Database.Model;
using OneShelf.OneDragon.Database.Model.Enums;
using OneShelf.Telegram.Ai.Model;
using System.Linq.Expressions;

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

    public required DbSet<AiParameters> AiParameters { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Interaction>()
            .Property(x => x.InteractionType)
            .HasConversion<string?>();
    }

    #region IInteractionsRepository

    private Expression<Func<Interaction, bool>>? _scopeFilter;

    public void InitializeInteractionsRepositoryScope(long userId, long chatId)
    {
        if (_scopeFilter != null)
            throw new("Already initialized.");

        _scopeFilter = i => i.UserId == userId && i.ChatId == chatId;
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

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterMessage => InteractionType.AiMessage;

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterMemoryPoint => InteractionType.AiMemoryPoint;

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterResetDialog => InteractionType.AiResetDialog;

    InteractionType IInteractionsRepository<InteractionType>.ImagesLimit => InteractionType.AiImagesLimit;

    InteractionType IInteractionsRepository<InteractionType>.ImagesSuccess => InteractionType.AiImagesSuccess;

    #endregion
}