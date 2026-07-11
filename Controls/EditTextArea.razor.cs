namespace Controls;

/// <summary> Edit control for multi-line string values, displays as a textarea with configurable row count.</summary>
public partial class EditTextArea : EditControlBase<string?>
{
    // Component-specific parameters

    /// <summary> Placeholder text to display in the textarea when empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary> Number of visible text rows in the textarea. Defaults to 2.</summary>
    [Parameter] public int Rows { get; set; } = 2;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditTextArea)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    // Trivial parser — same as Microsoft's InputTextArea: pass the string through.
    protected override bool TryParseValueFromString(string? value, out string? result, out string validationErrorMessage)
    {
        result = value;
        validationErrorMessage = null!;
        return true;
    }

    // Empty string counts as "default" for the NullOrDefault hiding modes.
    protected override bool IsValueDefault() => string.IsNullOrEmpty(CurrentValue);
}
