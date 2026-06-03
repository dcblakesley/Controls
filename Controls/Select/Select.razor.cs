using System.Threading;
using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// A self-contained Select / combo box with no dependency on Ant Design.
/// Supports single, multiple and tags modes, type-to-search, clear, keyboard
/// navigation and an Ant-Design-like appearance (see Select.razor.css).
/// </summary>
/// <remarks>
/// This is the general-purpose engine. For form binding (validation, label, read-only,
/// FormOptions) use <see cref="EditSelectSearch{TValue}"/> (single) or
/// <see cref="EditMultiSelect{TValue}"/> (multiple/tags), which wrap this control.
/// </remarks>
/// <typeparam name="TValue">The type of each option value.</typeparam>
public partial class Select<TValue> : IAsyncDisposable
{
    static readonly IEqualityComparer<TValue> _comparer = EqualityComparer<TValue>.Default;

    // Pixel height of a dropdown row; shared by Virtualize and scroll-into-view.
    const float RowHeight = 32;

    [Inject] IJSRuntime JS { get; set; } = default!;

    IJSObjectReference? _jsModule;
    ElementReference _inputRef;
    ElementReference _dropdownRef;
    ElementReference _wrapperRef;
    CancellationTokenSource? _debounceCts;
    bool _open;
    bool _dropdownPositioned;
    bool _focused;
    string _searchText = string.Empty;
    int _activeIndex;

    // Type-ahead state for non-searchable selects (jump-to-option by typed letters).
    string _typeAheadBuffer = string.Empty;
    DateTime _lastTypeAheadUtc;

    // Working copies. For multiple/tags the bound collection is mirrored here so we
    // can add user-created tags without mutating the caller's instance.
    // _selected keeps display order; _selectedSet gives O(1) "is this selected?".
    readonly List<TValue> _selected = [];
    readonly HashSet<TValue> _selectedSet = new(_comparer);
    readonly List<SelectOption<TValue>> _tagOptions = [];
    List<SelectOption<TValue>> _filtered = [];
    int _hiddenTagCount;
    readonly List<(SelectOption<TValue> Option, int Index)> _visibleTags = [];

    // value -> option, rebuilt only when Options changes, so FindOption is O(1)
    // instead of scanning the (potentially huge) option list on every render.
    // Null option values are filtered before insertion (RebuildLookup) and guarded on
    // lookup (FindOption), so the dictionary never holds a null key — suppress the
    // notnull-constraint warning to keep TValue unconstrained (e.g. nullable-enum options).
#pragma warning disable CS8714
    Dictionary<TValue, SelectOption<TValue>> _lookup = new(_comparer);
#pragma warning restore CS8714
    IEnumerable<SelectOption<TValue>>? _lastOptions;

    // ----- Parameters -------------------------------------------------------

    [Parameter] public SelectMode Mode { get; set; } = SelectMode.Single;

    [Parameter] public IEnumerable<SelectOption<TValue>> Options { get; set; } = [];

    /// <summary>Bound value for <see cref="SelectMode.Single"/> mode.</summary>
    [Parameter] public TValue Value { get; set; } = default!;
    [Parameter] public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>Bound values for <see cref="SelectMode.Multiple"/> / <see cref="SelectMode.Tags"/> modes.</summary>
    [Parameter] public IEnumerable<TValue>? Values { get; set; }
    [Parameter] public EventCallback<IEnumerable<TValue>> ValuesChanged { get; set; }

    [Parameter] public string Placeholder { get; set; } = "Please select";
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool AllowClear { get; set; } = true;
    [Parameter] public bool ShowSearch { get; set; } = true;
    [Parameter] public SelectSize Size { get; set; } = SelectSize.Default;
    [Parameter] public string? Width { get; set; }
    [Parameter] public int? MaxTagCount { get; set; }
    [Parameter] public string EmptyText { get; set; } = "No data";

    /// <summary>Render the dropdown open on first display.</summary>
    [Parameter] public bool DefaultOpen { get; set; }

    /// <summary>Raised with the current search text whenever it changes.</summary>
    [Parameter] public EventCallback<string> OnSearch { get; set; }

    /// <summary>When &gt; 0, wait this many milliseconds after the last keystroke before
    /// filtering. Keeps typing responsive when filtering a large or expensive option set.</summary>
    [Parameter] public int DebounceMilliseconds { get; set; }

