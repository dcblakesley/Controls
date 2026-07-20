namespace Controls;

/// <summary>
/// Composite two-field date-range edit control, backed by the <see cref="DateRangePicker"/> UI-kit
/// calendar dropdown. Binds two independent <c>DateTime?</c> model properties — <c>@bind-Start</c> /
/// <c>@bind-End</c> — since <see cref="Microsoft.AspNetCore.Components.Forms.InputBase{TValue}"/> only
/// supports a single bound value. Adds form binding, validation (for both fields), one shared label,
/// a read-only view, and <see cref="FormOptions"/> support on top of DateRangePicker's type-or-pick UX.
/// </summary>
/// <remarks>
/// <para>
/// Follows <see cref="EditControlListBase{TItem}"/>'s integration style (a plain
/// <see cref="ComponentBase"/>, not an <c>InputBase</c>, with hand-rolled <see cref="FormOptions"/>
/// registration and <see cref="EditContext"/> validation-state subscription) rather than inheriting
/// it directly — that base is hard-wired to a single bound <c>List&lt;TItem&gt;</c>, which doesn't fit
/// two independent scalar fields.
/// </para>
/// <para>
/// One <see cref="FormLabel"/> renders for the whole control, associated (<c>label[for]</c>) with the
/// Start input — the only one <see cref="DateRangePicker"/> exposes an <c>Id</c> for. Both fields still
/// register independently with <see cref="FormOptions"/> and each gets its own
/// <see cref="FieldValidationDisplay"/>, so a validation failure on either Start or End shows its own
/// message and links from a <c>ValidationView</c> summary.
/// </para>
/// <para>
/// Validation-state ARIA reaches both actual <c>&lt;input&gt;</c>s through
/// <see cref="DateRangePicker"/>'s per-input <c>StartAria*</c>/<c>EndAria*</c> parameters, each
/// reflecting its own field's state (a Start error never marks the End input invalid, and vice
/// versa); each input's <c>aria-errormessage</c>/<c>aria-describedby</c> references its own
/// <see cref="FieldValidationDisplay"/> message. The visible <see cref="FormLabel"/> associates
/// (<c>label[for]</c>) with the Start input, but <c>aria-label</c> wins the accessible-name
/// computation over that association (per the AccName spec) — so both inputs' accessible names come
/// entirely from <see cref="StartInputLabel"/>/<see cref="EndInputLabel"/>, which default to the
/// resolved <see cref="Label"/> plus a " start"/" end" suffix (falling back to each field's own
/// auto-derived label when <see cref="Label"/> isn't set). The suffix keeps the two names unique from
/// each other while both still containing the visible label text (WCAG 2.5.3 Label in Name). The End
/// input also carries its own id for <c>ValidationView</c> links.
/// </para>
/// </remarks>
public partial class EditDateRange : IEditControl, IDisposable
{
    [CascadingParameter] protected EditContext? EditContext { get; set; }
    /// <inheritdoc/>
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    /// <inheritdoc/>
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }
    /// <inheritdoc/>
    [CascadingParameter] public FormDefaults? FormDefaults { get; set; }

    /// <inheritdoc/>
    [Parameter] public string? Id { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? IdPrefix { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Label { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Description { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Tooltip { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? ContainerClass { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool? IsRequired { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsLabelHidden { get; set; }
    /// <inheritdoc/>
    [Parameter] public HidingMode? Hiding { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsHidden { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsEditMode { get; set; } = true;
    /// <inheritdoc/>
    [Parameter] public bool IsDisabled { get; set; }

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Start</c>/<c>@bind-End</c> alone supply
    /// the accessors those used to require. This inert stub exists only so a leftover
    /// <c>Field="..."</c> attribute is a compile error instead of silently building and throwing at
    /// runtime. Remove the attribute from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Start/@bind-End are sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<DateTime?>>? Field { get; set; }

    /// <summary> Start of the bound range. Supports <c>@bind-Start</c>.</summary>
    [Parameter] public DateTime? Start { get; set; }
    /// <summary> Raised with the new start when it changes (supports <c>@bind-Start</c>).</summary>
    [Parameter] public EventCallback<DateTime?> StartChanged { get; set; }
    /// <summary>
    /// Compiler-populated by <c>@bind-Start</c> — supplies the accessor <see cref="OnInitialized"/>
    /// needs to derive the Start <see cref="FieldIdentifier"/>, the control's resolved id, and its label.
    /// </summary>
    [Parameter, EditorRequired] public Expression<Func<DateTime?>>? StartExpression { get; set; }

    /// <summary> End of the bound range. Supports <c>@bind-End</c>.</summary>
    [Parameter] public DateTime? End { get; set; }
    /// <summary> Raised with the new end when it changes (supports <c>@bind-End</c>).</summary>
    [Parameter] public EventCallback<DateTime?> EndChanged { get; set; }
    /// <summary>
    /// Compiler-populated by <c>@bind-End</c> — supplies the accessor <see cref="OnInitialized"/> needs
    /// to derive the End <see cref="FieldIdentifier"/> (its own validation messages and attribute scan
    /// are independent of Start's).
    /// </summary>
    [Parameter, EditorRequired] public Expression<Func<DateTime?>>? EndExpression { get; set; }

    /// <summary>
    /// Optional label used in the End field's own validation messages (e.g. "End Date is required").
    /// Null (default) auto-generates from the End property's <c>[DisplayName]</c>/name, same precedence
    /// as the primary <see cref="Label"/>. The visible <see cref="FormLabel"/> for the whole control
    /// always derives from Start, never this.
    /// </summary>
    [Parameter] public string? EndLabel { get; set; }

    /// <inheritdoc cref="DateRangePicker.Presets"/>
    [Parameter] public IReadOnlyList<DateRangePreset>? Presets { get; set; }
    /// <inheritdoc cref="DateRangePicker.Min"/>
    [Parameter] public DateTime? Min { get; set; }
    /// <inheritdoc cref="DateRangePicker.Max"/>
    [Parameter] public DateTime? Max { get; set; }
    /// <inheritdoc cref="DateRangePicker.Format"/>
    [Parameter] public string Format { get; set; } = "MM/dd/yyyy";
    /// <inheritdoc cref="DateRangePicker.StartPlaceholder"/>
    [Parameter] public string? StartPlaceholder { get; set; }
    /// <inheritdoc cref="DateRangePicker.EndPlaceholder"/>
    [Parameter] public string? EndPlaceholder { get; set; }
    /// <inheritdoc cref="DateRangePicker.AllowClear"/>
    [Parameter] public bool AllowClear { get; set; } = true;
    /// <inheritdoc cref="DateRangePicker.Width"/>
    [Parameter] public string? Width { get; set; }
    /// <inheritdoc cref="DateRangePicker.FirstDayOfWeek"/>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary> Format string for the read-only "start - end" value display. Defaults to "MM-dd-yyyy" (matches <see cref="EditDate{T}"/>'s default).</summary>
    [Parameter] public string DateFormat { get; set; } = "MM-dd-yyyy";

    /// <summary>
    /// Captures unmatched attributes (a consumer's <c>class</c>/<c>style</c>/<c>data-*</c>) so they can
    /// be forwarded onto <see cref="DateRangePicker"/>'s own <c>AdditionalAttributes</c> splat — the
    /// same role <see cref="EditControlListBase{TItem}.AdditionalAttributes"/> plays for list controls.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // Accessible-name parameters forwarded to the inner DateRangePicker. Defaults mirror
    // DateRangePicker's own literal defaults except StartInputLabel/EndInputLabel, which resolve
    // through EffectiveStartInputLabel/EffectiveEndInputLabel below instead of a literal default.

    /// <summary>
    /// Accessible name of the Start input — the one <see cref="DateRangePicker"/> associates a
    /// <c>label[for]</c> with. Null (default) resolves to <see cref="Label"/> + " start" when
    /// <see cref="Label"/> is set, else the Start field's own auto-derived label (<c>[DisplayName]</c>/
    /// <c>[Display(Name)]</c>/property name) — see the class remarks for why the suffix is needed even
    /// though the visible <see cref="FormLabel"/> associates with this same input. Override to set
    /// something else entirely.
    /// </summary>
    [Parameter] public string? StartInputLabel { get; set; }
    /// <summary>
    /// Accessible name of the End input. Null (default) resolves to <see cref="Label"/> + " end" when
    /// <see cref="Label"/> is set, else the End field's own auto-derived label — mirrors
    /// <see cref="StartInputLabel"/>'s resolution so the two names stay unique from each other while
    /// both contain the visible label text (WCAG 2.5.3 Label in Name). Override to localize or to set
    /// something else entirely.
    /// </summary>
    [Parameter] public string? EndInputLabel { get; set; }
    /// <inheritdoc cref="DateRangePicker.DialogLabel"/>
    [Parameter] public string DialogLabel { get; set; } = "Choose date range";
    /// <inheritdoc cref="DateRangePicker.MonthSelectLabel"/>
    [Parameter] public string MonthSelectLabel { get; set; } = "Month";
    /// <inheritdoc cref="DateRangePicker.YearSelectLabel"/>
    [Parameter] public string YearSelectLabel { get; set; } = "Year";
    /// <inheritdoc cref="DateRangePicker.ClearLabel"/>
    [Parameter] public string ClearLabel { get; set; } = "Clear dates";
    /// <inheritdoc cref="DateRangePicker.PresetsLabel"/>
    [Parameter] public string PresetsLabel { get; set; } = "Quick ranges";
    /// <inheritdoc cref="DateRangePicker.PrevMonthLabel"/>
    [Parameter] public string PrevMonthLabel { get; set; } = "Previous month";
    /// <inheritdoc cref="DateRangePicker.NextMonthLabel"/>
    [Parameter] public string NextMonthLabel { get; set; } = "Next month";

    // Standard derived state — mirrors EditControlListBase's fields, duplicated per bound field.
    string _id = string.Empty;
    string _endId = string.Empty;
    string? _isRequired;
    List<Attribute>? _attributes;
    List<Attribute>? _endAttributes;
    FieldIdentifier _startFieldIdentifier;
    FieldIdentifier _endFieldIdentifier;
    string _errorMsgId = string.Empty;
    string _describedBy = string.Empty;
    string? _endIsRequired;
    string _endErrorMsgId = string.Empty;
    string _endDescribedBy = string.Empty;
    EditContext? _subscribedEditContext;
    Func<FieldIdentifier>? _startFieldIdentifierFactory;
    Func<FieldIdentifier>? _endFieldIdentifierFactory;

    /// <summary>
    /// The control's fully-resolved required-ness, derived from the Start field only (an <c>[Required]</c>
    /// on End does not raise the shared star — it still surfaces as its own validation message). Same
    /// resolution as every other control: <see cref="IEditControl.IsRequired"/> parameter →
    /// <c>[Required]</c> attribute → <see cref="FormOptions.RequiredResolver"/>.
    /// </summary>
    protected bool? IsRequiredResolved => _isRequired is not null;

    /// <summary> True when the Start field currently has a validation error.</summary>
    protected bool IsStartInvalid => EditContext is not null && EditContext.GetValidationMessages(_startFieldIdentifier).Any();
    /// <summary> True when the End field currently has a validation error.</summary>
    protected bool IsEndInvalid => EditContext is not null && EditContext.GetValidationMessages(_endFieldIdentifier).Any();

    /// <summary>
    /// The consumer's <c>class</c> attribute (if any) merged with the Start field's EditContext state
    /// classes (<c>modified</c>/<c>valid</c>/<c>invalid</c>) — mirrors
    /// <see cref="EditControlListBase{TItem}.FieldCssClass"/>, the list-control analogue, since this
    /// control is likewise not an <c>InputBase</c> and gets no <c>CssClass</c> for free.
    /// </summary>
    string? FieldCssClass
    {
        get
        {
            var fieldClass = EditContext is null ? string.Empty : EditContext.FieldCssClass(_startFieldIdentifier);
            if (AdditionalAttributes is not null &&
                AdditionalAttributes.TryGetValue("class", out var classObj) &&
                Convert.ToString(classObj, CultureInfo.InvariantCulture) is { Length: > 0 } consumerClass)
            {
                return fieldClass.Length > 0 ? $"{consumerClass} {fieldClass}" : consumerClass;
            }
            return fieldClass.Length > 0 ? fieldClass : null;
        }
    }

    /// <summary> True when the editor input should render. False renders the read-only view. </summary>
    protected bool ShowEditor => EditControlInit.ShowEditor(IsEditMode, FormOptions);

    /// <summary> True when the label should be suppressed. </summary>
    protected bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);

    /// <summary> Both Start and End null count as "default" for the hiding modes — there's no
    /// meaningful partial-range default distinct from "nothing entered".</summary>
    protected bool ShouldShowComponent()
    {
        var isNull = Start is null && End is null;
        return EditControlInit.ShouldShow(IsHidden, Hiding, FormOptions, ShowEditor, isNull, isNull);
    }

    // Both default to the resolved Label plus a " start"/" end" suffix — aria-label wins the
    // accessible-name computation over the visible FormLabel's label[for] association (see the class
    // remarks), so the suffix is what keeps the two inputs' names unique from each other while each
    // still contains the visible label text (WCAG 2.5.3 Label in Name). Falls back to each field's own
    // auto-derived label (matches EditDatePicker's EffectiveInputLabel) when Label isn't set.
    string EffectiveStartInputLabel => StartInputLabel ?? (Label is not null ? $"{Label} start" : _attributes.GetLabelText(_startFieldIdentifier));
    string EffectiveEndInputLabel => EndInputLabel ?? (Label is not null ? $"{Label} end" : _endAttributes.GetLabelText(_endFieldIdentifier));

    protected override void OnInitialized()
    {
        // Captured into locals (rather than closing over the nullable StartExpression/EndExpression
        // properties directly) so the factories below close over a provably non-null Expression —
        // nullable flow analysis doesn't carry a property's null-check narrowing into a lambda.
        var startExpression = StartExpression ?? throw new InvalidOperationException(
            $"{nameof(EditDateRange)} requires a two-way @bind-Start binding (which supplies {nameof(StartExpression)}).");
        var endExpression = EndExpression ?? throw new InvalidOperationException(
            $"{nameof(EditDateRange)} requires a two-way @bind-End binding (which supplies {nameof(EndExpression)}).");

        (_id, _attributes, _startFieldIdentifier) = EditControlInit.Init(startExpression, Id, FormGroupOptions, IdPrefix);
        _startFieldIdentifierFactory = () => FieldIdentifier.Create(startExpression);

        _endAttributes = AttributesHelper.GetExpressionCustomAttributes(endExpression);
        _endFieldIdentifier = FieldIdentifier.Create(endExpression);
        _endFieldIdentifierFactory = () => FieldIdentifier.Create(endExpression);
        _endId = $"{_id}-end";

        _isRequired = EditControlInit.AriaRequired(_attributes, IsRequired, FormOptions, _startFieldIdentifier);
        // Each field registers under its own input's DOM id (DateRangePicker's Id/EndId), so a
        // ValidationView link for an End-only error lands on the End input, not Start's.
        FormOptions?.RegisterField(_startFieldIdentifier, _id, this);
        FormOptions?.RegisterField(_endFieldIdentifier, _endId, this);

        (_errorMsgId, _describedBy) = EditControlInit.ResolveAriaRefs(_id, ShouldHideLabel, Description, Tooltip, _attributes);
        // The End field's ARIA state is independent of Start's. Description/Tooltip belong to the
        // whole control (rendered by the start-anchored FormLabel), so the end input's describedby
        // references only its own validation message — hence shouldHideLabel: true here.
        _endIsRequired = EditControlInit.AriaRequired(_endAttributes, null, FormOptions, _endFieldIdentifier);
        (_endErrorMsgId, _endDescribedBy) = EditControlInit.ResolveAriaRefs(_endId, true, null, null, _endAttributes);
    }

    protected override void OnParametersSet()
    {
        // Keep the cached ARIA references current when parameters change (runtime Description/Tooltip
        // or label-hidden toggle). No-op until OnInitialized has run (_attributes is null before then).
        if (_attributes is not null)
        {
            _isRequired = EditControlInit.AriaRequired(_attributes, IsRequired, FormOptions, _startFieldIdentifier);
            (_errorMsgId, _describedBy) = EditControlInit.ResolveAriaRefs(_id, ShouldHideLabel, Description, Tooltip, _attributes);
            _endIsRequired = EditControlInit.AriaRequired(_endAttributes, null, FormOptions, _endFieldIdentifier);
            (_endErrorMsgId, _endDescribedBy) = EditControlInit.ResolveAriaRefs(_endId, true, null, null, _endAttributes);
        }

        if (ReferenceEquals(EditContext, _subscribedEditContext)) return;
        if (_subscribedEditContext is not null)
            _subscribedEditContext.OnValidationStateChanged -= OnValidationStateChanged;
        if (EditContext is not null)
            EditContext.OnValidationStateChanged += OnValidationStateChanged;
        _subscribedEditContext = EditContext;

        // The EditContext changes when the parent swaps the model instance (form reset, reload) —
        // re-derive both FieldIdentifiers against the current model, mirroring
        // EditControlListBase.OnParametersSet.
        if (_startFieldIdentifierFactory is not null && _endFieldIdentifierFactory is not null)
        {
            FormOptions?.UnregisterField(_startFieldIdentifier, this);
            FormOptions?.UnregisterField(_endFieldIdentifier, this);
            _startFieldIdentifier = _startFieldIdentifierFactory();
            _endFieldIdentifier = _endFieldIdentifierFactory();
            FormOptions?.RegisterField(_startFieldIdentifier, _id, this);
            FormOptions?.RegisterField(_endFieldIdentifier, _endId, this);
        }
    }

    void OnValidationStateChanged(object? sender, ValidationStateChangedEventArgs e) => StateHasChanged();

    // Write the new value back to the bound model BEFORE notifying the EditContext — the validator
    // reads the property live off the model via reflection during NotifyFieldChanged (see
    // EditControlListBase.ToggleAsync for the full rationale).
    async Task OnStartChanged(DateTime? value)
    {
        Start = value;
        await StartChanged.InvokeAsync(value);
        EditContext?.NotifyFieldChanged(_startFieldIdentifier);
    }

    async Task OnEndChanged(DateTime? value)
    {
        End = value;
        await EndChanged.InvokeAsync(value);
        EditContext?.NotifyFieldChanged(_endFieldIdentifier);
    }

    // The validation-state ARIA goes through DateRangePicker's dedicated per-input Aria* parameters
    // (straight onto the two actual <input>s, each reflecting its own field's state); this splat
    // carries only the consumer's own attributes plus the state classes, landing on the picker's
    // outer wrapper (its documented AdditionalAttributes target).
    IReadOnlyDictionary<string, object> PickerAttributes
    {
        get
        {
            var attrs = new Dictionary<string, object>();
            if (AdditionalAttributes is not null)
                foreach (var kv in AdditionalAttributes) attrs[kv.Key] = kv.Value;
            // Overwrite the raw consumer "class" (if any) with FieldCssClass so the wrapper picks up
            // the Start field's validation-state styling hooks (see FieldCssClass's remarks).
            if (FieldCssClass is not null) attrs["class"] = FieldCssClass;
            return attrs;
        }
    }

    string GetDisplayValue()
    {
        var start = FormatOne(Start);
        var end = FormatOne(End);
        if (start.Length == 0 && end.Length == 0) return string.Empty;
        return $"{start} - {end}";
    }

    string FormatOne(DateTime? value)
    {
        if (value is not { } v) return string.Empty;
        // Gregorian-forced like the picker's own display, so read-only and edit mode can never
        // disagree about the year under a non-Gregorian-default culture (th-TH, ar-SA).
        var culture = GregorianCultureHelper.Gregorian(CultureInfo.CurrentCulture);
        try
        {
            return v.ToString(DateFormat, culture);
        }
        catch (FormatException)
        {
            return v.ToString(culture);
        }
    }

    /// <summary> Detaches the validation-state listener and drops both field registrations so a removed
    /// control (e.g. behind a conditional <c>@if</c>) doesn't leave stale state in the validation summary. </summary>
    public void Dispose()
    {
        if (_subscribedEditContext is not null)
            _subscribedEditContext.OnValidationStateChanged -= OnValidationStateChanged;
        FormOptions?.UnregisterField(_startFieldIdentifier, this);
        FormOptions?.UnregisterField(_endFieldIdentifier, this);
    }
}
