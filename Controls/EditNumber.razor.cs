namespace Controls;

/// <summary> Edit control for numeric values, displays as a number input. Supports custom formatting and step values.</summary>
// T is annotated 'All' because TryParseValueFromString feeds it to BindConverter.TryConvertTo<T>,
// which declares that requirement for its TypeConverter fallback (mirrors the framework's InputNumber<T>).
public partial class EditNumber<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> : EditControlBase<T>
{
    // Component-specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<T>>? Field { get; set; }

    /// <summary>
    /// The increment/decrement step for the number input, rendered as the input's <c>step</c> attribute
    /// (InvariantCulture). Falls back to the bound property's <c>[Step]</c> when unset -- see
    /// <see cref="EffectiveStep"/>.
    /// </summary>
    [Parameter] public decimal? Step { get; set; }

    /// <summary>
    /// The step actually rendered: the <see cref="Step"/> parameter, else the model property's
    /// <c>[Step]</c>, else <c>1.0m</c> -- the same default the parameter used to carry directly.
    /// <see cref="AttributesHelper.Step"/> already degrades a non-positive or unconvertible attribute
    /// value to null, so this is the only place that old hardcoded default needs to live now.
    /// </summary>
    decimal EffectiveStep => Step ?? _attributes.Step() ?? 1.0m;

    /// <summary>
    /// The minimum allowed value, rendered as the input's <c>min</c> attribute (InvariantCulture, same
    /// type discipline as <see cref="Step"/>). Falls back to the bound property's
    /// <c>[MinValue]</c>/<c>[Range]</c> when unset -- see <see cref="EffectiveMin"/>.
    /// </summary>
    [Parameter] public decimal? Min { get; set; }

    /// <summary>
    /// The maximum allowed value, rendered as the input's <c>max</c> attribute (InvariantCulture, same
    /// type discipline as <see cref="Step"/>). Falls back to the bound property's
    /// <c>[MaxValue]</c>/<c>[Range]</c> when unset -- see <see cref="EffectiveMax"/>.
    /// </summary>
    [Parameter] public decimal? Max { get; set; }

    /// <summary>
    /// The minimum bound actually rendered: the <see cref="Min"/> parameter, else the model property's
    /// <c>[MinValue]</c>/<c>[Range]</c> lower bound. Null when neither is set (or the bound isn't
    /// representable as <see cref="decimal"/>), so the <c>min</c> attribute is omitted rather than
    /// rendered as an unbounded floor.
    /// </summary>
    decimal? EffectiveMin => Min ?? _attributes.MinNumber();

    /// <summary>
    /// The maximum bound actually rendered: the <see cref="Max"/> parameter, else the model property's
    /// <c>[MaxValue]</c>/<c>[Range]</c> upper bound. Null when neither is set (or the bound isn't
    /// representable as <see cref="decimal"/>), so the <c>max</c> attribute is omitted rather than
    /// rendered as an unbounded ceiling.
    /// </summary>
    decimal? EffectiveMax => Max ?? _attributes.MaxNumber();

    /// <summary>
    /// Placeholder text to display in the input when empty. Falls back to the bound property's
    /// <c>[Placeholder]</c>/<c>[Display(Prompt = "…")]</c> when unset -- see <see cref="EffectivePlaceholder"/>.
    /// </summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>
    /// The placeholder actually rendered: the <see cref="Placeholder"/> parameter, else the model
    /// property's <c>[Placeholder]</c>/<c>[Display(Prompt)]</c> text. Null when neither is set, so the
    /// attribute is omitted rather than rendered empty.
    /// </summary>
    string? EffectivePlaceholder => Placeholder ?? _attributes.Placeholder();

    /// <summary> Optional leading affix content (e.g. a currency symbol or icon), rendered by <see cref="EditInputShell"/>. Setting this switches the control into the shell's AntD-style affix layout.</summary>
    [Parameter] public RenderFragment? Prefix { get; set; }

