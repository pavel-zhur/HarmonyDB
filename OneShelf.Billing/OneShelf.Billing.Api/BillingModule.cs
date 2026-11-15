using Microsoft.AspNetCore.Builder;
using OneShelf.Common.Api.Hosting;
using OneShelf.Common.Database.Songs;

namespace OneShelf.Billing.Api;

public class BillingModule() : ApiModule("Billing")
{
    public override void Configure(WebApplicationBuilder builder) => builder.Services
        .AddSongsDatabase();
}
