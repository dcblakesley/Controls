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

    /// <summary>
    /// Accepted file "accept tokens", mirroring the native <c>&lt;input accept&gt;</c>/Ant Design's
    /// <c>accept</c>: each entry is either a bare/dotted extension (<c>"pdf"</c>/<c>".pdf"</c>), a full
    /// MIME type (<c>"application/pdf"</c>), or a MIME wildcard (<c>"image/*"</c>) -- detected by
    /// whether the token contains <c>/</c>. Empty = all types accepted.
    /// </summary>
    [Parameter] public string[] AllowedExtensions { get; set; } = [];

    /// <summary> Visual style: the dashed drag-and-drop card (default), or a compact plain button
    /// (<see cref="EditFileVariant.Button"/>, Ant Design's plain <c>Upload</c> look). </summary>
    [Parameter] public EditFileVariant Variant { get; set; } = EditFileVariant.Dropzone;

    /// <summary> Button text shown when <see cref="Variant"/> is <see cref="EditFileVariant.Button"/>. </summary>
    [Parameter] public string ButtonText { get; set; } = "Select Files";

    /// <summary>
    /// Optional async gate run for each file, after the built-in format/size/count/duplicate checks
    /// and before its bytes are buffered. Return <c>false</c> to reject the file (reported via
    /// <see cref="BeforeAddRejectedMessageFormat"/>) -- e.g. a server-side dedupe check or content
    /// sniffing the cheap built-in checks can't do. An exception propagates uncaught: that's a bug in
    /// the consumer's hook, not a file rejection, so it isn't swallowed into an upload-error message.
    /// </summary>
    [Parameter] public Func<IBrowserFile, Task<bool>>? BeforeAdd { get; set; }

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

    /// <summary> <see cref="BeforeAdd"/> rejection message. {0} = file name.</summary>
    [Parameter] public string BeforeAddRejectedMessageFormat { get; set; } = "{0} was rejected.";

    [Inject] IJSRuntime JS { get; set; } = default!;

    readonly List<string> _uploadErrors = [];
    string _hoverClass = string.Empty;
    // Set by RemoveFile, consumed by OnAfterRenderAsync: the element to focus once the list re-renders
    // (a keyboard remove otherwise drops focus on <body>). Focused by id, so it survives the re-render.
    string? _pendingFocusId;

    // Reentrancy guard for LoadFiles -- see its doc comment for the full rationale. Also gates the
    // rendered <InputFile>'s `disabled` attribute (alongside IsDisabled in the .razor) so a real user
    // can't fire a second change event while a batch is mid-flight.
    bool _isLoadingFiles;

    bool _hasError => _uploadErrors.Count > 0 || IsInvalid;

    // The <InputFile> (and its drop zone) unmounts once the MaxFiles cap is reached — anything that
    // targets the input (the FormLabel's `for`, focus restoration) must gate on this same condition
    // or it points at an element that isn't in the DOM.
    bool CanAddMoreFiles => MaxFiles <= 0 || Value is null || Value.Count < MaxFiles;

    // AllowedExtensions doubles as an accept-token list: a token containing '/' is a MIME type (or
    // MIME wildcard, e.g. "image/*") and is passed through verbatim -- dot-prefixing it (the old,
    // MIME-unaware behavior) turned "image/*" into the meaningless ".image/*", both breaking the
    // `accept` attribute and silently rejecting every file. A bare extension token is still
    // normalized to a leading dot ("pdf" -> ".pdf") -- an easy consumer mistake that otherwise
    // silently rejects every file, since Path.GetExtension always returns the dot.
    static bool IsMimeToken(string token) => token.Contains('/');

    string[] NormalizedExtensions => [.. AllowedExtensions.Select(x => IsMimeToken(x) ? x : (x.StartsWith('.') ? x : $".{x}"))];

    // True when `file` satisfies accept token `token`, whichever of the three shapes it's written in:
    // bare/dotted extension, full MIME type ("application/pdf"), or MIME wildcard ("image/*"). MIME
    // matching reads the browser-reported IBrowserFile.ContentType (not sniffed) case-insensitively.
    static bool MatchesAcceptToken(IBrowserFile file, string token)
    {
        if (!IsMimeToken(token))
        {
            var normalized = token.StartsWith('.') ? token : $".{token}";
            return normalized.Equals(Path.GetExtension(file.Name), StringComparison.OrdinalIgnoreCase);
        }

        if (token.EndsWith("/*", StringComparison.Ordinal))
            return file.ContentType.StartsWith(token[..^1], StringComparison.OrdinalIgnoreCase);

        return token.Equals(file.ContentType, StringComparison.OrdinalIgnoreCase);
    }

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

    /// <summary>
    /// Validates/dedupes/caps the files from one InputFile change event and buffers the accepted ones
    /// into memory (see <see cref="BufferedBrowserFile"/>).
    /// </summary>
    /// <remarks>
    /// <b>Re-entrancy:</b> this method suspends at <see cref="BeforeAdd"/> and again while buffering
    /// each file's bytes -- both yield control back to Blazor's renderer. A second InputFile change
    /// event firing before the first batch finishes would otherwise interleave with it against the
    /// same <c>Value</c>/<c>EditContext</c>: <see cref="MaxFiles"/>/<see cref="MaxTotalBytes"/> are
    /// both checked against a snapshot of <c>Value</c> read at the top of the method, so a second batch
    /// racing the first bypasses both caps, and two concurrent <c>EditContext.NotifyFieldChanged</c>
    /// calls for the same field can throw <see cref="ArgumentException"/>. <see cref="_isLoadingFiles"/>
    /// is checked and set synchronously as the very first statement (no <c>await</c> before the set),
    /// which is a complete guard under Blazor's cooperative single-threaded dispatcher: there is no
    /// true multithreading to race against here, only interleaving at <c>await</c> points, so a
    /// <see cref="SemaphoreSlim"/> (and the <c>IDisposable</c> plumbing it would need) would buy nothing
    /// beyond what a plain bool already guarantees. This is a REJECT strategy, not a queue: a
    /// re-entrant call returns immediately without touching <c>Value</c>, <c>_uploadErrors</c>, or the
    /// <c>EditContext</c> -- it leaves the in-flight batch to finish on its own, and the rejected call's
    /// files are silently dropped (not reported; the rejected batch never ran far enough to know what
    /// it would even report). In practice a real user can't trigger this through the UI: the
    /// &lt;InputFile&gt; is disabled for the duration of the batch (<c>_isLoadingFiles</c> gates
    /// <c>disabled</c> in the .razor alongside <c>IsDisabled</c>), so this guards only against a
    /// synthetic/automated double-fire (two change events dispatched back-to-back before the first
    /// yields a render) -- exactly the hunter's repro.
    /// </remarks>
    async Task LoadFiles(InputFileChangeEventArgs e)
    {
        if (IsDisabled || _isLoadingFiles) return;
        _isLoadingFiles = true;
        try
        {
            await LoadFilesCore(e);
        }
        finally
        {
            _isLoadingFiles = false;
        }
    }

    async Task LoadFilesCore(InputFileChangeEventArgs e)
    {
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

            if (AllowedExtensions.Length > 0 && !AllowedExtensions.Any(t => MatchesAcceptToken(file, t)))
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

            // Last gate before buffering: an optional consumer hook (server-side dedupe, content
            // sniffing) that the cheap built-in checks above can't do. Not caught -- an exception here
            // is a bug in the hook, not a file rejection.
            if (BeforeAdd is not null && !await BeforeAdd(file))
            {
                _uploadErrors.Add(string.Format(CultureInfo.CurrentCulture, BeforeAddRejectedMessageFormat, file.Name));
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
        // Best-effort: no-op if the element is gone or JS is unavailable (prerender / tests) —
        // FocusById swallows interop failures itself. Passing the cascaded FormDefaults lets it
        // resolve a lazy re-import against the right origin if window.WssEditControls is missing
        // (a cross-origin MFE whose host page never linked edit-controls.js as a <script> tag).
        await JsInteropEc.FocusById(JS, id, FormDefaults);
    }
}
