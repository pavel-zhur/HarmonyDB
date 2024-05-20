using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Model;
using OneShelf.Collectives.Api.Model;
using OneShelf.Collectives.Api.Model.V2;
using OneShelf.Collectives.Api.Model.V2.Sub;
using OneShelf.Collectives.Api.Model.VInternal;
using OneShelf.Common.Api.Client;
using GetRequest = OneShelf.Collectives.Api.Model.VInternal.GetRequest;
using GetResponse = OneShelf.Collectives.Api.Model.VInternal.GetResponse;

namespace OneShelf.Collectives.Api.Client;

public class CollectivesApiClient : ApiClientBase<CollectivesApiClient>
{
    public CollectivesApiClient(IHttpClientFactory httpClientFactory, IOptions<ApiClientOptions<CollectivesApiClient>> options)
        : base(options, httpClientFactory)
    {
    }

    public async Task<ListAllResponse> ListAll()
        => await PostWithCode<ListAllRequest, ListAllResponse>(CollectivesApiUrls.ListAll, new());

    public async Task<Model.V2.GetResponse> Get(Guid collectiveId, Identity identity)
        => await Post<Model.V2.GetRequest, Model.V2.GetResponse>(CollectivesApiUrls.V2Get, new()
        {
            CollectiveId = collectiveId,
            Identity = identity,
        });

    public async Task<GetResponse> Get(Guid collectiveId)
        => await PostWithCode<GetRequest, GetResponse>(CollectivesApiUrls.Get, new()
        {
            CollectiveId = collectiveId,
        });

    public async Task<GetResponse> Get(Uri collectiveUri)
        => await PostWithCode<GetRequest, GetResponse>(CollectivesApiUrls.Get, new()
        {
            CollectiveUri = collectiveUri,
        });

    public async Task<ListResponse> List(Identity identity)
        => await Post<ListRequest, ListResponse>(CollectivesApiUrls.V2List, new()
        {
            Identity = identity,
        });

    public async Task<DeleteResponse> Delete(Identity identity, Guid collectiveId)
        => await Post<DeleteRequest, DeleteResponse>(CollectivesApiUrls.V2Delete, new()
        {
            Identity = identity,
            CollectiveId = collectiveId,
        });

    public async Task<UpdateResponse> Update(Identity identity, Guid collectiveId, Collective collective)
        => await Post<UpdateRequest, UpdateResponse>(CollectivesApiUrls.V2Update, new()
        {
            Identity = identity,
            CollectiveId = collectiveId,
            Collective = collective,
        });

    public async Task<InsertResponse> Insert(Identity identity, Collective collective, int? derivedFromVersionId)
        => await Post<InsertRequest, InsertResponse>(CollectivesApiUrls.V2Insert, new()
        {
            Identity = identity,
            Collective = collective,
            DerivedFromVersionId = derivedFromVersionId,
        });
}