    /// <summary> Optional custom trailing affix content, rendered by <see cref="EditInputShell"/> after the (absent, for EditNumber) clear button and character count. Setting this switches the control into the shell's AntD-style affix layout.</summary>
    [Parameter] public RenderFragment? Suffix { get; set; }

    /// <summary>
    /// Visual size, shared with the <c>Select</c> family's <see cref="SelectSize"/> (Default/Small/
    /// Large). Adds <c>edit-input-sm</c>/<c>edit-input-lg</c> to the input's class in both legacy and
    /// affix mode, and to the shell's affix wrapper in affix mode (via <see cref="EditInputShell.WrapperClass"/>).
    /// Unthemed these are inert hooks -- the opt-in <c>.edit-theme</c> section is what actually sizes
    /// them. <see cref="SelectSize.Default"/> adds no class (byte-identical legacy DOM).
    /// </summary>
    [Parameter] public SelectSize Size { get; set; }

    /// <summary>
    /// Optional format string for displaying the number in read-only mode (e.g., "N2" for 2 decimal
    /// places). Falls back to the bound property's <c>[DisplayFormat(DataFormatString = …)]</c> when
    /// unset -- see <see cref="EffectiveFormat"/>.
    /// </summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>
    /// The format string actually applied in read-only mode: the <see cref="Format"/> parameter, else
    /// the model property's <c>[DisplayFormat(DataFormatString = …)]</c> (normalized by
    /// <see cref="AttributesHelper.FormatString"/> to the bare token <c>ToString(format)</c> expects).
    /// Null when neither is set, so <see cref="GetFormattedNumber"/> falls through to the value's own
    /// default <c>ToString()</c> exactly as before.
    /// </summary>
    string? EffectiveFormat => Format ?? _attributes.FormatString();

    /// <summary> Error message format string used when the value can't be parsed. <c>{0}</c> is replaced with the field name.</summary>
    [Parameter] public string ParsingErrorMessage { get; set; } = "The {0} field must be a number.";

    /// <summary>
    /// Which DOM event commits keystrokes to <see cref="InputBase{TValue}.CurrentValue"/> --
    /// <see cref="UpdateTrigger.Input"/> (<c>oninput</c>) commits on every keystroke,
    /// <see cref="UpdateTrigger.Change"/> (<c>onchange</c>) commits on blur/Enter. Resolution order:
    /// this parameter, then the cascaded <see cref="FormDefaults.EffectiveUpdateOn"/>, then this
    /// control's own default of <see cref="UpdateTrigger.Change"/>. Choosing <see cref="UpdateTrigger.Input"/>
    /// here is not free: browsers report a <c>type="number"</c> input's value as an empty string while
    /// the user is mid-way through typing a partial number ("-", "3.", "1e"), so per-keystroke binding
    /// flashes a spurious <see cref="ParsingErrorMessage"/> validation error on every keystroke -- which
    /// is exactly why <see cref="UpdateTrigger.Change"/> is the default.
    /// </summary>
    [Parameter] public UpdateTrigger? UpdateOn { get; set; }

    /// <summary> The resolved DOM event name ("oninput" or "onchange") driving <c>@bind-value:event</c>, per <see cref="UpdateOn"/>'s resolution order.</summary>
    protected string UpdateEventName => ResolveUpdateEvent(UpdateOn, UpdateTrigger.Change);

    /// <summary>
    /// True once <see cref="Prefix"/> or <see cref="Suffix"/> is in use -- the single computation
    /// site <see cref="EditInputShell.UsesAffixLayout"/> defines, so this control and the shell
    /// always agree on which layout renders. EditNumber never sets AllowClear/CountText/IsPassword
    /// (no clear/count/password toggle for numbers), so those arguments are always false/null here.
    /// </summary>
    bool UseAffixLayout => EditInputShell.UsesAffixLayout(Prefix, Suffix, false, null, false);

