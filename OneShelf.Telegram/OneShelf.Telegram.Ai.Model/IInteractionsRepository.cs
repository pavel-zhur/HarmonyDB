namespace OneShelf.Telegram.Ai.Model;

public interface IInteractionsRepository<TInteractionType>
{
    Task<List<IInteraction<TInteractionType>>> Get(Func<IQueryable<IInteraction<TInteractionType>>, IQueryable<IInteraction<TInteractionType>>> query);

    Task Add(List<IInteraction<TInteractionType>> interactions);

    TInteractionType OwnChatterMessage { get; }
    TInteractionType OwnChatterMemoryPoint { get; }
    TInteractionType OwnChatterResetDialog { get; }
    TInteractionType ImagesLimit { get; }
    TInteractionType ImagesSuccess { get; }
}