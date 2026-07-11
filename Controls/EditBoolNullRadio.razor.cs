namespace Controls;

/// <summary> Edit control for nullable boolean values, displays as radio buttons (Yes/No/Not Set).</summary>
public partial class EditBoolNullRadio : EditControlBase<bool?>
{
    // Component-specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<bool?>>? Field { get; set; }

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
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditBoolNullRadio)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
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

    // For bool? the "default" is null OR false — preserves prior behavior. The base
    // ShouldShowComponent handles the null branch; this override only addresses "false counts
    // as default too." Centralization also fixes a pre-existing latent bug where the
    // WhenReadOnly variants used bare IsEditMode and ignored form-wide FormOptions.IsEditMode.
    protected override bool IsValueDefault() => CurrentValue.HasValue && !CurrentValue.Value;

    string GetDisplayText(bool? value) => value switch
    {
        true => TrueText,
        false => FalseText,
        _ => NullText
    };
}
