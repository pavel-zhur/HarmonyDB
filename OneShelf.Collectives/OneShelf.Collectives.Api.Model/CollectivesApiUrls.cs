namespace OneShelf.Collectives.Api.Model;

public static class CollectivesApiUrls
{
    public const string V2Delete = nameof(V2Delete);
    public const string V2List = nameof(V2List);
    public const string V2Update = nameof(V2Update);
    public const string V2Insert = nameof(V2Insert);
    public const string V2Get = nameof(V2Get);

    public const string Get = nameof(Get);
    public const string ListAll = nameof(ListAll);

    // the own chords by their authors' names, and every one for their source, to internal clients
    public const string Items = nameof(Items);
    public const string OwnList = nameof(OwnList);
    public const string OwnGet = nameof(OwnGet);
    public const string OwnInsert = nameof(OwnInsert);
    public const string OwnUpdate = nameof(OwnUpdate);
    public const string OwnDelete = nameof(OwnDelete);
    public const string OwnRemovePrivate = nameof(OwnRemovePrivate);
}