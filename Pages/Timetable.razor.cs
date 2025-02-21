using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System;

namespace BlazorWASM2.Pages
{
    public partial class Timetable
    {
        protected List<TimetableEntry> timetable = new();

        // For the split dialog
        private bool showSplitDialog;
        private TimetableEntry? rowToSplit;
        private string splitTime = "";
        private string splitError = "";

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

        // -- SPLIT ROW DIALOG APPROACH --
        protected void OpenSplitDialog(TimetableEntry entry)
        {
            rowToSplit = entry;
            splitTime = "";
            splitError = "";
            showSplitDialog = true;
        }

        protected void CancelSplit()
        {
            showSplitDialog = false;
            rowToSplit = null;
            splitTime = "";
            splitError = "";
        }

        protected void ConfirmSplitTime()
        {
            if (rowToSplit == null)
            {
                showSplitDialog = false;
                return;
            }

            // Validate that the user actually entered something
            if (string.IsNullOrWhiteSpace(splitTime))
            {
                splitError = "Please enter a split time.";
                return;
            }

            // Parse StartTime / EndTime of the row
            if (!TimeSpan.TryParse(rowToSplit.StartTime, out var startTs) ||
                !TimeSpan.TryParse(rowToSplit.EndTime, out var endTs))
            {
                // If the row's times are un-parseable, show error or abort
                splitError = "Row start/end time is invalid. Cannot split.";
                return;
            }

            // Parse the user's input
            if (!TimeSpan.TryParse(splitTime, out var userTs))
            {
                splitError = "Invalid time format. Use HH:mm (e.g. 06:45).";
                return;
            }

            // Check that userTs is strictly between startTs and endTs
            if (userTs <= startTs || userTs >= endTs)
            {
                splitError = $"Split time must be between {rowToSplit.StartTime} and {rowToSplit.EndTime}.";
                return;
            }

            // Proceed with the actual split
            int idx = timetable.IndexOf(rowToSplit);
            if (idx < 0)
            {
                showSplitDialog = false;
                return;
            }

            var row1 = new TimetableEntry
            {
                StartTime = rowToSplit.StartTime,
                EndTime = splitTime,
                Task = rowToSplit.Task,
                IsEditing = false,
                IsSelected = false
            };

            var row2 = new TimetableEntry
            {
                StartTime = splitTime,
                EndTime = rowToSplit.EndTime,
                Task = rowToSplit.Task,
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

            // Reset dialog
            showSplitDialog = false;
            rowToSplit = null;
            splitTime = "";
            splitError = "";
        }

        protected async Task ExportTimetableAsync()
        {
            var fileName = await JSRuntime.InvokeAsync<string>("prompt", "Enter file name (without extension):", "timetable");
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
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
