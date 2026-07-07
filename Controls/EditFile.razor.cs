namespace Controls;

/// <summary>
/// File upload control. Binds to a <see cref="List{IBrowserFile}"/> — files are added/removed
/// in the list; consumers read the list to stream/upload the selected files themselves.
/// Supports drag-and-drop and click-to-browse, optional extension filtering, per-file size cap,
/// and an optional max-file count. Styled to match the Hatch/Spot drop-zone look.
/// </summary>
/// <remarks>
/// Each accepted file's bytes are buffered into memory at selection time (see
/// <see cref="BufferedBrowserFile"/>). This is what makes accumulation across multiple picks/drops
/// reliable — the framework wipes the browser file map on every change event, so an un-buffered
/// <c>IBrowserFile</c> from an earlier batch throws on <c>OpenReadStream</c>. Because the bytes are
/// already in memory, consumers may call <c>file.OpenReadStream()</c> with no size argument (the
/// framework's 500&#160;KB default doesn't apply to a buffered file). The trade-off: selected files
/// occupy memory — bounded by <see cref="MaxFileSizeBytes"/> × file count — from pick time until the
/// list is cleared; on Blazor Server the bytes cross the SignalR circuit at selection. Set
/// <see cref="MaxFileSizeBytes"/> and <see cref="MaxFiles"/> to bound that footprint.
/// </remarks>
public partial class EditFile : EditControlListBase<IBrowserFile>
{
    /// <summary> Expression that binds to the List&lt;IBrowserFile&gt; property on the model.</summary>
    [Parameter] public required Expression<Func<List<IBrowserFile>>> Field { get; set; }

    /// <summary> Accepted file extensions, e.g. <c>".pdf"</c>, <c>".xlsx"</c>. Empty = all types accepted.</summary>
    [Parameter] public string[] AllowedExtensions { get; set; } = [];

    /// <summary>
    /// Maximum size in bytes for any single file. Defaults to 10 MB. Also caps the per-file in-memory
    /// buffer (files over this are rejected before any bytes are read), so it doubles as the memory bound.
    /// </summary>
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

            // Buffer the bytes NOW, while the file is still readable. Blazor wipes the browser file map
            // on the next change event and the <InputFile> unmounts once MaxFiles is reached — either
            // makes a stored framework IBrowserFile throw on OpenReadStream. A buffered copy survives
            // both, so accumulation across selection batches actually works (see BufferedBrowserFile).
            // Size is already validated <= MaxFileSizeBytes above, so that's a safe read cap.
            try
            {
                var bytes = new byte[file.Size];
                await using var stream = file.OpenReadStream(MaxFileSizeBytes);
                await stream.ReadExactlyAsync(bytes);
                toAdd.Add(new BufferedBrowserFile(file, bytes));
            }
            catch (Exception) // one unreadable file (I/O, disconnected circuit, size race) mustn't nuke the batch
            {
                _uploadErrors.Add($"{file.Name} could not be read.");
            }
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
