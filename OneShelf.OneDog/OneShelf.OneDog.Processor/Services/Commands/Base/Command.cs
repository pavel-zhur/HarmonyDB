using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.Base;

public abstract class Command : CommandBase
{
    protected Command(Io io, ScopeAwareness scopeAwareness)
        : base(io)
    {
        ScopeAwareness = scopeAwareness;
    }

    protected ScopeAwareness ScopeAwareness { get; }
}