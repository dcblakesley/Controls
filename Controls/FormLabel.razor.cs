namespace Controls;

/// <summary> The Label and Description for a form field that shows up over the input.</summary>
public partial class FormLabel
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] FormDefaults? FormDefaults { get; set; }

    /// <inheritdoc cref="IEditControl.Id"/>
    [Parameter] public string? Id { get; set; }
    
    /// <inheritdoc cref="IEditControl.IdPrefix"/>
    [Parameter] public string? IdPrefix { get; set; }
    
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
    
    /// <inheritdoc cref="IEditControl.IsLabelHidden"/>
    [Parameter] public bool IsLabelHidden { get; set; }

    /// <summary>
    /// False when the paired element is not labelable (the read-only view renders a div, and
    /// <c>label[for]</c> must reference a labelable element) — the label then renders without
    /// <c>for</c>; the read-only view names itself via <c>aria-labelledby</c>. Controls pass
    /// <c>ShowEditor</c>. Defaults to true.
    /// </summary>
    [Parameter] public bool IsForLabelable { get; set; } = true;

    // Resolved once per parameter-change cycle; the razor binds to these instead of calling the
    // helpers on every render path (the legend + label branches in FormLabel.razor evaluate
    // DisplayLabel/DisplayDescription twice otherwise).
    string _label = string.Empty;
    string? _description;
    bool _isRequired;

    string DisplayLabel() => _label;
    string? DisplayDescription() => _description;

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
            && ReferenceEquals(Attributes, _lastAttributes)
            && FieldIdentifier.Equals(_lastFieldIdentifier)
            && IsRequired == _lastIsRequiredParam
            && ReferenceEquals(FormOptions, _lastFormOptions))
            return;
        _inputsSeen = true;
        _lastLabelParam = Label;
        _lastDescriptionParam = Description;
        _lastAttributes = Attributes;
        _lastFieldIdentifier = FieldIdentifier;
        _lastIsRequiredParam = IsRequired;
        _lastFormOptions = FormOptions;

        // Attributes can be null when FormLabel is used outside the Edit* controls (EditDisplay supplies
        // no attribute list). Fall back to the explicit Label/Description rather than dropping them, and
        // never call GetLabelText with the default FieldIdentifier (its FieldName would be null).
        _label = Label ?? Attributes?.GetLabelText(FieldIdentifier) ?? string.Empty;
        _description = Description ?? Attributes?.Description();
        // Same resolution as the controls' aria-required (IsRequired param → [Required] attribute →
        // FormOptions.RequiredResolver) so the star and aria-required can never disagree.
        _isRequired = EditControlInit.IsRequired(Attributes, IsRequired, FormOptions, FieldIdentifier);
    }
}