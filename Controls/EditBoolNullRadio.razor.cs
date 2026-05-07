namespace Controls;

/// <summary> Edit control for nullable boolean values, displays as radio buttons (Yes/No/Not Set).</summary>
public partial class EditBoolNullRadio : EditControlBase<bool?>
{
    // Component-specific parameters
    /// <summary> Expression that binds to the nullable boolean property in the model.</summary>
    [Parameter] public required Expression<Func<bool?>> Field { get; set; }

    /// <summary> When true, displays radio buttons horizontally. Defaults to true.</summary>
    [Parameter] public bool IsHorizontal { get; set; } = true;

    /// <summary> When true, displays the null/not set option. Defaults to true.</summary>
    [Parameter] public bool ShowNullOption { get; set; } = true;

    /// <summary> Text to display for the true option. Defaults to "Yes".</summary>
    [Parameter] public string TrueText { get; set; } = "Yes";

    /// <summary> Text to display for the false option. Defaults to "No".</summary>
    [Parameter] public string FalseText { get; set; } = "No";

    /// <summary> Text to display for the null option. Defaults to "Not Set".</summary>
    [Parameter] public string NullText { get; set; } = "Not Set";

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
    }

    void OnValueChanged(bool? value) => CurrentValue = value;

    protected override bool TryParseValueFromString(string? value, out bool? result, out string validationErrorMessage)
    {
        if (string.IsNullOrEmpty(value))
        {
            result = null;
            validationErrorMessage = null!;
            return true;
        }

        if (bool.TryParse(value, out bool boolValue))
        {
            result = boolValue;
            validationErrorMessage = null!;
            return true;
        }

        result = null;
        validationErrorMessage = "The value must be either true, false, or empty.";
        return false;
    }

    string DisplayLabel() => Label ?? _attributes.GetLabelText(FieldIdentifier);

    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        var hidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        return hidingMode switch
        {
            HidingMode.None => true,
            HidingMode.WhenNull => CurrentValue.HasValue,
            HidingMode.WhenNullOrDefault => CurrentValue.HasValue && CurrentValue.Value,
            HidingMode.WhenReadOnlyAndNull => IsEditMode || CurrentValue.HasValue,
            HidingMode.WhenReadOnlyAndNullOrDefault => IsEditMode || (CurrentValue.HasValue && CurrentValue.Value),
            _ => true
        };
    }

    string GetDisplayText(bool? value) => value switch
    {
        true => TrueText,
        false => FalseText,
        _ => NullText
    };
}
