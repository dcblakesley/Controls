namespace Controls;

/// <summary> Edit control for nullable boolean values, displays as radio buttons (Yes/No/Not Set).</summary>
public partial class EditBoolNullRadio : EditControlBase<bool?>
{
    // Injected only for FocusAsync's group-focus call below.
    [Inject] IJSRuntime JS { get; set; } = default!;

    /// <inheritdoc cref="EditRadio{TValue}.FocusAsync"/>
    /// <remarks>
    /// The fourth radio group, on the same shared implementation as the other three -- "the checked
    /// radio, else the first enabled one" -- so a consumer switching between them gets the same
    /// behavior. This one does render its own <c>&lt;input type="radio"&gt;</c>s and could have
    /// captured element references instead; agreeing with <c>EditRadio</c> (which cannot, its radios
    /// being consumer-authored) was judged worth more than avoiding the JS hop for one control.
    /// </remarks>
    protected override ValueTask FocusCoreAsync() =>
        new(JsInteropEc.FocusGroupInput(JS, _id, "input[type=radio]", preferChecked: true, FormDefaults));

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

    /// <summary>
    /// Text to display for the true option. Falls back to the bound property's <c>[BoolText]</c>
    /// when unset -- see <see cref="EffectiveTrueText"/>. Defaults to "Yes".
    /// </summary>
    [Parameter] public string? TrueText { get; set; }

    /// <summary>
    /// Text to display for the false option. Falls back to the bound property's <c>[BoolText]</c>
    /// when unset -- see <see cref="EffectiveFalseText"/>. Defaults to "No".
    /// </summary>
    [Parameter] public string? FalseText { get; set; }

    /// <summary>
    /// Text to display for the null option. Falls back to the bound property's <c>[BoolText]</c>
    /// when unset -- see <see cref="EffectiveNullText"/>. Defaults to "Not Set".
    /// </summary>
    [Parameter] public string? NullText { get; set; }

    /// <summary>
    /// The text actually rendered for the true option: the <see cref="TrueText"/> parameter, else
    /// the model property's <c>[BoolText(TrueText = …)]</c>, else <c>"Yes"</c> -- the control's
    /// built-in default.
    /// </summary>
    string EffectiveTrueText => _attributes.BoolText(TrueText, static a => a.TrueText, "Yes");

    /// <summary>
    /// The text actually rendered for the false option: the <see cref="FalseText"/> parameter, else
    /// the model property's <c>[BoolText(FalseText = …)]</c>, else <c>"No"</c> -- the control's
    /// built-in default.
    /// </summary>
    string EffectiveFalseText => _attributes.BoolText(FalseText, static a => a.FalseText, "No");

    /// <summary>
    /// The text actually rendered for the null option: the <see cref="NullText"/> parameter, else
    /// the model property's <c>[BoolText(NullText = …)]</c>, else <c>"Not Set"</c> -- the control's
    /// built-in default.
    /// </summary>
    string EffectiveNullText => _attributes.BoolText(NullText, static a => a.NullText, "Not Set");

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

    // For bool? the "default" is null OR false — preserves prior behavior. The base
    // ShouldShowComponent handles the null branch; this override only addresses "false counts
    // as default too." Centralization also fixes a pre-existing latent bug where the
    // WhenReadOnly variants used bare IsEditMode and ignored form-wide FormOptions.IsEditMode.
    protected override bool IsValueDefault() => CurrentValue.HasValue && !CurrentValue.Value;

    string GetDisplayText(bool? value) => value switch
    {
        true => EffectiveTrueText,
        false => EffectiveFalseText,
        _ => EffectiveNullText
    };
}
