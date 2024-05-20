using Microsoft.AspNetCore.Components;
using OneShelf.Frontend.Web.Models;
using OneShelf.Frontend.Web.Services;

namespace OneShelf.Frontend.Web.Components
{
    public class CollectionComponentBase : ComponentBase, IDisposable
    {
        [Inject] 
        private CollectionIndexProvider CollectionIndexProvider { get; init; } = null!;

        [Inject] 
        private ILogger<CollectionComponentBase> Logger { get; init; } = default!;

        protected CollectionIndex? CollectionIndex => CollectionIndexProvider.Peek().collectionIndex;

        protected bool CollectionIndexFailed => CollectionIndexProvider.Peek().failed;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            CollectionIndexProvider.CollectionChanged += OnCollectionIndexProviderCollectionChanged;

            if (CollectionIndexFailed) CollectionIndexProvider.Sync();
        }

        protected void RetryCollectionIndex() => CollectionIndexProvider.Sync();

        public virtual void Dispose()
        {
            CollectionIndexProvider.CollectionChanged -= OnCollectionIndexProviderCollectionChanged;
        }

        private async Task OnCollectionIndexProviderCollectionChanged(CollectionIndex? collectionIndex, bool failed)
        {
            await OnNewCollectionIndexReceived();
            StateHasChanged();
        }

        protected virtual async Task OnNewCollectionIndexReceived()
        {
        }
    }
}
