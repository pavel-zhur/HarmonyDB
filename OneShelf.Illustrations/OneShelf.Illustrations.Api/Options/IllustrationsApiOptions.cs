namespace OneShelf.Illustrations.Api.Options;

public class IllustrationsApiOptions
{
    /// <summary>
    /// The address of this API as browsers reach it, ending with the routes' prefix ("…/api/").
    /// The public addresses of the illustrations' copies are made from it.
    /// </summary>
    public Uri? PublicEndpoint { get; set; }
}
