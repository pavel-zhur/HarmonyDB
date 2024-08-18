using Microsoft.EntityFrameworkCore;
using OneShelf.OneDragon.Database.Model;

namespace OneShelf.OneDragon.Database;

public class DragonDatabase : DbContext
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
}