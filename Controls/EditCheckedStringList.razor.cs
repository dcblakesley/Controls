namespace Controls;

/// <summary> Provides checkboxes for each input string (in Options), binds to a List of selected strings.</summary>
public partial class EditCheckedStringList : CheckedListControlBase<string>
{
    // Component-specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<List<string>>>? Field { get; set; }

    /// <summary> List of string options to display as checkboxes.</summary>
    [Parameter] public List<string> Options { get; set; } = [];

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditCheckedStringList)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }
}
