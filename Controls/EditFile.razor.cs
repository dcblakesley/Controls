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
/// occupy memory from pick time until the list is cleared; on Blazor Server the bytes cross the
/// SignalR circuit at selection. The aggregate footprint is bounded by <see cref="MaxTotalBytes"/>
/// (default 100&#160;MB across all selected files), with <see cref="MaxFileSizeBytes"/> and
/// <see cref="MaxFiles"/> bounding the per-file size and the file count.
/// </remarks>
public partial class EditFile : EditControlListBase<IBrowserFile>
{
    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<List<IBrowserFile>>>? Field { get; set; }

    /// <summary> Accepted file extensions, e.g. <c>".pdf"</c>, <c>".xlsx"</c>. Empty = all types accepted.</summary>
    [Parameter] public string[] AllowedExtensions { get; set; } = [];

    /// <summary>
    /// Maximum size in bytes for any single file. Defaults to 10 MB. Also caps the per-file in-memory
    /// buffer (files over this are rejected before any bytes are read), so it doubles as the memory bound.
    /// </summary>
    [Parameter] public long MaxFileSizeBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary> Maximum number of files that may be selected. 0 = unlimited.</summary>
    [Parameter] public int MaxFiles { get; set; } = 0;

    /// <summary>
    /// Maximum total bytes across all selected files (existing plus newly picked). Defaults to 100 MB.
    /// 0 = unlimited. Enforced before each file is buffered, so it bounds the aggregate in-memory
    /// footprint even when every individual file passes <see cref="MaxFileSizeBytes"/>.
    /// </summary>
    [Parameter] public long MaxTotalBytes { get; set; } = 100L * 1024 * 1024;

    // Localizable upload-error messages (string.Format under CurrentCulture, defaults keep today's
    // exact English output — same pattern as the Pagination/Select label parameters). The {n}
    // arguments are documented per parameter; pluralizing formats also receive a pre-pluralized
    // English unit ("file"/"files") as their last argument, which localized formats simply ignore.

    /// <summary> Rejected-extension message. {0} = file name, {1} = comma-joined accepted extensions.</summary>
    [Parameter] public string UnsupportedFormatMessageFormat { get; set; } = "{0}: unsupported format. Accepted: {1}.";

    /// <summary> Per-file size-cap message. {0} = file name, {1} = formatted size limit (e.g. "10 MB").</summary>
    [Parameter] public string FileTooLargeMessageFormat { get; set; } = "{0} exceeds the {1} limit.";

    /// <summary> Per-file read-failure message. {0} = file name.</summary>
    [Parameter] public string FileReadFailedMessageFormat { get; set; } = "{0} could not be read.";

    /// <summary> Duplicate-file message. {0} = file name.</summary>
    [Parameter] public string DuplicateFileMessageFormat { get; set; } = "{0} is already added.";

    /// <summary> Count-cap message. {0} = MaxFiles, {1} = number skipped, {2} = "file"/"files" (English plural of {0} — ignore when localizing).</summary>
    [Parameter] public string MaxFilesMessageFormat { get; set; } = "Only {0} {2} allowed — {1} not added.";

    /// <summary> Aggregate size-cap message. {0} = formatted total limit (e.g. "100 MB"), {1} = number skipped, {2} = "file"/"files" (English plural of {1} — ignore when localizing).</summary>
    [Parameter] public string TotalSizeMessageFormat { get; set; } = "Total size limit of {0} reached — {1} {2} not added.";

    [Inject] IJSRuntime JS { get; set; } = default!;

    readonly List<string> _uploadErrors = [];
    string _hoverClass = string.Empty;
    // Set by RemoveFile, consumed by OnAfterRenderAsync: the element to focus once the list re-renders
    // (a keyboard remove otherwise drops focus on <body>). Focused by id, so it survives the re-render.
    string? _pendingFocusId;

    bool _hasError => _uploadErrors.Count > 0 || IsInvalid;

    // The <InputFile> (and its drop zone) unmounts once the MaxFiles cap is reached — anything that
    // targets the input (the FormLabel's `for`, focus restoration) must gate on this same condition
    // or it points at an element that isn't in the DOM.
    bool CanAddMoreFiles => MaxFiles <= 0 || Value is null || Value.Count < MaxFiles;

    // AllowedExtensions entries are documented dot-prefixed (".pdf"), but a bare "pdf" is an easy
    // consumer mistake that otherwise silently rejects every file (Path.GetExtension always returns
    // the dot) and emits an invalid `accept` attribute — normalize instead of failing.
    string[] NormalizedExtensions => [.. AllowedExtensions.Select(x => x.StartsWith('.') ? x : $".{x}")];

