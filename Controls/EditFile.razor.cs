namespace Controls;

/// <summary>
/// File upload control. Binds to a <see cref="List{IBrowserFile}"/> — files are added/removed
/// in the list; consumers read the list to stream/upload the selected files themselves.
/// Supports drag-and-drop and click-to-browse, optional extension filtering, per-file size cap,
/// and an optional max-file count. Styled to match the Hatch/Spot drop-zone look.
/// </summary>
public partial class EditFile : EditControlListBase<IBrowserFile>
{
    /// <summary> Expression that binds to the List&lt;IBrowserFile&gt; property on the model.</summary>
    [Parameter] public required Expression<Func<List<IBrowserFile>>> Field { get; set; }

    /// <summary> Accepted file extensions, e.g. <c>".pdf"</c>, <c>".xlsx"</c>. Empty = all types accepted.</summary>
    [Parameter] public string[] AllowedExtensions { get; set; } = [];

    /// <summary> Maximum size in bytes for any single file. Defaults to 10 MB.</summary>
    [Parameter] public long MaxFileSizeBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary> Maximum number of files that may be selected. 0 = unlimited.</summary>
    [Parameter] public int MaxFiles { get; set; } = 0;

    readonly List<string> _uploadErrors = [];
    string _hoverClass = string.Empty;

    bool _hasError => _uploadErrors.Count > 0 || IsInvalid;

    void OnDragEnter(Microsoft.AspNetCore.Components.Web.DragEventArgs _) => _hoverClass = "hover";
    void OnDragLeave(Microsoft.AspNetCore.Components.Web.DragEventArgs _) => _hoverClass = string.Empty;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
    }

    // "10 MB limit", "500 KB limit" — integer MB division reported "0 MB" for sub-MB caps.
    static string FormatSize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes} B",
    };

    async Task LoadFiles(InputFileChangeEventArgs e)
    {
        if (IsDisabled) return;
        _hoverClass = string.Empty;
        _uploadErrors.Clear();

        var incoming = e.GetMultipleFiles(e.FileCount);
        var toAdd = new List<IBrowserFile>();
        var skippedByCap = 0;

        foreach (var file in incoming)
        {
            if (MaxFiles > 0 && (Value?.Count ?? 0) + toAdd.Count >= MaxFiles)
            {
                skippedByCap++;
                continue;
            }

            var ext = Path.GetExtension(file.Name);
            if (AllowedExtensions.Length > 0 &&
                !AllowedExtensions.Any(x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)))
            {
                _uploadErrors.Add($"{file.Name}: unsupported format. Accepted: {string.Join(", ", AllowedExtensions)}.");
                continue;
            }

            if (file.Size > MaxFileSizeBytes)
            {
                _uploadErrors.Add($"{file.Name} exceeds the {FormatSize(MaxFileSizeBytes)} limit.");
                continue;
            }

            toAdd.Add(file);
        }

        // Files silently dropped by the count cap looked like a bug — say so.
        if (skippedByCap > 0)
            _uploadErrors.Add($"Only {MaxFiles} file{(MaxFiles == 1 ? "" : "s")} allowed — {skippedByCap} not added.");

        if (toAdd.Count > 0)
        {
            // A null bound list (model property never initialized) starts fresh instead of throwing.
            Value = Value is null ? toAdd : [.. Value, .. toAdd];
            // Write back to the model (ValueChanged) BEFORE notifying — the validator reads the property
            // live off the model during NotifyFieldChanged, so notifying first validates the stale value.
            await ValueChanged.InvokeAsync(Value);
            EditContext?.NotifyFieldChanged(_fieldIdentifier);
        }
    }

    async Task RemoveFile(IBrowserFile file)
    {
        if (IsDisabled) return;
        var updated = new List<IBrowserFile>(Value);
        updated.Remove(file);
        Value = updated;
        _uploadErrors.Clear();
        // Write back before notifying so validation sees the post-removal list, not the stale one.
        await ValueChanged.InvokeAsync(Value);
        EditContext?.NotifyFieldChanged(_fieldIdentifier);
    }
}
