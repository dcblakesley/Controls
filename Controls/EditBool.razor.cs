namespace Controls;

/// <summary> Edit control for boolean values, displays as a checkbox.</summary>
public partial class EditBool : EditControlBase<bool>
{
    // Component-specific parameters

    /// <summary> Expression that binds to the boolean property in the model.</summary>
    [Parameter] public required Expression<Func<bool>> Field { get; set; }

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

    string _displayLabel = string.Empty;
    string? _displayDescription;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
        // Resolve label/description once at init rather than per render (DisplayDescription was
        // evaluated twice each render — once for the null-check, once to render).
        _displayLabel = Label ?? _attributes.GetLabelText(FieldIdentifier);
        _displayDescription = Description ?? _attributes.Description();
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