    /// <summary>
    /// The input's <c>class</c> attribute. Legacy mode with <see cref="Size"/> at its default
    /// reproduces today's exact string (so a no-new-params render stays byte-identical); affix mode
    /// adds <c>edit-affix-input</c> per <see cref="EditInputShell"/>'s contract, and a non-default
    /// <see cref="Size"/> appends its <see cref="EditInputShell.SizeClass"/> token.
    /// </summary>
    string InputClass => EditInputShell.BuildInputClass(
        UseAffixLayout ? "edit-input edit-number-input edit-affix-input" : "edit-input edit-number-input",
        Size, CssClass);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditNumber<T>)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    // Ported from Microsoft.AspNetCore.Components.Forms.InputNumber<T>:
    // BindConverter handles every numeric primitive (int, long, short, sbyte, byte, decimal,
    // float, double, plus their unsigned + nullable variants).
    protected override bool TryParseValueFromString(string? value, out T result, out string validationErrorMessage)
    {
        if (BindConverter.TryConvertTo<T>(value, CultureInfo.InvariantCulture, out var parsedValue))
        {
            result = parsedValue!;
            validationErrorMessage = null!;
            return true;
        }

        result = default!;
        validationErrorMessage = string.Format(CultureInfo.InvariantCulture, ParsingErrorMessage, FieldIdentifier.FieldName);
        return false;
    }

    // Ported from InputNumber<T>, extended to every numeric primitive the parse side accepts —
    // the unsigned/byte types must format invariantly too, or a culture with a non-ASCII negative
    // sign (e.g. sv-SE's U+2212 for sbyte) renders a value the number input can't round-trip.
    protected override string? FormatValueAsString(T? value) => value switch
    {
        null => null,
        int @int => BindConverter.FormatValue(@int, CultureInfo.InvariantCulture),
        long @long => BindConverter.FormatValue(@long, CultureInfo.InvariantCulture),
        short @short => BindConverter.FormatValue(@short, CultureInfo.InvariantCulture),
        float @float => BindConverter.FormatValue(@float, CultureInfo.InvariantCulture),
        double @double => BindConverter.FormatValue(@double, CultureInfo.InvariantCulture),
        decimal @decimal => BindConverter.FormatValue(@decimal, CultureInfo.InvariantCulture),
        byte @byte => @byte.ToString(CultureInfo.InvariantCulture),
        sbyte @sbyte => @sbyte.ToString(CultureInfo.InvariantCulture),
        ushort @ushort => @ushort.ToString(CultureInfo.InvariantCulture),
        uint @uint => @uint.ToString(CultureInfo.InvariantCulture),
        ulong @ulong => @ulong.ToString(CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    // Numeric zero (any T) counts as "default" for the NullOrDefault hiding modes.
    // CurrentValue is guaranteed non-null here — the base method handles the null branch.
    protected override bool IsValueDefault() => Convert.ToDouble(CurrentValue) == 0;

    string? GetFormattedNumber()
    {
        try
        {
            if (Value != null)
            {
                return Value switch
                {
                    decimal d => d.ToString(EffectiveFormat),
                    float f => f.ToString(EffectiveFormat),
                    double d => d.ToString(EffectiveFormat),
                    int i => i.ToString(EffectiveFormat),
                    long l => l.ToString(EffectiveFormat),
                    short s => s.ToString(EffectiveFormat),
                    byte b => b.ToString(EffectiveFormat),
                    sbyte sb => sb.ToString(EffectiveFormat),
                    uint ui => ui.ToString(EffectiveFormat),
                    ulong ul => ul.ToString(EffectiveFormat),
                    ushort us => us.ToString(EffectiveFormat),
                    _ => Value.ToString()
                };
            }
        }
        catch (FormatException)
        {
            // Invalid custom Format string — show blank in read-only mode rather than throw.
        }

        return string.Empty;
    }
}
