namespace Controls;

/// <summary> Edit control for boolean values, displays as a checkbox.</summary>
public partial class EditBool : EditControlBase<bool>
{
    // Component-specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<bool>>? Field { get; set; }

    /// <summary> When true, allows the checkbox to receive focus even when disabled. Defaults to true.</summary>
    [Parameter] public bool AllowFocusWhenDisabled { get; set; } = true;

    /// <summary> Text shown by the read-only view when the value is true. Defaults to "Yes". </summary>
    [Parameter] public string TrueText { get; set; } = "Yes";

    /// <summary> Text shown by the read-only view when the value is false. Defaults to "No". </summary>
    [Parameter] public string FalseText { get; set; } = "No";

    /// <summary>
    /// When true, falls back to the legacy behavior of rendering a disabled checkbox in read-only mode.
    /// Defaults to false — read-only mode now uses <see cref="ReadOnlyValue"/> with <see cref="TrueText"/>/<see cref="FalseText"/> like the other controls.
    /// </summary>
    [Parameter] public bool RenderAsCheckboxWhenReadOnly { get; set; }

    /// <summary>
    /// When true, renders the checkbox with a custom-drawn box (hidden native input + a sibling
    /// element that draws the visual state) instead of the bare native checkbox. Use this when you
    /// need <c>border-radius</c> or other shape styling that a native checkbox + accent-color cannot
    /// render in any current browser. Null (default) falls through to <see cref="FormOptions"/>, then
    /// any enclosing <see cref="Controls.FormDefaults"/>, then <see cref="FormOptions.DefaultUseStyledCheckbox"/>
    /// — see <see cref="Controls.FormDefaults"/> to set this once for a whole app or MFE.
    /// </summary>
    [Parameter] public bool? UseStyledCheckbox { get; set; }

    /// <summary> <see cref="UseStyledCheckbox"/> resolved through the FormOptions/FormDefaults/static chain. </summary>
    bool EffectiveUseStyledCheckbox => EditControlInit.UseStyledCheckbox(UseStyledCheckbox, FormOptions, FormDefaults);

    string _displayLabel = string.Empty;
    string? _displayDescription;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditBool)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // Resolved per parameter change (not per render — DisplayDescription used to be evaluated
        // twice each render) so a dynamic Label (localization switch, runtime text) is reflected;
        // resolving only at init froze the first value forever. No-op until InitState has run.
        if (_attributes is not null)
        {
            _displayLabel = Label ?? _attributes.GetLabelText(FieldIdentifier);
            _displayDescription = Description ?? _attributes.Description();
        }
    }

    // Checkboxes don't bind via string parsing — the value is set directly through CurrentValue
    // by HandleCheckboxChange below. This matches Microsoft's InputCheckbox behavior.
    protected override bool TryParseValueFromString(string? value, out bool result, out string validationErrorMessage)
        => throw new NotSupportedException(
            $"This component does not parse string inputs. Bind to the '{nameof(CurrentValue)}' property, not '{nameof(CurrentValueAsString)}'.");

    string DisplayLabel() => _displayLabel;
    string? DisplayDescription() => _displayDescription;

    // bool default is false. The base ShouldShowComponent already knows CurrentValue is non-null
    // here (bool is a value type), so this override only needs to flag "false == default".
    // Note: this fixes a pre-existing bug in WhenReadOnlyAndNullOrDefault where the old logic
    // (`!IsEditMode && CurrentValue`) showed only when read-only AND true — the centralized
    // behavior now correctly shows except when read-only AND default-false.
    protected override bool IsValueDefault() => !CurrentValue;

    void HandleCheckboxChange(ChangeEventArgs args)
    {
        // Only update the value if the checkbox is not disabled
        if (ShowEditor && !IsDisabled)
            CurrentValue = (bool)args.Value!;
    }
}
