using Microsoft.EntityFrameworkCore;
using OneShelf.OneDog.Database.Model;

namespace OneShelf.OneDog.Database;

public class DogDatabase : DbContext
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
}