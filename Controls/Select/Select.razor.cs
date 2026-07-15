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
    // True while a debounced search has updated _searchText but not yet rebuilt _filtered. Keyboard
    // navigation/selection flushes it first so it never acts on the pre-keystroke filtered list.
    bool _searchPending;
    bool _open;
    bool _dropdownPositioned;
    // The open-order z-index placeDropdown assigned this wrapper (null while closed). C# owns it so a
    // Blazor re-render of the wrapper's bound `style` (e.g. a changed Width) re-asserts it instead of
    // clobbering the value JS wrote to the DOM (see WidthStyle). Set once per open (placeDropdown fires
    // once, guarded by _dropdownPositioned) and cleared on every close path.
    int? _openZIndex;
    // Set first thing in DisposeAsync so an import that completes after disposal disposes its module
    // instead of stranding it on a dead instance (see GetJsModuleAsync).
    bool _disposed;
    bool _inputWired;
    SelectMode _wiredMode;
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

    /// <summary>Selection mode: single value, multiple values, or tags (multiple + free-text entries). Defaults to <see cref="SelectMode.Single"/>.</summary>
    [Parameter] public SelectMode Mode { get; set; } = SelectMode.Single;

    /// <summary>The selectable options. Treated as an immutable parameter: reassign a new collection to refresh — mutating the same instance in place is not seen.</summary>
    [Parameter] public IEnumerable<SelectOption<TValue>> Options { get; set; } = [];

    /// <summary>Bound value for <see cref="SelectMode.Single"/> mode.</summary>
    [Parameter] public TValue Value { get; set; } = default!;
    /// <summary>Raised with the new value when the single-mode selection changes (supports <c>@bind-Value</c>).</summary>
    [Parameter] public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>Bound values for <see cref="SelectMode.Multiple"/> / <see cref="SelectMode.Tags"/> modes.</summary>
    [Parameter] public IEnumerable<TValue>? Values { get; set; }
    /// <summary>Raised with the new selection when the multiple/tags-mode values change (supports <c>@bind-Values</c>).</summary>
    [Parameter] public EventCallback<IEnumerable<TValue>> ValuesChanged { get; set; }

    /// <summary>Text shown while nothing is selected. Defaults to "Please select".</summary>
    [Parameter] public string Placeholder { get; set; } = "Please select";
    /// <summary>Disables all interaction (the dropdown cannot open and tags cannot be removed).</summary>
    [Parameter] public bool Disabled { get; set; }
    /// <summary>Shows a clear button while a value is selected. Defaults to true.</summary>
    [Parameter] public bool AllowClear { get; set; } = true;
    /// <summary>Enables type-to-search filtering of the options. Defaults to true; when false, typed letters jump to matching options instead (native-select-style type-ahead).</summary>
    [Parameter] public bool ShowSearch { get; set; } = true;
    /// <summary>Control height/padding variant. Defaults to <see cref="SelectSize.Default"/>.</summary>
    [Parameter] public SelectSize Size { get; set; } = SelectSize.Default;
    /// <summary>Trigger appearance. <see cref="SelectVariant.Pill"/> renders the filter-button pill;
    /// defaults to <see cref="SelectVariant.Outlined"/> (the classic bordered box).</summary>
    [Parameter] public SelectVariant Variant { get; set; } = SelectVariant.Outlined;
    /// <summary>Optional content rendered at the start of the selector (before the value/search),
    /// typically a decorative icon (mark it <c>aria-hidden</c>). Works in every mode/variant.</summary>
    [Parameter] public RenderFragment? Prefix { get; set; }
    /// <summary>Control width as a CSS length (e.g. "240px", "100%"). Null (default) keeps the stylesheet width.</summary>
    [Parameter] public string? Width { get; set; }
    /// <summary>Multiple/tags modes: maximum number of selected tags to display; the remainder collapses into a "+ n ..." summary tag. Null (default) shows all.</summary>
    [Parameter] public int? MaxTagCount { get; set; }
    /// <summary>Text shown in the dropdown when no options match. Defaults to "No data".</summary>
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

    IEnumerable<TValue>? _lastValues;
    int? _lastMaxTagCount;
    SelectMode _lastMode;

    protected override void OnParametersSet()
    {
        // Reference-guarded rebuilds: a parent re-render re-parameterizes this component on every
        // keystroke elsewhere in the form (delegate parameters defeat Blazor's change skip), and
        // the advertised use-case is tens of thousands of options — so the O(n) mirror/filter work
        // only runs when its actual inputs change. Consequence: Options and Values are immutable
        // parameters — reassign a new instance to refresh, don't mutate in place.
        var optionsChanged = !ReferenceEquals(Options, _lastOptions);
        if (optionsChanged)
        {
            _lastOptions = Options;
            RebuildLookup();
            RebuildFiltered();
        }

        var modeChanged = Mode != _lastMode;
        _lastMode = Mode;

        if (IsMultiple && (optionsChanged || modeChanged || !ReferenceEquals(Values, _lastValues)))
        {
            _lastValues = Values;
            SetSelected(Values);
            RebuildVisibleTags();
        }
        else if (MaxTagCount != _lastMaxTagCount)
        {
            RebuildVisibleTags();
        }
        _lastMaxTagCount = MaxTagCount;
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
            if (Variant == SelectVariant.Pill) classes.Add("wss-select-pill");
            if (!string.IsNullOrEmpty(CssClass)) classes.Add(CssClass);
            return string.Join(" ", classes);
        }
    }

    // The wrapper's inline style. While the dropdown is open, C# owns the stack z-index (mirrored from
    // placeDropdown's return value) and appends it here — so a mid-open re-render (e.g. a changed Width)
    // re-emits a style attribute that still carries the z-index instead of dropping the wrapper below its
    // own full-screen backdrop. The z is written twice, by JS immediately on open and by Blazor on the
    // next diff, and both agree — that agreement is the point. Cleared on close, so a closed wrapper
    // emits no z (a stale high z would otherwise poke through later overlays' masks).
    string? WidthStyle
    {
        get
        {
            var width = string.IsNullOrEmpty(Width) ? null : $"width:{Width};";
            return _openZIndex is null ? width : $"{width}z-index:{_openZIndex};";
        }
    }

    // Stable id root for ARIA wiring (the listbox + the active option). Uses the supplied Id when
    // present (the Edit* wrappers pass one), otherwise a generated one so standalone use still works.
    string? _generatedId;
    string BaseId => !string.IsNullOrEmpty(Id) ? Id : (_generatedId ??= $"wss-select-{Guid.NewGuid():N}");

    bool _showPlaceholder => IsMultiple
        ? _selected.Count == 0 && string.IsNullOrEmpty(_searchText)
        : !HasSingleValue && string.IsNullOrEmpty(_searchText);

    // "A value is selected" = it resolves to an option, OR it's a non-default value. The FindOption
    // arm matters for value types whose default is a real option (e.g. a non-nullable enum's 0 member
    // or int 0) — without it the default would mis-render as the empty placeholder with no clear button.
    bool HasSingleValue => FindOption(Value) is not null || !_comparer.Equals(Value, default!);

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
        RebuildFiltered();
        SetInitialActive();
        await FocusInputAsync();
    }

    // Highlight the current selection when the dropdown opens (falling back to the first enabled
    // option, never a disabled one) — so a long list opens at the user's value instead of the top,
    // and Enter always has a selectable target.
    void SetInitialActive()
    {
        _activeIndex = 0;
        var selectedIdx = IsMultiple
            ? (_selected.Count > 0 ? _filtered.FindIndex(o => !o.Disabled && _selectedSet.Contains(o.Value)) : -1)
            : (HasSingleValue ? _filtered.FindIndex(o => !o.Disabled && _comparer.Equals(o.Value, Value)) : -1);
        if (selectedIdx >= 0)
        {
            _activeIndex = selectedIdx;
            return;
        }
        MoveActiveTo(0, 1);
    }

    Task CloseAsync()
    {
        _open = false;
        _focused = false;
        _searchText = string.Empty;
        // Give up the C#-owned open z-index: this is the sole logical close path, so clearing it here
        // makes the very next (close) render drop the z from the bound style (the OnAfterRender close
        // branch also nulls it + runs clearZ as the DOM-side teardown). A reopen re-takes a fresh z.
        _openZIndex = null;
        // Drop any in-flight debounced search so it can't re-fire against the now-closed dropdown.
        _debounceCts?.Cancel();
        _searchPending = false;
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
        _searchPending = true;
        try
        {
            await Task.Delay(DebounceMilliseconds, token);
        }
        catch (TaskCanceledException)
        {
            return; // superseded by a newer keystroke (or flushed by a keyboard action)
        }

        // The delay can complete just before a selection/close cancels the CTS — without this
        // check the stale continuation would re-run the search (firing OnSearch("") against the
        // now-cleared text) after the dropdown already closed.
        if (token.IsCancellationRequested) return;

        await ApplySearchAsync();
        StateHasChanged();
    }

    async Task ApplySearchAsync()
    {
        _searchPending = false;
        _activeIndex = 0;
        RebuildFiltered();
        MoveActiveTo(0, 1); // first enabled match, never a disabled one
        if (OnSearch.HasDelegate) await OnSearch.InvokeAsync(_searchText);
    }

    // Apply a still-pending debounced search immediately, so keyboard navigation/selection sees the
    // list filtered by what the user has actually typed. Cancels the timer so it can't re-fire and
    // reset the active index afterwards. No-op when nothing is pending (the common, non-debounced path).
    async Task FlushPendingSearchAsync()
    {
        if (!_searchPending) return;
        _debounceCts?.Cancel();
        await ApplySearchAsync();
    }

    async Task SelectAsync(SelectOption<TValue> option)
    {
        if (option.Disabled) return;

        if (IsMultiple)
        {
            if (_selectedSet.Contains(option.Value))
            {
                RemoveSelected(option.Value);
                PruneTagOption(option.Value);
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
        PruneTagOption(value);
        await ValuesChanged.InvokeAsync(_selected.ToList());
    }

    // A user-created tag that is no longer selected leaves the option list too (matching AntD) —
    // otherwise a removed "typo-tag" stayed selectable in the dropdown for the component's lifetime.
    void PruneTagOption(TValue value)
    {
        if (Mode != SelectMode.Tags || _selectedSet.Contains(value)) return;
        if (_tagOptions.RemoveAll(t => _comparer.Equals(t.Value, value)) > 0)
        {
            if (value is not null) _lookup.Remove(value);
            RebuildFiltered();
        }
    }

    async Task ClearAsync()
    {
        if (IsMultiple)
        {
            ClearSelected();
            if (Mode == SelectMode.Tags && _tagOptions.Count > 0)
            {
                foreach (var tag in _tagOptions)
                {
                    if (tag.Value is not null) _lookup.Remove(tag.Value);
                }
                _tagOptions.Clear();
            }
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
        // A debounced search may have updated the text but not yet rebuilt _filtered; flush it before
        // any key that navigates or commits against that list, so it reflects what the user typed.
        if (_searchPending && e.Key is "Enter" or "ArrowDown" or "ArrowUp" or "Home" or "End")
            await FlushPendingSearchAsync();

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
                // A disabled active option (e.g. every match is disabled) falls through — in Tags
                // mode the typed text still commits instead of the keystroke dying on it.
                if (_open && _activeIndex >= 0 && _activeIndex < _filtered.Count && !_filtered[_activeIndex].Disabled)
                {
                    await SelectAsync(_filtered[_activeIndex]);
                }
                else if (Mode == SelectMode.Tags && !string.IsNullOrWhiteSpace(_searchText))
                {
                    await CommitTagAsync();
                }
                else if (!_open)
                {
                    await OpenAsync(); // ARIA combobox pattern: Enter on a closed combobox opens it
                }
                break;

            case "Escape":
                if (_open) await CloseAsync();
                break;

            case " ":
                // ARIA combobox pattern: Space opens a closed, non-searchable select — its input is
                // readonly, so Space has no text-entry meaning there (wss-select.js suppresses the
                // page-scroll default for this case). When ShowSearch is on, Space belongs to the
                // search text and passes through untouched.
                if (!ShowSearch && !_open) await OpenAsync();
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

    // Imports the RCL-local JS module once and hands it back, re-checking _disposed after the awaited
    // import so a dispose that raced an in-flight import disposes-and-nulls the reference here instead of
    // stranding it on a dead instance (DisposeAsync saw _jsModule still null). Returns null when the
    // component is disposed or JS is unavailable (server prerender, unit tests) — every caller then takes
    // the same no-JS degrade path it had when it caught the import failure itself.
    async Task<IJSObjectReference?> GetJsModuleAsync()
    {
        if (_disposed) return null;
        try
        {
            _jsModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/WssBlazorControls/wss-select.js");
        }
        catch
        {
            return null; // no JS runtime / module (prerender, tests)
        }
        if (_disposed)
        {
            try { await _jsModule.DisposeAsync(); } catch { }
            _jsModule = null;
            return null;
        }
        return _jsModule;
    }

    // Keeps the keyboard-highlighted row visible in the virtualized dropdown.
    // Uses a tiny RCL-local JS module; degrades to a no-op when JS isn't available
    // (e.g. server prerender or unit tests).
    async Task ScrollActiveIntoViewAsync()
    {
        if (_activeIndex < 0) return;
        var module = await GetJsModuleAsync();
        if (module is null) return;
        try
        {
            await module.InvokeVoidAsync("scrollActiveIntoView", _dropdownRef, _activeIndex, RowHeight);
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
        // Wire the native key-default suppression once per input element (the element is recreated
        // when Mode switches between the single and multiple markup branches).
        if (!_inputWired || _wiredMode != Mode)
        {
            _inputWired = true;
            _wiredMode = Mode;
            var module = await GetJsModuleAsync();
            if (module is not null)
            {
                try
                {
                    await module.InvokeVoidAsync("initInput", _inputRef, _wrapperRef);
                }
                catch
                {
                    // No JS runtime / module (prerender, tests) — keyboard still works, minus the
                    // native-default suppression (e.g. Enter may implicitly submit an enclosing form).
                }
            }
        }

        if (_open && !_dropdownPositioned)
        {
            var module = await GetJsModuleAsync();
            if (module is not null)
            {
                try
                {
                    // placeDropdown positions/flips the panel AND returns the open-order z-index it wrote
                    // to the wrapper. Mirror it into _openZIndex so WidthStyle re-emits it on every bound-
                    // style re-render (the JS write and the Blazor write agree — that's the point). Fires
                    // once per open (guarded by _dropdownPositioned), so _openZIndex is null here and there
                    // is no z creep from the shared counter. On throw (no JS) it stays null → CSS fallback.
                    var z = await module.InvokeAsync<int>("placeDropdown", _wrapperRef, _dropdownRef, 4);
                    // 0 is the JS null-ref guard value (nothing was positioned) — mirroring it would pin
                    // the wrapper at z-index:0, under its own backdrop. Only positive values are real.
                    _openZIndex = z > 0 ? z : null;
                }
                catch
                {
                    // No JS runtime / module — keep the CSS default (downward) placement.
                }
            }
            // The highlight opens on the current selection (SetInitialActive) — bring it into view
            // while the panel is still measuring, so the first visible frame is already scrolled.
            if (_activeIndex > 0) await ScrollActiveIntoViewAsync();
            _dropdownPositioned = true;
            StateHasChanged(); // reveal now that it's positioned (drops wss-measuring)
        }
        else if (!_open && _dropdownPositioned)
        {
            _dropdownPositioned = false;
            // Keep the C#-owned z in sync with the DOM teardown below (CloseAsync already nulled it on
            // the normal close path; belt-and-suspenders for any future path that closes without it).
            _openZIndex = null;
            try
            {
                // Drop the open-order z-index — the wrapper persists in the page, and a stale
                // high z would poke through later overlays' masks.
                if (_jsModule is not null) await _jsModule.InvokeVoidAsync("clearZ", _wrapperRef);
            }
            catch
            {
                // No JS runtime / module — nothing was assigned, nothing to clear.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Set first: an import in flight (GetJsModuleAsync) re-checks this after its await and disposes
        // its own late-assigned module rather than stranding it on this dead instance.
        _disposed = true;
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
