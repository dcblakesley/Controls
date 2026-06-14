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

    string? _uploadError;
    string _hoverClass = string.Empty;

    bool _hasError => _uploadError != null || IsInvalid;

    void OnDragEnter(Microsoft.AspNetCore.Components.Web.DragEventArgs _) => _hoverClass = "hover";
    void OnDragLeave(Microsoft.AspNetCore.Components.Web.DragEventArgs _) => _hoverClass = string.Empty;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
    }

    async Task LoadFiles(InputFileChangeEventArgs e)
    {
        _hoverClass = string.Empty;
        _uploadError = null;

        var incoming = e.GetMultipleFiles(e.FileCount);
        var toAdd = new List<IBrowserFile>();

        foreach (var file in incoming)
        {
            if (MaxFiles > 0 && Value.Count + toAdd.Count >= MaxFiles)
                break;

            var ext = Path.GetExtension(file.Name);
            if (AllowedExtensions.Length > 0 &&
                !AllowedExtensions.Any(x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)))
            {
                _uploadError = $"Unsupported format. Accepted: {string.Join(", ", AllowedExtensions)}.";
                continue;
            }

            if (file.Size > MaxFileSizeBytes)
            {
                _uploadError = $"{file.Name} exceeds the {MaxFileSizeBytes / (1024 * 1024)} MB limit.";
                continue;
            }

            toAdd.Add(file);
        }

        if (toAdd.Count > 0)
        {
            Value = [.. Value, .. toAdd];
            EditContext?.NotifyFieldChanged(_fieldIdentifier);
            await ValueChanged.InvokeAsync(Value);
        }
    }

    async Task RemoveFile(IBrowserFile file)
    {
        var updated = new List<IBrowserFile>(Value);
        updated.Remove(file);
        Value = updated;
        _uploadError = null;
        EditContext?.NotifyFieldChanged(_fieldIdentifier);
        await ValueChanged.InvokeAsync(Value);
    }
}
