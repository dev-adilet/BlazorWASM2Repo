using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlazorWASM2.Pages
{
    public partial class Timetable
    {
        // Main timetable data
        protected List<TimetableEntry> timetable = new();

        #region Modal State

        // Edit modal state
        private bool showEditDialog;
        private TimetableEntry? rowToEdit;
        private bool isNewRowEdit; // true if editing a newly added row
        private string editStartTime = "";
        private string editEndTime = "";
        private string editTaskName = "";
        private string editError = "";

        // Split modal state
        private bool showSplitDialog;
        private TimetableEntry? rowToSplit;
        private string splitTime = "";
        private string splitError = "";

        // Merge modal state
        private bool showMergeDialog;
        private string mergeTaskName = "";
        private string mergeError = "";
        #endregion

        [Inject]
        private IJSRuntime JSRuntime { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            // Add a default row if none exist
            if (!timetable.Any())
            {
                timetable.Add(new TimetableEntry
                {
                    StartTime = "05:45:00",
                    EndTime = "06:25:00",
                    Task = "Wake up & freshen up",
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
            entry.IsSelected = !entry.IsSelected;
        }

        #region Edit Modal Methods
        protected void OpenEditDialog(TimetableEntry entry, bool isNewRow)
        {
            if (showEditDialog)
                return;

            rowToEdit = entry;
            isNewRowEdit = isNewRow;
            editStartTime = entry.StartTime;
            editEndTime = entry.EndTime;
            editTaskName = entry.Task;
            editError = "";
            showEditDialog = true;
        }

        protected void ConfirmEditDialog()
        {
            if (string.IsNullOrWhiteSpace(editStartTime) || string.IsNullOrWhiteSpace(editEndTime))
            {
                editError = "Start Time and End Time are required.";
                return;
            }
            if (!TimeSpan.TryParse(editStartTime, out var startTs) || !TimeSpan.TryParse(editEndTime, out var endTs))
            {
                editError = "Invalid time format.";
                return;
            }
            if (startTs >= endTs)
            {
                editError = "Start Time must be before End Time.";
                return;
            }

            if (rowToEdit != null)
            {
                rowToEdit.StartTime = editStartTime;
                rowToEdit.EndTime = editEndTime;
                rowToEdit.Task = editTaskName;

                int idx = timetable.IndexOf(rowToEdit);
                if (idx < timetable.Count - 1)
                {
                    timetable[idx + 1].StartTime = editEndTime;
                }
            }
            showEditDialog = false;
            rowToEdit = null;
        }

        protected void CancelEditDialog()
        {
            if (isNewRowEdit && rowToEdit != null)
            {
                timetable.Remove(rowToEdit);
            }
            showEditDialog = false;
            rowToEdit = null;
        }
        #endregion

        protected void AddNewRow()
        {
            // Prevent adding a new row while editing
            if (showEditDialog)
                return;

            string newStartTime = "";
            if (timetable.Count > 0)
            {
                newStartTime = timetable[timetable.Count - 1].EndTime;
            }
            var newEntry = new TimetableEntry
            {
                StartTime = newStartTime,
                EndTime = "",
                Task = "",
                IsSelected = false
            };
            timetable.Add(newEntry);
            OpenEditDialog(newEntry, isNewRow: true);
        }

        #region Merge Modal Methods
        protected void OpenMergeDialog()
        {
            if (timetable.Count(x => x.IsSelected) < 2)
                return;

            mergeTaskName = "";
            mergeError = "";
            showMergeDialog = true;
        }

        protected void CancelMerge()
        {
            showMergeDialog = false;
            mergeTaskName = "";
            mergeError = "";
        }

        protected void ConfirmMerge()
        {
            if (string.IsNullOrWhiteSpace(mergeTaskName))
            {
                mergeError = "Please enter a task name.";
                return;
            }

            var selectedIndices = timetable
                .Select((entry, index) => new { entry, index })
                .Where(x => x.entry.IsSelected)
                .Select(x => x.index)
                .ToList();

            if (selectedIndices.Count < 2)
            {
                mergeError = "Please select at least two rows.";
                return;
            }

            selectedIndices.Sort();
            if (selectedIndices.Last() - selectedIndices.First() != selectedIndices.Count - 1)
            {
                mergeError = "Selected rows must be contiguous.";
                return;
            }

            int firstIndex = selectedIndices.First();
            int lastIndex = selectedIndices.Last();

            var mergedEntry = new TimetableEntry
            {
                StartTime = timetable[firstIndex].StartTime,
                EndTime = timetable[lastIndex].EndTime,
                Task = mergeTaskName,
                IsSelected = false
            };

            timetable.RemoveRange(firstIndex, lastIndex - firstIndex + 1);
            timetable.Insert(firstIndex, mergedEntry);

            if (firstIndex < timetable.Count - 1)
            {
                timetable[firstIndex + 1].StartTime = mergedEntry.EndTime;
            }

            showMergeDialog = false;
            mergeTaskName = "";
            mergeError = "";
        }
        #endregion

        #region Split Modal Methods
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

            if (string.IsNullOrWhiteSpace(splitTime))
            {
                splitError = "Please enter a split time.";
                return;
            }

            if (!TimeSpan.TryParse(rowToSplit.StartTime, out var startTs) ||
                !TimeSpan.TryParse(rowToSplit.EndTime, out var endTs))
            {
                splitError = "Row start/end time is invalid. Cannot split.";
                return;
            }

            if (!TimeSpan.TryParse(splitTime, out var userTs))
            {
                splitError = "Invalid time format. Use HH:mm (e.g. 06:45).";
                return;
            }

            if (userTs <= startTs || userTs >= endTs)
            {
                splitError = $"Split time must be between {rowToSplit.StartTime} and {rowToSplit.EndTime}.";
                return;
            }

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
                IsSelected = false
            };

            var row2 = new TimetableEntry
            {
                StartTime = splitTime,
                EndTime = rowToSplit.EndTime,
                Task = rowToSplit.Task,
                IsSelected = false
            };

            timetable.RemoveAt(idx);
            timetable.Insert(idx, row1);
            timetable.Insert(idx + 1, row2);

            if (idx + 2 < timetable.Count)
            {
                timetable[idx + 2].StartTime = row2.EndTime;
            }

            showSplitDialog = false;
            rowToSplit = null;
            splitTime = "";
            splitError = "";
        }
        #endregion

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

        public string FormatTime(string time)
        {
            if (string.IsNullOrEmpty(time))
                return "";
            return time.Substring(0, Math.Min(time.Length, 5));
        }

        bool ShowActionsColumn => timetable.Any(x => x.IsSelected);

        public class TimetableEntry
        {
            public string StartTime { get; set; } = "";
            public string EndTime { get; set; } = "";
            public string Task { get; set; } = "";
            public bool IsSelected { get; set; }
        }
    }
}
