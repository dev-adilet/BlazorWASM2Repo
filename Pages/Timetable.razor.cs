using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlazorWASM2.Pages
{
    public partial class Timetable : ComponentBase
    {
        protected List<TimetableEntry> timetable = new();

        [Inject]
        private IJSRuntime JSRuntime { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            // Example: if no rows exist, add a default row
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
            // This calls the browser’s native print dialog
            await JSRuntime.InvokeVoidAsync("window.print");
        }

        // Toggle row selection when the row is clicked (only if not editing).
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

            // If there's a next row, set its StartTime to this row's EndTime
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
            // Must be contiguous
            if (selectedIndices.Last() - selectedIndices.First() != selectedIndices.Count - 1)
            {
                // Not contiguous, do nothing or show error
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

            // Remove all selected rows
            timetable.RemoveRange(firstIndex, lastIndex - firstIndex + 1);

            // Insert the merged row
            timetable.Insert(firstIndex, mergedEntry);

            // If there's a next row, set its StartTime to the new row's EndTime
            if (firstIndex < timetable.Count - 1)
            {
                timetable[firstIndex + 1].StartTime = mergedEntry.EndTime;
            }
        }

        // -- SplitRow method with next-row logic --
        protected async Task SplitRow(TimetableEntry entry)
        {
            // Prompt user for the split time (e.g., "6:45")
            var splitTime = await JSRuntime.InvokeAsync<string>(
                "prompt",
                $"Enter split time between {entry.StartTime} and {entry.EndTime}"
            );

            // If user pressed Cancel or didn't enter anything, abort
            if (string.IsNullOrWhiteSpace(splitTime))
                return;

            // TODO: Optionally validate splitTime is within the range of [StartTime, EndTime]

            int idx = timetable.IndexOf(entry);

            // Create two new rows
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

            // Remove the original row
            timetable.RemoveAt(idx);

            // Insert the two new rows in place
            timetable.Insert(idx, row1);
            timetable.Insert(idx + 1, row2);

            // If there's a row after row2, update its StartTime to row2's EndTime
            if (idx + 2 < timetable.Count)
            {
                timetable[idx + 2].StartTime = row2.EndTime;
            }
        }

        // Export the timetable as a downloadable JSON file.
        protected async Task ExportTimetableAsync()
        {
            // Prompt the user for a file name (default value "timetable")
            var fileName = await JSRuntime.InvokeAsync<string>("prompt", "Enter file name (without extension):", "timetable");

            // If the user cancels or enters an empty name, abort the export.
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            // Ensure the file name ends with .json
            if (!fileName.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".json";
            }

            // Serialize your timetable data to JSON
            var json = JsonSerializer.Serialize(timetable);

            // Call your JavaScript function to trigger the download
            await JSRuntime.InvokeVoidAsync("downloadFile", fileName, json);
        }


        // Handle file selection to import a timetable.
        protected async Task HandleFileSelected(InputFileChangeEventArgs e)
        {
            var file = e.File;
            using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Seek(0, SeekOrigin.Begin);
            var json = await new StreamReader(ms).ReadToEndAsync();
            try
            {
                var importedTimetable = JsonSerializer.Deserialize<List<TimetableEntry>>(json);
                if (importedTimetable != null)
                {
                    timetable = importedTimetable;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing timetable file: " + ex.Message);
            }
        }

        public class TimetableEntry
        {
            public string StartTime { get; set; } = "";
            public string EndTime { get; set; } = "";
            public string Task { get; set; } = "";
            public bool IsEditing { get; set; } = false;
            public bool IsSelected { get; set; } = false;
            public TimetableEntry? EditingBackup { get; set; }
        }
    }
}
