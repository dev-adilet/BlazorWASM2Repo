using Microsoft.AspNetCore.Components;

namespace BlazorWASM2.Modals
{
    public partial class EditModal
    {
        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public string StartTime { get; set; } = "";

        [Parameter]
        public EventCallback<string> StartTimeChanged { get; set; }

        [Parameter]
        public string EndTime { get; set; } = "";

        [Parameter]
        public EventCallback<string> EndTimeChanged { get; set; }

        [Parameter]
        public string TaskName { get; set; } = "";

        [Parameter]
        public EventCallback<string> TaskNameChanged { get; set; }

        [Parameter]
        public string Error { get; set; } = "";

        [Parameter]
        public EventCallback OnSave { get; set; }

        [Parameter]
        public EventCallback OnCancel { get; set; }
    }
}
