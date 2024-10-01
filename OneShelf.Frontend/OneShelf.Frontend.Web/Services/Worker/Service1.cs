using System.Security.Cryptography.X509Certificates;
using OneShelf.Authorization.Api.Model;
using OneShelf.Frontend.Web.Models;
using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.WebWorkers;

namespace OneShelf.Frontend.Web.Services.Worker;

public class Service2(ILogger<Service2> logger) : IService2
{
    public async Task Callback(string message) => logger.LogInformation(message);
}

public interface IService2
{
    Task Callback(string message);
}

public class Service1(ILogger<Service1> logger, BlazorJSRuntime blazorJsRuntime, WebWorkerService webWorkerService) : IService1
{
    public async Task<int> Go(int parameter)
    {
        logger.LogInformation("Worker {parameter} waiting", parameter);
        await Task.Delay(TimeSpan.FromSeconds(3));
        if (parameter == 0) throw new("Zero.");
        logger.LogInformation("Worker {parameter} done", parameter);
        return parameter * 2;
    }

    public async Task Initialize(Identity? identity)
    {
        logger.LogInformation("Got {identity} in the web worker.", identity);
        Go2();
    }

    private async void Go2()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            var message = Random.Shared.NextDouble();
            foreach (var instance in webWorkerService.Instances.Where(x => x.Info.Scope == GlobalScope.Window))
            {
                try
                {
                    await instance.GetService<IService2>().Callback($"{message} from {webWorkerService.Info.InstanceId}");
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Could not send a callback to parent {instance id}", instance.Info.ParentInstanceId);
                }
            }
        }
    }

    public async Task Initialize(IReadOnlyList<string> externalIds)
    {
        logger.LogInformation("Got collection of {count} external ids.", externalIds.Count);
    }
}

public interface IService1
{
    Task<int> Go(int parameter);
    Task Initialize(Identity? identity);
    Task Initialize(IReadOnlyList<string> externalIds);
}

public class OptionalWebWorker(WebWorkerService webWorkerService, ILogger<OptionalWebWorker> logger, CollectionIndexProvider collectionIndexProvider, IdentityProvider identityProvider) : IDisposable
{
    private readonly TimeSpan _firstDelay = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _smallDelay = TimeSpan.FromSeconds(1);
    private bool _isInitializationStarted;
    private bool _isInitializationFinished;
    private WebWorker? _webWorker;
    private IService1? _service1;

    public bool IsInitializationFinished => _isInitializationFinished;
    public bool WebWorkerExists => _webWorker != null;

    public async void Initialize()
    {
        try
        {
            if (_isInitializationStarted)
                return;
            
            _isInitializationStarted = true;

            await Task.Delay(_firstDelay);

            _webWorker = await webWorkerService.GetWebWorker();
            if (_webWorker == null)
                return;

            logger.LogInformation("Created scope = {scope}", webWorkerService.Instances.Single(x => x.Info.InstanceId == _webWorker.LocalInfo.InstanceId).Info.Scope);
            logger.LogInformation("Created iid = {scope}", webWorkerService.Instances.Single(x => x.Info.InstanceId == _webWorker.LocalInfo.InstanceId).Info.InstanceId);
            logger.LogInformation("Created iidl = {scope}", _webWorker.LocalInfo.InstanceId);
            logger.LogInformation("Created iidr = {scope}", _webWorker.RemoteInfo!.InstanceId);
            logger.LogInformation("Created parent instance id = {scope}", webWorkerService.Instances.Single(x => x.Info.InstanceId == _webWorker.LocalInfo.InstanceId).Info.ParentInstanceId);
            logger.LogInformation("Current id = {scope}", webWorkerService.InstanceId);

            foreach (var appInstance in webWorkerService.Instances)
            {
                logger.LogInformation("Registered {id} {scope} {parent} cid={cliend id} il={is local} n={name}", appInstance.Info.InstanceId, appInstance.Info.Scope, appInstance.Info.ParentInstanceId, appInstance.Info.ClientId, appInstance.IsLocal, appInstance.Info.Name);
            }

            await Task.Delay(_smallDelay);

            _service1 = _webWorker.GetService<IService1>();

            await Task.Delay(_smallDelay);

            var collectionIndex = collectionIndexProvider.Peek();
            collectionIndexProvider.CollectionChanged += CollectionChanged;
            await CollectionChanged(collectionIndex.collectionIndex, collectionIndex.failed);

            await Task.Delay(_smallDelay);

            identityProvider.IdentityChange += IdentityChange;
            IdentityChange();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error initializing the optional web worker.");
        }

        _isInitializationFinished = true;
    }

    private async void IdentityChange()
    {
        if (_service1 == null)
            return;

        await _service1.Initialize(identityProvider.Identity);
    }

    private async Task CollectionChanged(CollectionIndex? collectionIndex, bool failed)
    {
        if (collectionIndex != null && _service1 != null)
            await _service1.Initialize(collectionIndex.VersionsByExternalId.Select(x => x.Key).ToList());
    }

    public void Dispose()
    {
        identityProvider.IdentityChange -= IdentityChange;
        collectionIndexProvider.CollectionChanged -= CollectionChanged;
        _webWorker?.Dispose();
        _webWorker = null;
    }
}