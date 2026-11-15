namespace OneShelf.Illustrations.Api.Model;

public static class IllustrationsApiUrls
{
    public const string All = nameof(All);
    public const string Generate = nameof(Generate);

    [Obsolete]
    public const string GetImage = nameof(GetImage);

    [Obsolete]
    public const string GetImagePublic = nameof(GetImagePublic);
    public const string GetNonEmptyUrls = nameof(GetNonEmptyUrls);

    /// <summary>
    /// The copies of the illustrations, open to anyone: {Images}/{illustration id}/{size}.
    /// </summary>
    public const string Images = "images";

    public const string PublishCustomSystemMessagedPrompts = nameof(PublishCustomSystemMessagedPrompts);
}