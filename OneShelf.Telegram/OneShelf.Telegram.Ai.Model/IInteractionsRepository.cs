namespace OneShelf.Telegram.Ai.Model;

public interface IInteractionsRepository<TInteractionType>
{
    Task<List<IInteraction<TInteractionType>>> Get(Func<IQueryable<IInteraction<TInteractionType>>, IQueryable<IInteraction<TInteractionType>>> query);

    Task Add(List<IInteraction<TInteractionType>> interactions);

    Task Update(IInteraction<TInteractionType> interaction);

    TInteractionType OwnChatterMessage { get; }
    TInteractionType OwnChatterImageMessage { get; }
    TInteractionType OwnChatterMemoryPoint { get; }
    TInteractionType OwnChatterResetDialog { get; }
    TInteractionType ImagesLimit { get; }
    TInteractionType ImagesSuccess { get; }
    TInteractionType VideosLimit { get; }
    TInteractionType VideosSuccess { get; }
    TInteractionType SongsLimit { get; }
    TInteractionType SongsSuccess { get; }
    TInteractionType Audio { get; }
}