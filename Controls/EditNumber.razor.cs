namespace Controls;

/// <summary> Edit control for numeric values, displays as a number input. Supports custom formatting and step values.</summary>
// T is annotated 'All' because TryParseValueFromString feeds it (via EditControlInit.TryConvert<T>)
// to BindConverter.TryConvertTo<T>, which declares that requirement for its TypeConverter fallback
// (mirrors the framework's InputNumber<T>).
public partial class EditNumber<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> : EditTextControlBase<T>
{
    // Component-specific parameters. Size and UpdateOn (+ UpdateEventName) live on
    // EditTextControlBase<TValue>, shared with EditString/EditTextArea/EditDateNative.

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
    /// The <c>step</c> attribute value actually rendered, already formatted (or null to omit the
    /// attribute entirely): the <see cref="Step"/> parameter or the model property's <c>[Step]</c>
    /// (InvariantCulture) when either is explicitly set; otherwise <c>"any"</c> for a non-integral
    /// <typeparamref name="T"/> (float/double/decimal and their nullable forms), so a fractional value
    /// isn't natively invalid; otherwise null for an integral <typeparamref name="T"/>, omitting the
    /// attribute so the native default (step 1, already correct for a whole number) applies -- matching
    /// the framework's own <c>InputNumber&lt;T&gt;</c>, which never renders a <c>step</c> attribute at
    /// all. The old unconditional <c>1.0m</c> default made every fractional value natively invalid on
    /// arrival (<c>step="1.0"</c> rejects <c>12.34</c>), which blocks a native form submit before
    /// <c>OnValidSubmit</c>/<c>OnSubmit</c> even fire, since <c>EditForm</c> emits no <c>novalidate</c>.
    /// </summary>
    string? EffectiveStep
    {
        get
        {
            var explicitStep = Step ?? _attributes.Step();
            if (explicitStep is not null) return explicitStep.Value.ToString(CultureInfo.InvariantCulture);
            return IsIntegralType ? null : "any";
        }
    }

    // The numeric type actually bound, with Nullable<T> unwrapped -- both shapes render the same step.
    static readonly Type UnderlyingNumericType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

    // Whole-number types, for which the native default step (1) is already correct and no attribute
    // needs to be rendered at all. Anything else (float/double/decimal, or an unrecognized T) falls
    // through to "any" -- safer than assuming a whole-number step for a type this list doesn't know.
    static readonly bool IsIntegralType = UnderlyingNumericType == typeof(int)
        || UnderlyingNumericType == typeof(long)
        || UnderlyingNumericType == typeof(short)
        || UnderlyingNumericType == typeof(byte)
        || UnderlyingNumericType == typeof(sbyte)
        || UnderlyingNumericType == typeof(uint)
        || UnderlyingNumericType == typeof(ulong)
        || UnderlyingNumericType == typeof(ushort);

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
    // Stays on this control rather than moving to EditTextControlBase: EditDateNative inherits that
    // base and deliberately renders no placeholder (the native date input shows its own format hint),
    // so hoisting would hand it a public parameter it ignores. The identical pair EditString and
    // EditTextArea declare lives on EditTextInputBase, which those two share and this control doesn't.
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

    /// <inheritdoc/>
    /// <remarks>
    /// This control's answer is <see cref="UpdateTrigger.Change"/>, and overriding it with
    /// <see cref="UpdateTrigger.Input"/> via <see cref="EditTextControlBase{TValue}.UpdateOn"/> is not
    /// free: browsers report a
    /// <c>type="number"</c> input's value as an empty string while the user is mid-way through typing a
    /// partial number ("-", "3.", "1e"), so per-keystroke binding flashes a spurious
    /// <see cref="ParsingErrorMessage"/> validation error on every keystroke -- which is exactly why
    /// Change is the default here (matching the framework's own <c>InputNumber&lt;T&gt;</c>).
    /// </remarks>
    protected override UpdateTrigger DefaultUpdateTrigger => UpdateTrigger.Change;

    /// <summary>
    /// True once <see cref="Prefix"/> or <see cref="Suffix"/> is in use -- the single computation
    /// site <see cref="EditInputShell.UsesAffixLayout"/> defines, so this control and the shell
    /// always agree on which layout renders. EditNumber never sets AllowClear/CountText/IsPassword
    /// (no clear/count/password toggle for numbers), so those arguments are always false/null here.
    /// </summary>
    bool UseAffixLayout => EditInputShell.UsesAffixLayout(Prefix, Suffix, false, null, false);

    /// <summary>
    /// The input's <c>class</c> attribute. Legacy mode carries <c>edit-input-legacy-padding</c> (the
    /// trailing-edge space InvalidIcon needs, formerly an inline style -- see
    /// <see cref="EditInputShell.UsesAffixLayout"/>'s remarks) with <see cref="EditTextControlBase{TValue}.Size"/>
    /// at its default otherwise reproducing today's exact string; affix mode adds <c>edit-affix-input</c>
    /// per <see cref="EditInputShell"/>'s contract instead, and a non-default Size appends its
    /// <see cref="EditInputShell.SizeClass"/> token.
    /// </summary>
    string InputClass => EditInputShell.BuildInputClass(
        UseAffixLayout ? "edit-input edit-number-input edit-affix-input" : "edit-input edit-number-input edit-input-legacy-padding",
        Size, CssClass);

    // Ported from Microsoft.AspNetCore.Components.Forms.InputNumber<T>, and identical to
    // EditDateNative<T>'s parse — hence the shared body in EditControlInit.TryConvert. BindConverter
    // handles every numeric primitive (int, long, short, sbyte, byte, decimal, float, double, plus
    // their unsigned + nullable variants); only ParsingErrorMessage differs between the two controls.
    protected override bool TryParseValueFromString(string? value, out T result, out string validationErrorMessage) =>
        EditControlInit.TryConvert(value, ParsingErrorMessage, FieldIdentifier.FieldName, out result, out validationErrorMessage);

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
