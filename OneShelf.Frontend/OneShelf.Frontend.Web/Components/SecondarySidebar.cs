using Microsoft.AspNetCore.Components;
using OneShelf.Frontend.Web.Shared;

namespace OneShelf.Frontend.Web.Components;

public class SecondarySidebar : ComponentBase, IDisposable
{
	[CascadingParameter]
	public required MainLayout MainLayout { get; set; }

	[Parameter]
	public required bool IsJust { get; set; }

	[Parameter] 
	public required RenderFragment ChildContent { get; set; }

	protected override void OnParametersSet()
	{
		MainLayout.SetSidebar(IsJust);
		base.OnParametersSet();
	}

	protected override void OnInitialized()
	{
		MainLayout.SetSecondarySidebar(this);
		base.OnInitialized();
	}

	protected override bool ShouldRender()
	{
		var shouldRender = base.ShouldRender();
		if (shouldRender)
		{
			MainLayout.Update();
		}

		return base.ShouldRender();
	}

	public void Dispose()
	{
		MainLayout.SetSecondarySidebar(null);
	}
}