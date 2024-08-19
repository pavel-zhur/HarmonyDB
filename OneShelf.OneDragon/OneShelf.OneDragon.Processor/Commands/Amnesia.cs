using OneShelf.OneDragon.Database;
using OneShelf.OneDragon.Database.Model.Enums;
using OneShelf.OneDragon.Processor.Services;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDragon.Processor.Commands;

[BothCommand("amnesia", "Амнезия", "После этой команды я забуду всё, что мы обсуждали, как будто съел слишком много косточек на ночь! Ла-ла, короткая память рулит!")]
public class Amnesia : Command
{
    private readonly DragonDatabase _dragonDatabase;
    private readonly DragonScope _scope;

    public Amnesia(Io io, DragonDatabase dragonDatabase, DragonScope scope) 
        : base(io)
    {
        _dragonDatabase = dragonDatabase;
        _scope = scope;
    }

    protected override async Task ExecuteQuickly()
    {
        Io.WriteLine("Память у меня, как у рыбки... или как у лужайки?");

        _dragonDatabase.Interactions.Add(new()
        {
            CreatedOn = DateTime.Now,
            ChatId = _scope.ChatId,
            UserId = Io.UserId,
            UpdateId = _scope.UpdateId,
            InteractionType = InteractionType.AiResetDialog,
            Serialized = "reset",
        });

        await _dragonDatabase.SaveChangesAsync();
    }
}