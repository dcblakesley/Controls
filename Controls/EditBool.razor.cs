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

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
    }

    // Checkboxes don't bind via string parsing — the value is set directly through CurrentValue
    // by HandleCheckboxChange below. This matches Microsoft's InputCheckbox behavior.
    protected override bool TryParseValueFromString(string? value, out bool result, out string validationErrorMessage)
        => throw new NotSupportedException(
            $"This component does not parse string inputs. Bind to the '{nameof(CurrentValue)}' property, not '{nameof(CurrentValueAsString)}'.");

    string DisplayLabel() => Label ?? _attributes.GetLabelText(FieldIdentifier);
    string? DisplayDescription() => Description ?? _attributes.Description();

    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        var hidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        return hidingMode switch
        {
            HidingMode.None => true,
            HidingMode.WhenNull => true,
            HidingMode.WhenNullOrDefault => CurrentValue,
            HidingMode.WhenReadOnlyAndNull => true,
            HidingMode.WhenReadOnlyAndNullOrDefault => !IsEditMode && CurrentValue,
            _ => true
        };
    }

    void HandleCheckboxChange(ChangeEventArgs args)
    {
        // Only update the value if the checkbox is not disabled
        if (ShowEditor && !IsDisabled)
            CurrentValue = (bool)args.Value!;
    }
}
