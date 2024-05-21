using DnetIndexedDb;
using Microsoft.JSInterop;

namespace OneShelf.Frontend.Web.IndexedDb;

public class MyIndexedDb : IndexedDbInterop
{
    public MyIndexedDb(IJSRuntime jsRuntime, IndexedDbOptions<MyIndexedDb> indexedDbDatabaseOptions)
        : base(jsRuntime, indexedDbDatabaseOptions)
    {
    }
}