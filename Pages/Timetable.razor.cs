using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlazorWASM2.Pages
{
    public partial class Timetable
    {
        protected List<TimetableEntry> timetable = new();

        [Inject]
        private IJSRuntime JSRuntime { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            if (!timetable.Any())
            {
                timetable.Add(new TimetableEntry
                {
                    StartTime = "5:45",
                    EndTime = "6:25",
                    Task = "Wake up & freshen up",
                    IsEditing = false,
                    IsSelected = false
                });
            }
        }

        private async Task PrintAsPDF()
        {
            await JSRuntime.InvokeVoidAsync("window.print");
        }

        protected void ToggleSelection(TimetableEntry entry)
        {
            if (!entry.IsEditing)
            {
                entry.IsSelected = !entry.IsSelected;
            }
        }

        protected void Edit(TimetableEntry entry)
        {
            entry.EditingBackup = new TimetableEntry
            {
                StartTime = entry.StartTime,
                EndTime = entry.EndTime,
                Task = entry.Task
            };
            entry.IsEditing = true;
        }

        protected void Save(TimetableEntry entry)
        {
            entry.IsEditing = false;
            entry.EditingBackup = null;

            int idx = timetable.IndexOf(entry);
            if (idx < timetable.Count - 1)
            {
                timetable[idx + 1].StartTime = entry.EndTime;
            }
        }

        protected void Cancel(TimetableEntry entry)
        {
            if (entry.EditingBackup != null)
            {
                entry.StartTime = entry.EditingBackup.StartTime;
                entry.EndTime = entry.EditingBackup.EndTime;
                entry.Task = entry.EditingBackup.Task;
            }
            entry.IsEditing = false;
            entry.EditingBackup = null;
        }

        protected void AddNewRow()
        {
            string newStartTime = "";
            if (timetable.Count > 0)
            {
                var lastEntry = timetable[timetable.Count - 1];
                newStartTime = lastEntry.EndTime;
            }
            var newEntry = new TimetableEntry
            {
                StartTime = newStartTime,
                EndTime = "",
                Task = "",
                IsEditing = true,
                IsSelected = false
            };
            timetable.Add(newEntry);
        }

        protected void MergeSelectedRows()
        {
            var selectedIndices = timetable
                .Select((entry, index) => new { entry, index })
                .Where(x => x.entry.IsSelected)
                .Select(x => x.index)
                .ToList();

            if (selectedIndices.Count == 0)
                return;

            selectedIndices.Sort();
            if (selectedIndices.Last() - selectedIndices.First() != selectedIndices.Count - 1)
            {
                return;
            }

            int firstIndex = selectedIndices.First();
            int lastIndex = selectedIndices.Last();

            var mergedEntry = new TimetableEntry
            {
                StartTime = timetable[firstIndex].StartTime,
                EndTime = timetable[lastIndex].EndTime,
                Task = string.Join(" ", timetable
                          .GetRange(firstIndex, lastIndex - firstIndex + 1)
                          .Select(r => r.Task)),
                IsEditing = false,
                IsSelected = false
            };

            timetable.RemoveRange(firstIndex, lastIndex - firstIndex + 1);
            timetable.Insert(firstIndex, mergedEntry);

            if (firstIndex < timetable.Count - 1)
            {
                timetable[firstIndex + 1].StartTime = mergedEntry.EndTime;
            }
        }

        protected async Task SplitRow(TimetableEntry entry)
        {
            var splitTime = await JSRuntime.InvokeAsync<string>(
                "prompt",
                $"Enter split time between {entry.StartTime} and {entry.EndTime}"
            );

            if (string.IsNullOrWhiteSpace(splitTime))
                return;

            int idx = timetable.IndexOf(entry);

            var row1 = new TimetableEntry
            {
                StartTime = entry.StartTime,
                EndTime = splitTime,
                Task = entry.Task,
                IsEditing = false,
                IsSelected = false
            };

            var row2 = new TimetableEntry
            {
                StartTime = splitTime,
                EndTime = entry.EndTime,
                Task = entry.Task,
                IsEditing = false,
                IsSelected = false
            };

            timetable.RemoveAt(idx);
            timetable.Insert(idx, row1);
            timetable.Insert(idx + 1, row2);

            if (idx + 2 < timetable.Count)
            {
                timetable[idx + 2].StartTime = row2.EndTime;
            }
        }

        protected async Task ExportTimetableAsync()
        {
            var fileName = await JSRuntime.InvokeAsync<string>("prompt", "Enter file name (without extension):", "timetable");
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }
            if (!fileName.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".json";
            }

            var json = JsonSerializer.Serialize(timetable);
            await JSRuntime.InvokeVoidAsync("downloadFile", fileName, json);
        }

        protected async Task HandleFileSelected(InputFileChangeEventArgs e)
        {
            var file = e.File;
            using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Seek(0, System.IO.SeekOrigin.Begin);
            var json = await new System.IO.StreamReader(ms).ReadToEndAsync();
            try
            {
                var importedTimetable = JsonSerializer.Deserialize<List<TimetableEntry>>(json);
                if (importedTimetable != null)
                {
                    timetable = importedTimetable;
                    StateHasChanged();
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Error parsing timetable file: " + ex.Message);
            }
        }

        protected void DeleteRow(TimetableEntry entry)
        {
            int idx = timetable.IndexOf(entry);
            if (idx == -1)
                return;

            if (idx > 0 && idx < timetable.Count - 1)
            {
                timetable[idx + 1].StartTime = timetable[idx - 1].EndTime;
            }
            timetable.RemoveAt(idx);
        }

        public class TimetableEntry
        {
            public string StartTime { get; set; } = "";
            public string EndTime { get; set; } = "";
            public string Task { get; set; } = "";
            public bool IsEditing { get; set; }
            public bool IsSelected { get; set; }
            public TimetableEntry? EditingBackup { get; set; }
        }
    }
}
