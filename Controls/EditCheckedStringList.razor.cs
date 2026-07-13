namespace Controls;

/// <summary> Provides checkboxes for each input string (in Options), binds to a List of selected strings.</summary>
public partial class EditCheckedStringList : EditControlListBase<string>
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

    /// <summary> Labels for the checkboxes.</summary>
    [Parameter] public string? LabelClass { get; set; }

    /// <summary> If true, the checkboxes will be displayed horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }

    /// <summary>
    /// When true, each checkbox renders with a custom-drawn box (hidden native input + a sibling
    /// element that draws the visual state) instead of the bare native checkbox — same opt-in as
    /// <see cref="EditBool.UseStyledCheckbox"/>. Null (default) falls through to <see cref="FormOptions"/>,
    /// then any enclosing <see cref="Controls.FormDefaults"/>, then <see cref="FormOptions.DefaultUseStyledCheckbox"/>.
    /// </summary>
    [Parameter] public bool? UseStyledCheckbox { get; set; }

    /// <summary> <see cref="UseStyledCheckbox"/> resolved through the FormOptions/FormDefaults/static chain. </summary>
    bool EffectiveUseStyledCheckbox => EditControlInit.UseStyledCheckbox(UseStyledCheckbox, FormOptions, FormDefaults);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditCheckedStringList)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }
}
