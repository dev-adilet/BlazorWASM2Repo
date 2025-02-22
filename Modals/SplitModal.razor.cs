using Microsoft.AspNetCore.Components;

namespace BlazorWASM2.Modals
{
    public partial class SplitModal
    {
        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public string LowerBound { get; set; } = "";

        [Parameter]
        public string UpperBound { get; set; } = "";

        [Parameter]
        public string SplitTime { get; set; } = "";

        [Parameter]
        public EventCallback<string> SplitTimeChanged { get; set; }

        [Parameter]
        public string Error { get; set; } = "";

        [Parameter]
        public EventCallback OnConfirm { get; set; }

        [Parameter]
        public EventCallback OnCancel { get; set; }
    }
}