    /// <summary>Tags mode only: turn typed text into a <typeparamref name="TValue"/>. When null and
    /// <typeparamref name="TValue"/> is <see cref="string"/>, the text is used directly.</summary>
    [Parameter] public Func<string, TValue>? TagValueFactory { get; set; }

    // ----- Form-integration pass-throughs (set by the Edit* wrappers) -------

    /// <summary>HTML id applied to the search input — wires the form label/validation/test hooks.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra classes appended to the wrapper (e.g. Blazor's field "valid"/"invalid"/"modified").</summary>
    [Parameter] public string? CssClass { get; set; }

    /// <summary>Value for the search input's <c>aria-required</c> attribute ("true"/"false").</summary>
    [Parameter] public string? AriaRequired { get; set; }

    /// <summary>When true, marks the search input <c>aria-invalid</c>.</summary>
    [Parameter] public bool AriaInvalid { get; set; }

    /// <summary>Value for the search input's <c>aria-describedby</c> attribute.</summary>
    [Parameter] public string? AriaDescribedBy { get; set; }

    bool IsMultiple => Mode != SelectMode.Single;

    // ----- Inline icons (no icon-font / Ant dependency) ---------------------

    static readonly MarkupString ArrowDown = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M884 256h-75c-5.1 0-9.9 2.5-12.9 6.6L512 654.2 227.9 262.6c-3-4.1-7.8-6.6-12.9-6.6h-75c-6.5 0-10.3 7.4-6.5 12.7l352.6 486.1c12.8 17.6 39 17.6 51.7 0l352.6-486.1c3.9-5.3.1-12.7-6.4-12.7z\"/></svg>");

    static readonly MarkupString CheckMark = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M912 190h-69.9c-9.8 0-19.1 4.5-25.1 12.2L404.7 724.5 207 474a32 32 0 00-25.1-12.2H112c-6.7 0-10.4 7.7-6.3 12.9l273.9 347c12.8 16.2 37.4 16.2 50.3 0l488.4-618.9c4.1-5.1.4-12.8-6.3-12.8z\"/></svg>");

    static readonly MarkupString CloseCross = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M563.8 512l262.5-312.9c4.4-5.2.7-13.1-6.1-13.1h-79.8c-4.7 0-9.2 2.1-12.3 5.7L511.6 449.8 295.1 191.7c-3-3.6-7.5-5.7-12.3-5.7H203c-6.8 0-10.5 7.9-6.1 13.1L459.4 512 196.9 824.9A7.95 7.95 0 00203 838h79.8c4.7 0 9.2-2.1 12.3-5.7l216.5-258.1 216.5 258.1c3 3.6 7.5 5.7 12.3 5.7h79.8c6.8 0 10.5-7.9 6.1-13.1L563.8 512z\"/></svg>");

    // ----- Lifecycle --------------------------------------------------------

    protected override void OnInitialized()
    {
        _open = DefaultOpen;
    }

    protected override void OnParametersSet()
    {
        // Rebuild the value->option lookup only when the Options instance changes,
        // not on every parameter set (cheap for huge lists).
        if (!ReferenceEquals(Options, _lastOptions))
        {
            RebuildLookup();
            _lastOptions = Options;
        }

        if (IsMultiple)
        {
            SetSelected(Values);
        }

        RebuildFiltered();
        RebuildVisibleTags();
    }

    // ----- Display helpers (used by the .razor markup) ----------------------

    string WrapperClass
    {
        get
        {
            var classes = new List<string> { "wss-select", IsMultiple ? "wss-select-multiple" : "wss-select-single" };
            if (_open) classes.Add("wss-select-open");
            if (_focused) classes.Add("wss-select-focused");
            if (Disabled) classes.Add("wss-select-disabled");
            if (ShowSearch) classes.Add("wss-select-show-search");
            if (Size == SelectSize.Small) classes.Add("wss-select-sm");
            if (Size == SelectSize.Large) classes.Add("wss-select-lg");
            if (!string.IsNullOrEmpty(CssClass)) classes.Add(CssClass);
            return string.Join(" ", classes);
        }
    }

    string? WidthStyle => string.IsNullOrEmpty(Width) ? null : $"width:{Width};";

    // Stable id root for ARIA wiring (the listbox + the active option). Uses the supplied Id when
    // present (the Edit* wrappers pass one), otherwise a generated one so standalone use still works.
    string? _generatedId;
    string BaseId => !string.IsNullOrEmpty(Id) ? Id : (_generatedId ??= $"wss-select-{Guid.NewGuid():N}");

    bool _showPlaceholder => IsMultiple
        ? _selected.Count == 0 && string.IsNullOrEmpty(_searchText)
        : !HasSingleValue && string.IsNullOrEmpty(_searchText);

    bool HasSingleValue => !_comparer.Equals(Value, default!);

    string SelectedLabel => FindOption(Value)?.Label ?? Value?.ToString() ?? string.Empty;

    bool ShowClear => AllowClear && !Disabled &&
        (IsMultiple ? _selected.Count > 0 : HasSingleValue);

    // Returns the cached visible-tag list (rebuilt by RebuildVisibleTags when the selection changes).
    IReadOnlyList<(SelectOption<TValue> Option, int Index)> VisibleTags() => _visibleTags;

    // Rebuilt only when the selection (or MaxTagCount) changes, not on every render — caching avoids
    // per-render List/tuple/SelectOption allocations and keeps _hiddenTagCount out of a render getter.
    void RebuildVisibleTags()
    {
        _visibleTags.Clear();
        var max = MaxTagCount is { } m && m >= 0 ? m : int.MaxValue;
        _hiddenTagCount = _selected.Count > max ? _selected.Count - max : 0;
        var visible = Math.Min(_selected.Count, max);
        for (var i = 0; i < visible; i++)
        {
            var v = _selected[i];
            _visibleTags.Add((FindOption(v) ?? new SelectOption<TValue>(v, v?.ToString()), i));
        }
    }

    // The keyboard-highlighted option (compared by reference so each rendered row
    // is an O(1) check rather than needing its index in the filtered list).
    SelectOption<TValue>? ActiveOption =>
        _activeIndex >= 0 && _activeIndex < _filtered.Count ? _filtered[_activeIndex] : null;

    string OptionClass(SelectOption<TValue> option)
    {
        var cls = "wss-select-item wss-select-item-option";
        if (option.Disabled) cls += " wss-select-item-option-disabled";
        if (IsSelected(option.Value)) cls += " wss-select-item-option-selected";
        if (ReferenceEquals(option, ActiveOption)) cls += " wss-select-item-option-active";
        return cls;
    }

    bool IsSelected(TValue value) =>
        IsMultiple ? _selectedSet.Contains(value) : _comparer.Equals(Value, value);

    // ----- Option lookup / filtering ----------------------------------------

    IEnumerable<SelectOption<TValue>> AllOptions =>
        (Options ?? []).Concat(_tagOptions);

    SelectOption<TValue>? FindOption(TValue value) =>
        value is not null && _lookup.TryGetValue(value, out var option) ? option : null;

    void RebuildLookup()
    {
#pragma warning disable CS8714
        _lookup = new Dictionary<TValue, SelectOption<TValue>>(_comparer);
#pragma warning restore CS8714
        foreach (var o in Options ?? [])
        {
            if (o.Value is not null) _lookup[o.Value] = o;
        }
        foreach (var o in _tagOptions)
        {
            if (o.Value is not null) _lookup[o.Value] = o;
        }
    }

    void RebuildFiltered()
    {
        IEnumerable<SelectOption<TValue>> source = AllOptions;

        if (!string.IsNullOrEmpty(_searchText))
        {
            source = source.Where(o =>
                (o.Label ?? string.Empty).Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        _filtered = source.ToList();
        if (_activeIndex >= _filtered.Count) _activeIndex = _filtered.Count - 1;
        if (_activeIndex < 0) _activeIndex = 0;
    }

    // ----- Selection bookkeeping (keeps _selected list and _selectedSet in sync) ----

    void SetSelected(IEnumerable<TValue>? values)
    {
        _selected.Clear();
        _selectedSet.Clear();
        if (values is null) return;
        foreach (var v in values)
        {
            if (_selectedSet.Add(v)) _selected.Add(v);
        }
    }

    void AddSelected(TValue value)
    {
        if (_selectedSet.Add(value)) _selected.Add(value);
        RebuildVisibleTags();
    }

    void RemoveSelected(TValue value)
    {
        if (_selectedSet.Remove(value)) _selected.RemoveAll(v => _comparer.Equals(v, value));
        RebuildVisibleTags();
    }

    void ClearSelected()
    {
        _selected.Clear();
        _selectedSet.Clear();
        RebuildVisibleTags();
    }

    // ----- Interaction ------------------------------------------------------

    async Task OnWrapperClickAsync()
    {
        if (Disabled) return;

        if (!_open)
        {
            await OpenAsync();
        }
        else if (!ShowSearch)
        {
            // A plain (non-searchable) select toggles closed on a second click.
            await CloseAsync();
        }
    }

    async Task OpenAsync()
    {
        _open = true;
        _focused = true;
        _activeIndex = 0;
        RebuildFiltered();
        await FocusInputAsync();
    }

    Task CloseAsync()
    {
        _open = false;
        _focused = false;
        _searchText = string.Empty;
        RebuildFiltered();
        // No StateHasChanged: every caller (wrapper/option/backdrop click, Escape keydown) is an
        // event handler, after which Blazor re-renders automatically.
        return Task.CompletedTask;
    }

    async Task OnInputAsync(ChangeEventArgs e)
    {
        // Update the text immediately so the input stays responsive...
        _searchText = e.Value?.ToString() ?? string.Empty;
        if (!_open) await OpenAsync();

        if (DebounceMilliseconds <= 0)
        {
            await ApplySearchAsync();
            return;
        }

        // ...but defer the (potentially expensive) filtering until typing pauses.
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        try
        {
            await Task.Delay(DebounceMilliseconds, token);
        }
        catch (TaskCanceledException)
        {
            return; // superseded by a newer keystroke
        }

        await ApplySearchAsync();
        StateHasChanged();
    }

    async Task ApplySearchAsync()
    {
        _activeIndex = 0;
        RebuildFiltered();
        if (OnSearch.HasDelegate) await OnSearch.InvokeAsync(_searchText);
    }

    async Task SelectAsync(SelectOption<TValue> option)
    {
        if (option.Disabled) return;

        if (IsMultiple)
        {
            if (_selectedSet.Contains(option.Value))
            {
                RemoveSelected(option.Value);
            }
            else
            {
                AddSelected(option.Value);
            }

            _searchText = string.Empty;
            RebuildFiltered();
            await ValuesChanged.InvokeAsync(_selected.ToList());
            await FocusInputAsync();
        }
        else
        {
            Value = option.Value;
            await ValueChanged.InvokeAsync(Value);
            await CloseAsync();
        }
    }

    async Task RemoveAsync(TValue value)
    {
        if (Disabled) return;
        RemoveSelected(value);
        await ValuesChanged.InvokeAsync(_selected.ToList());
    }

    async Task ClearAsync()
    {
        if (IsMultiple)
        {
            ClearSelected();
            await ValuesChanged.InvokeAsync(_selected.ToList());
        }
        else
        {
            Value = default!;
            await ValueChanged.InvokeAsync(Value);
        }

        _searchText = string.Empty;
        RebuildFiltered();
    }

    async Task CommitTagAsync()
    {
        var text = _searchText.Trim();
        if (text.Length == 0) return;

        TValue value;
        if (TagValueFactory is not null)
        {
            value = TagValueFactory(text);
        }
        else if (typeof(TValue) == typeof(string))
        {
            value = (TValue)(object)text;
        }
        else
        {
            // Can't turn free text into a non-string TValue without a TagValueFactory — ignore the
            // keystroke rather than throw an InvalidCastException.
            return;
        }

        if (FindOption(value) is null)
        {
            var tag = new SelectOption<TValue>(value, text);
            _tagOptions.Add(tag);
            if (value is not null) _lookup[value] = tag;
        }

        if (!_selectedSet.Contains(value))
        {
            AddSelected(value);
            await ValuesChanged.InvokeAsync(_selected.ToList());
        }

        _searchText = string.Empty;
        RebuildFiltered();
        await FocusInputAsync();
    }

    async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowDown":
                if (!_open) { await OpenAsync(); return; }
                MoveActive(1);
                await ScrollActiveIntoViewAsync();
                break;

            case "ArrowUp":
                if (!_open) { await OpenAsync(); return; }
                MoveActive(-1);
                await ScrollActiveIntoViewAsync();
                break;

            case "Enter":
                if (_open && _activeIndex >= 0 && _activeIndex < _filtered.Count)
                {
                    await SelectAsync(_filtered[_activeIndex]);
                }
                else if (Mode == SelectMode.Tags && !string.IsNullOrWhiteSpace(_searchText))
                {
                    await CommitTagAsync();
                }
                break;

            case "Escape":
                if (_open) await CloseAsync();
                break;

            case "Backspace":
                if (IsMultiple && string.IsNullOrEmpty(_searchText) && _selected.Count > 0)
                {
                    await RemoveAsync(_selected[^1]);
                }
                break;

            case "Home":
                if (_open) { MoveActiveTo(0, 1); await ScrollActiveIntoViewAsync(); }
                break;

            case "End":
                if (_open) { MoveActiveTo(_filtered.Count - 1, -1); await ScrollActiveIntoViewAsync(); }
                break;

            default:
                // Type-ahead for non-searchable selects: jump to an option by typed letters.
                // (When ShowSearch is on, the same keystrokes filter the list through the input.)
                if (!ShowSearch && e.Key.Length == 1 && char.IsLetterOrDigit(e.Key[0]))
                {
                    if (!_open) await OpenAsync();
                    TypeAhead(e.Key);
                    await ScrollActiveIntoViewAsync();
                }
                break;
        }
    }

    void MoveActive(int delta)
    {
        if (_filtered.Count == 0) return;

        var next = _activeIndex;
        for (var i = 0; i < _filtered.Count; i++)
        {
            next = (next + delta + _filtered.Count) % _filtered.Count;
            if (!_filtered[next].Disabled)
            {
                _activeIndex = next;
                return;
            }
        }
    }

    // Move the highlight to the first non-disabled option at/after `start`, stepping by `direction`.
    void MoveActiveTo(int start, int direction)
    {
        if (_filtered.Count == 0) return;
        var i = Math.Clamp(start, 0, _filtered.Count - 1);
        while (i >= 0 && i < _filtered.Count)
        {
            if (!_filtered[i].Disabled) { _activeIndex = i; return; }
            i += direction;
        }
    }

    void TypeAhead(string ch)
    {
        var now = DateTime.UtcNow;
        // Native <select> resets the buffer after a short pause between keystrokes.
        if ((now - _lastTypeAheadUtc).TotalMilliseconds > 1000) _typeAheadBuffer = string.Empty;
        _lastTypeAheadUtc = now;
        _typeAheadBuffer += ch;

        var firstChar = char.ToUpperInvariant(_typeAheadBuffer[0]);
        var allSame = _typeAheadBuffer.All(c => char.ToUpperInvariant(c) == firstChar);

        // Repeating the same key cycles through the options that start with that letter; any other
        // sequence matches the accumulated prefix from the top of the list.
        var match = _typeAheadBuffer.Length > 1 && allSame
            ? FindLabelPrefix(ch, _activeIndex + 1)
            : FindLabelPrefix(_typeAheadBuffer, 0);
        if (match >= 0) _activeIndex = match;
    }

    int FindLabelPrefix(string prefix, int startFrom)
    {
        for (var i = 0; i < _filtered.Count; i++)
        {
            var idx = (startFrom + i) % _filtered.Count;
            var o = _filtered[idx];
            if (!o.Disabled && (o.Label ?? string.Empty).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return idx;
        }
        return -1;
    }

    async Task FocusInputAsync()
    {
        try
        {
            await _inputRef.FocusAsync();
        }
        catch
        {
            // Focus can fail if the element is not yet rendered; harmless for the prototype.
        }
    }

    // Keeps the keyboard-highlighted row visible in the virtualized dropdown.
    // Uses a tiny RCL-local JS module; degrades to a no-op when JS isn't available
    // (e.g. server prerender or unit tests).
    async Task ScrollActiveIntoViewAsync()
    {
        if (_activeIndex < 0) return;
        try
        {
            _jsModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/WssBlazorControls/wss-select.js");
            await _jsModule.InvokeVoidAsync("scrollActiveIntoView", _dropdownRef, _activeIndex, RowHeight);
        }
        catch
        {
            // No JS runtime / module (prerender, tests) — highlight still works, just no auto-scroll.
        }
    }

    // After the dropdown opens, decide whether it should flip above the control (when it would
    // otherwise run off the bottom of the viewport), then reveal it. The panel renders hidden
    // (wss-measuring) for one frame so the flip is never visible. Degrades to the default downward
    // CSS placement when JS isn't available (server prerender, unit tests).
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_open && !_dropdownPositioned)
        {
            try
            {
                _jsModule ??= await JS.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/WssBlazorControls/wss-select.js");
                await _jsModule.InvokeVoidAsync("placeDropdown", _wrapperRef, _dropdownRef, 4);
            }
            catch
            {
                // No JS runtime / module — keep the CSS default (downward) placement.
            }
            _dropdownPositioned = true;
            StateHasChanged(); // reveal now that it's positioned (drops wss-measuring)
        }
        else if (!_open && _dropdownPositioned)
        {
            _dropdownPositioned = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();

        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.DisposeAsync();
            }
            catch
            {
                // Circuit may already be gone; nothing to clean up.
            }
            _jsModule = null;
        }
    }
}
