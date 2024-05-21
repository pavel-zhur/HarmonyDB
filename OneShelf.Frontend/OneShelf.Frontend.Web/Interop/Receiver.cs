using Microsoft.JSInterop;
using Nito.AsyncEx;
using OneShelf.Frontend.Web.Services;

namespace OneShelf.Frontend.Web.Interop;

public class Receiver : IDisposable
{
    private readonly List<ReceiverInstance> _instances = new();
    private readonly AsyncLock _instancesLock = new();
    private readonly IdentityProvider _identityProvider;

    public Receiver(IdentityProvider identityProvider)
    {
        _identityProvider = identityProvider;
        Reference = DotNetObjectReference.Create(this);
    }

    public DotNetObjectReference<Receiver> Reference { get; }

    [JSInvokable]
    public async Task OnTelegramAuth(object user)
    {
        await _identityProvider.Set(user);
    }

    [JSInvokable]
    public async Task OnChordClick(string chordData, int chordIndex)
    {
        using (await _instancesLock.LockAsync())
        {
            foreach (var receiverInstance in _instances)
            {
                await receiverInstance.CallChordClick(chordData, chordIndex);
            }
        }
    }

    public void Dispose()
    {
        Reference.Dispose();
    }

    public ReceiverInstance CreateInstance()
    {
        var instance = new ReceiverInstance(instance =>
        {
            using (_instancesLock.Lock())
            {
                _instances.Remove(instance);
            }
        });

        using (_instancesLock.Lock())
        {
            _instances.Add(instance);
        }

        return instance;
    }
}