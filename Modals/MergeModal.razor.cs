using Microsoft.AspNetCore.Components;

namespace BlazorWASM2.Modals
{
    public partial class MergeModal
    {
        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public string TaskName { get; set; } = "";

        [Parameter]
        public EventCallback<string> TaskNameChanged { get; set; }

        [Parameter]
        public string Error { get; set; } = "";

        [Parameter]
        public EventCallback OnConfirm { get; set; }

        [Parameter]
        public EventCallback OnCancel { get; set; }
    }
}
