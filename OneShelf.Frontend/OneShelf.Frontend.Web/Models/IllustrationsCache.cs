using OneShelf.Frontend.Api.Model.V3.Illustrations;

namespace OneShelf.Frontend.Web.Models;

public class IllustrationsCache
{
    public required AllIllustrations All { get; init; }

    public required DateTime ReceivedOn { get; init; }
}