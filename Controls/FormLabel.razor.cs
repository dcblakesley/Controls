namespace Controls;

/// <summary> The Label and Description for a form field that shows up over the input.</summary>
/// <remarks>
/// <para>
/// Two ids come out of every branch, and they are NOT interchangeable:
/// </para>
/// <list type="bullet">
///   <item><c>lbl-{id}</c> — the <c>&lt;label&gt;</c>/<c>&lt;legend&gt;</c> element itself. Use it
///   when you need the element (e.g. <c>label[for]</c> pairing, or scrolling to it).</item>
///   <item><c>lbltext-{id}</c> — the <b>naming anchor</b>: a span holding the label text, plus the
///   visually-hidden "(required)" when <see cref="IsRequiredTextIncluded"/> is set. This is what an
///   <c>aria-labelledby</c> should point at.</item>
/// </list>
/// <para>
/// The distinction exists because the <see cref="LabelTooltip"/> trigger renders <em>inside</em> the
/// label/legend (it has to — a <c>&lt;legend&gt;</c> only renders as a legend while it is the
/// fieldset's first child, so the trigger cannot be a sibling), and accessible-name computation folds
/// a descendant button's own name into the name it builds from content. Naming a field from
/// <c>lbl-{id}</c> therefore announced "Full Name More information about Full Name"; naming it from
/// <c>lbltext-{id}</c> gives a clean "Full Name" while the trigger keeps its own distinct name.
/// The required star is outside the anchor as well, but only for tidiness — it is
/// <c>aria-hidden</c>, so it never contributed to any name.
/// </para>
/// <para>
/// The sr-only "(required)" is deliberately <em>inside</em> the anchor: for a <c>role="group"</c>
/// fieldset, ARIA 1.2 forbids <c>aria-required</c>, so that text being part of the accessible name is
/// the entire mechanism by which requiredness reaches assistive tech. Excluding it would silently
/// un-fix that while leaving markup that still looks correct.
/// </para>
/// </remarks>
public partial class FormLabel
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] FormDefaults? FormDefaults { get; set; }

    /// <inheritdoc cref="IEditControl.Id"/>
    [Parameter] public string? Id { get; set; }
    
    [Parameter] public required List<Attribute> Attributes { get; set; }
    [Parameter] public required FieldIdentifier FieldIdentifier { get; set; }
    
    /// <inheritdoc cref="IEditControl.Label"/>
    [Parameter] public string? Label { get; set; }
    
    /// <inheritdoc cref="IEditControl.Description"/>
    [Parameter] public string? Description { get; set; }
    
    /// <summary> Used when a legend is more appropriate than a label such as when you have a group of radio buttons</summary>
    [Parameter] public bool IsLegend { get; set; }
    
    /// <inheritdoc cref="IEditControl.IsRequired"/>
    [Parameter] public bool? IsRequired { get; set; }
    
    /// <inheritdoc cref="IEditControl.Tooltip"/>
    [Parameter] public string? Tooltip { get; set; }

    /// <summary>
    /// Replaces the tooltip trigger's accessible name (<see cref="LabelTooltip.TriggerLabel"/>).
    /// Unset, the trigger is named "More information about {label}" so a form with several tooltipped
    /// fields doesn't present a list of identical "More information" buttons — set this to localize
    /// that sentence, the same way the toast containers' <c>CloseButtonLabel</c> localizes theirs.
    /// Ignored when no tooltip renders.
    /// </summary>
    [Parameter] public string? TooltipTriggerLabel { get; set; }

    /// <summary>
    /// Adds a visually-hidden "(required)" after the label text — <see cref="IsLegend"/> only, since
    /// it exists for a control whose grouping element cannot carry <c>aria-required</c>: ARIA 1.2
    /// allows it on <c>radiogroup</c> but not on <c>group</c>, so
    /// <see cref="EditCheckedEnumList{TEnum}"/>/<see cref="EditCheckedStringList"/>'s fieldsets had no
    /// required cue for assistive tech at all — the star is <c>aria-hidden</c>, and accessible-name
    /// computation skips it.
    /// <para>
    /// Opt-in precisely because the signal must not arrive twice: every control whose field (or
    /// <c>radiogroup</c> fieldset) already carries <c>aria-required</c> leaves this false, or the
    /// legend's own text would add a second "required" to the name AT already announces from the
    /// state. Deliberately NOT gated on the star's own visibility
    /// (<see cref="FormOptions.IsRequiredStarHidden"/>): this is the <c>aria-required</c> stand-in,
    /// not a second star, and hiding the star doesn't drop <c>aria-required</c> anywhere else either.
    /// </para>
    /// </summary>
    [Parameter] public bool IsRequiredTextIncluded { get; set; }

    /// <inheritdoc cref="IEditControl.IsLabelHidden"/>
    [Parameter] public bool IsLabelHidden { get; set; }

    /// <summary>
    /// False when the paired element is not labelable (the read-only view renders a div, and
    /// <c>label[for]</c> must reference a labelable element) — the label then renders without
    /// <c>for</c>; the read-only view names itself via <c>aria-labelledby</c>. Controls pass
    /// <c>ShowEditor</c>. Defaults to true.
    /// </summary>
    [Parameter] public bool IsForLabelable { get; set; } = true;

    /// <summary>
    /// Optional content rendered <em>inside</em> the <c>&lt;label&gt;</c>, before the required star and
    /// the label text — for a control whose input must nest within its own label
    /// (<c>&lt;label&gt;&lt;input type="checkbox"&gt; Text&lt;/label&gt;</c>, the checkbox row shape).
    /// That nesting is the only reason <see cref="EditBool"/> couldn't use this component, which left it
    /// re-implementing the label text, description, tooltip and visually-hidden fallback inline — and
    /// silently omitting the required star. When <see cref="IsLabelHidden"/> is set the content renders
    /// as a <em>sibling</em> immediately after the visually-hidden label instead: nesting it inside an
    /// <c>.edit-sr-only</c> label would visually hide the very control the label names. Ignored by the
    /// <see cref="IsLegend"/> branch (a fieldset's inputs are the legend's siblings, never its
    /// children). Unset for every other caller, and then renders nothing at all.
    /// </summary>
    [Parameter] public RenderFragment? NestedInput { get; set; }

    /// <summary>
    /// Replaces — not appends to — the default <c>edit-label</c> class on the rendered
    /// <c>&lt;label&gt;</c>. <see cref="EditBool"/>'s checkbox mode needs the checkbox-row layout class
    /// (<c>edit-checkbox-label</c>, a flex row) and must <em>not</em> also carry <c>edit-label</c>,
    /// which consumers commonly style as a block-level field label. Ignored by the
    /// <see cref="IsLegend"/> branch, and by the <see cref="IsLabelHidden"/> branch (which must keep
    /// <c>edit-sr-only</c>). Defaults to <c>edit-label</c>.
    /// </summary>
    [Parameter] public string? LabelClass { get; set; }

    /// <summary> <see cref="LabelClass"/> with its <c>edit-label</c> default applied. </summary>
    string EffectiveLabelClass => LabelClass ?? "edit-label";

    // Resolved once per parameter-change cycle; the razor binds to these instead of calling the
    // helpers on every render path (the legend + label branches in FormLabel.razor evaluate
    // DisplayLabel/DisplayDescription twice otherwise).
    string _label = string.Empty;
    string? _description;
    string? _tooltip;
    string? _tooltipTriggerLabel;
    bool _isRequired;

    string DisplayLabel() => _label;
    string? DisplayDescription() => _description;

    /// <summary>
    /// The tooltip text folded into the visually-hidden description, and only in the
    /// <see cref="IsLabelHidden"/> branch — where no trigger renders to reach it any other way. Null
    /// (renders nothing) whenever a real trigger exists, so the text is never announced twice.
    /// </summary>
    string? DisplayHiddenTooltip() => IsLabelHidden ? _tooltip : null;

    /// <summary>
    /// Whether the legend carries the visually-hidden "(required)" — see
    /// <see cref="IsRequiredTextIncluded"/>. Unlike the star it is NOT suppressed by
    /// <see cref="IsStarHidden"/>: it stands in for <c>aria-required</c>, which no other control
    /// drops when the star is hidden either.
    /// </summary>
    bool IsRequiredTextShown => _isRequired && IsRequiredTextIncluded;

    // Per-form FormOptions → per-tree FormDefaults (Effective* walks nested instances per property)
    // → process-wide static. FormOptions in the last term binds to the *type* (static member), so
    // the chain is null-safe despite appearances.
    bool IsStarHidden =>
        FormOptions?.IsRequiredStarHidden ?? FormDefaults?.EffectiveIsRequiredStarHidden ?? FormOptions.DefaultIsRequiredStarHidden;

    // Last-seen inputs for the OnParametersSet guard below. Every validation-state change re-renders
    // every InputBase-derived control in the form, which re-parameterizes this label — and the
    // List<Attribute>/FieldIdentifier parameters defeat Blazor's change skip, so OnParametersSet
    // re-ran in full (label derivation, attribute scans, RequiredResolver) on every keystroke in
    // ANY field. Skip the recompute unless an input it reads actually changed.
    bool _inputsSeen;
    string? _lastLabelParam;
    string? _lastDescriptionParam;
    string? _lastTooltipParam;
    string? _lastTooltipTriggerLabelParam;
    List<Attribute>? _lastAttributes;
    FieldIdentifier _lastFieldIdentifier;
    bool? _lastIsRequiredParam;
    FormOptions? _lastFormOptions;

    protected override void OnParametersSet()
    {
        // Inputs compared: everything the recompute below reads. IsStarHidden is a live property
        // evaluated per render (and FormDefaults feeds only it), so neither needs to invalidate here.
        // Behavior note (matches the documented FormOptions.RequiredResolver contract — "controls
        // consult it on init and on parameter changes, not on every keystroke"): a resolver whose
        // ANSWER changes for the same field is re-consulted only when a real parameter changes. A
        // consumer needing a live re-evaluation toggles IsRequired or cascades a new FormOptions.
        if (_inputsSeen
            && Label == _lastLabelParam
            && Description == _lastDescriptionParam
            && Tooltip == _lastTooltipParam
            && TooltipTriggerLabel == _lastTooltipTriggerLabelParam
            && ReferenceEquals(Attributes, _lastAttributes)
            && FieldIdentifier.Equals(_lastFieldIdentifier)
            && IsRequired == _lastIsRequiredParam
            && ReferenceEquals(FormOptions, _lastFormOptions))
            return;
        _inputsSeen = true;
        _lastLabelParam = Label;
        _lastDescriptionParam = Description;
        _lastTooltipParam = Tooltip;
        _lastTooltipTriggerLabelParam = TooltipTriggerLabel;
        _lastAttributes = Attributes;
        _lastFieldIdentifier = FieldIdentifier;
        _lastIsRequiredParam = IsRequired;
        _lastFormOptions = FormOptions;

        // Attributes can be null when FormLabel is used outside the Edit* controls (EditDisplay supplies
        // no attribute list). Fall back to the explicit Label/Description rather than dropping them, and
        // never call GetLabelText with the default FieldIdentifier (its FieldName would be null).
        _label = Label ?? Attributes?.GetLabelText(FieldIdentifier) ?? string.Empty;
        _description = Description ?? Attributes?.Description();
        // Same resolution LabelTooltip does for itself -- needed here too because the hidden-label
        // branch renders no LabelTooltip and has to fold the text into the description instead.
        // Normalized to null when empty so it matches EditControlInit.ResolveAriaRefs' own
        // IsNullOrEmpty test, and desc-{id} can never render for a tooltip nothing references.
        var tooltip = Tooltip ?? Attributes?.Tooltip();
        _tooltip = string.IsNullOrEmpty(tooltip) ? null : tooltip;
        // "More information" on every trigger made a form's button list read as N identical entries
        // with nothing to tell them apart. Falls back to the bare phrase when there is no label to
        // name (standalone FormLabel with neither a Label nor a resolvable field).
        _tooltipTriggerLabel = TooltipTriggerLabel
            ?? (string.IsNullOrEmpty(_label) ? null : $"More information about {_label}");
        // Same resolution as the controls' aria-required (IsRequired param → [Required] attribute →
        // FormOptions.RequiredResolver) so the star and aria-required can never disagree.
        _isRequired = EditControlInit.IsRequired(Attributes, IsRequired, FormOptions, FieldIdentifier);
    }
}