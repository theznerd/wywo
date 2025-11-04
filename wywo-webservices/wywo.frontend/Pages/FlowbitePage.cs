using Microsoft.AspNetCore.Components;
using wywo.frontend.Services;

namespace wywo.frontend.Pages
{
    public class FlowbitePage : ComponentBase
    {
        [Inject]
        protected IFlowbiteService FlowbiteService { get; set; } = default!;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await FlowbiteService.InitializeFlowbiteAsync();
            }
            await base.OnAfterRenderAsync(firstRender);
        }
    }
}
