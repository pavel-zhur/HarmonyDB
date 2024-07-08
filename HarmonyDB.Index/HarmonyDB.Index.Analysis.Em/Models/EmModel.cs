namespace HarmonyDB.Index.Analysis.Em.Models;

public record EmModel
{
    public EmModel(IReadOnlyDictionary<string, ISong> songs,
        IReadOnlyDictionary<string, ILoop> loops,
        IReadOnlyList<LoopLink> loopLinks)
    {
        Songs = songs;
        Loops = loops;
        LoopLinks = loopLinks;
        LoopLinksBySongId = loopLinks.ToLookup(x => x.SongId);
        LoopLinksByLoopId = loopLinks.ToLookup(x => x.LoopId);
    }

    public IReadOnlyDictionary<string, ISong> Songs { get; }

    public IReadOnlyDictionary<string, ILoop> Loops { get; }

    public IReadOnlyList<LoopLink> LoopLinks { get; }

    public ILookup<string, LoopLink> LoopLinksBySongId { get; }

    public ILookup<string, LoopLink> LoopLinksByLoopId { get; }
}