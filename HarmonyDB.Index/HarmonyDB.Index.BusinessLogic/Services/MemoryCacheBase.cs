using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace HarmonyDB.Index.BusinessLogic.Services;

public abstract class MemoryCacheBase<TInputModel, TPresentationModel>
{
    private readonly AsyncLock _lock = new();
    private TPresentationModel? _presentationModel;

    protected MemoryCacheBase(ILogger<MemoryCacheBase<TInputModel, TPresentationModel>> logger)
    {
        Logger = logger;
    }

    protected abstract string Key { get; }

    protected ILogger<MemoryCacheBase<TInputModel, TPresentationModel>> Logger { get; }

    protected abstract Task<TInputModel> GetInputModel();

    protected abstract TPresentationModel GetPresentationModel(TInputModel inputModel);

    public async Task<TPresentationModel> Get()
    {
        if (_presentationModel != null) return _presentationModel;
        using var _ = await _lock.LockAsync();
        if (_presentationModel != null) return _presentationModel;

        var started = DateTime.Now;
        var inputModel = await GetInputModel();
        Logger.LogInformation("Getting the input model for the {type} memory cache took {time} ms", Key, (DateTime.Now - started).TotalMicroseconds);
        started = DateTime.Now;
        _presentationModel = GetPresentationModel(inputModel);
        Logger.LogInformation("Building the presentation model for the {type} memory cache took {time} ms", Key, (DateTime.Now - started).TotalMicroseconds);

        return _presentationModel;
    }
}