    // Don't light up the drop zone as if it accepts a drop when it doesn't — the drop is refused when
    // disabled, so the hover highlight would be a lie.
    void OnDragEnter(Microsoft.AspNetCore.Components.Web.DragEventArgs _) { if (!IsDisabled) _hoverClass = "hover"; }
    void OnDragLeave(Microsoft.AspNetCore.Components.Web.DragEventArgs _) => _hoverClass = string.Empty;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditFile)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    // "10 MB limit", "500 KB limit" — integer MB division reported "0 MB" for sub-MB caps.
    static string FormatSize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes} B",
    };

    // Name + size + last-modified is the same identity a user would judge by eye and is cheap to
    // compare (no content hashing) — good enough to catch the common case of re-dropping the same
    // file without reading it twice.
    static bool IsSameFile(IBrowserFile a, IBrowserFile b) =>
        a.Size == b.Size && a.LastModified == b.LastModified && a.Name == b.Name;

    async Task LoadFiles(InputFileChangeEventArgs e)
    {
        if (IsDisabled) return;
        _hoverClass = string.Empty;
        _uploadErrors.Clear();

        var incoming = e.GetMultipleFiles(e.FileCount);
        var toAdd = new List<IBrowserFile>();
        var skippedByCap = 0;
        var skippedByTotalCap = 0;
        // Running total of accepted bytes: the already-buffered files plus everything accepted so far this
        // batch. Checked before buffering each file so the aggregate in-memory footprint stays bounded even
        // when every file individually passes MaxFileSizeBytes (a 300-photo drop otherwise buffers unbounded).
        var runningTotal = Value?.Sum(f => f.Size) ?? 0;

        foreach (var file in incoming)
        {
            // Skip-and-report, same as every other rejection below: the same file picked twice
            // (re-drag, or picking an already-added file from a fresh browse) otherwise occupies two
            // MaxFiles/MaxTotalBytes slots for one logical file. Checked against both the already-held
            // Value and this batch's own toAdd, so duplicates within a single drop are caught too.
            if ((Value?.Exists(v => IsSameFile(v, file)) ?? false) || toAdd.Exists(v => IsSameFile(v, file)))
            {
                _uploadErrors.Add(string.Format(CultureInfo.CurrentCulture, DuplicateFileMessageFormat, file.Name));
                continue;
            }

            if (MaxFiles > 0 && (Value?.Count ?? 0) + toAdd.Count >= MaxFiles)
            {
                skippedByCap++;
                continue;
            }

            var ext = Path.GetExtension(file.Name);
            if (AllowedExtensions.Length > 0 &&
                !NormalizedExtensions.Any(x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)))
            {
                _uploadErrors.Add(string.Format(CultureInfo.CurrentCulture,
                    UnsupportedFormatMessageFormat, file.Name, string.Join(", ", AllowedExtensions)));
                continue;
            }

            if (file.Size > MaxFileSizeBytes)
            {
                _uploadErrors.Add(string.Format(CultureInfo.CurrentCulture,
                    FileTooLargeMessageFormat, file.Name, FormatSize(MaxFileSizeBytes)));
                continue;
            }

            // Aggregate cap: skip (don't buffer) an otherwise-valid file that would push the total over
            // MaxTotalBytes. Counted like the count cap and reported once after the loop.
            if (MaxTotalBytes > 0 && runningTotal + file.Size > MaxTotalBytes)
            {
                skippedByTotalCap++;
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
                runningTotal += file.Size; // only count bytes that actually got buffered
            }
            catch (Exception) // one unreadable file (I/O, disconnected circuit, size race) mustn't nuke the batch
            {
                _uploadErrors.Add(string.Format(CultureInfo.CurrentCulture,
                    FileReadFailedMessageFormat, file.Name));
            }
        }

        // Files silently dropped by the count cap looked like a bug — say so.
        if (skippedByCap > 0)
            _uploadErrors.Add(string.Format(CultureInfo.CurrentCulture,
                MaxFilesMessageFormat, MaxFiles, skippedByCap, MaxFiles == 1 ? "file" : "files"));

        // Same voice as the count cap: one aggregate line for everything the total-size cap turned away.
        if (skippedByTotalCap > 0)
            _uploadErrors.Add(string.Format(CultureInfo.CurrentCulture,
                TotalSizeMessageFormat, FormatSize(MaxTotalBytes), skippedByTotalCap, skippedByTotalCap == 1 ? "file" : "files"));

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
        var removedIndex = updated.IndexOf(file);
        updated.Remove(file);
        Value = updated;
        _uploadErrors.Clear();

        // Keep keyboard focus on the control after the delete button vanishes: focus the file that
        // shifted into this slot, else the new last file, else the drop zone's file input. Consumed
        // in OnAfterRenderAsync once the new list has rendered.
        _pendingFocusId = updated.Count == 0
            ? _id
            : $"del-{_id}-{Math.Min(removedIndex, updated.Count - 1)}";

        // Write back before notifying so validation sees the post-removal list, not the stale one.
        await ValueChanged.InvokeAsync(Value);
        EditContext?.NotifyFieldChanged(_fieldIdentifier);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_pendingFocusId is null) return;
        var id = _pendingFocusId;
        _pendingFocusId = null;
        // Best-effort: no-op if the element is gone or JS is unavailable (prerender / tests).
        try { await JsInteropEc.FocusById(JS, id); } catch { /* focus is a nicety, never fatal */ }
    }
}
