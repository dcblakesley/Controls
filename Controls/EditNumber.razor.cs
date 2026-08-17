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
    /// rendered as an unbounded floor. Passes <see cref="UnderlyingNumericType"/> so a <c>[Range]</c>
    /// bound is only treated as vacuous when it's <typeparamref name="T"/>'s OWN extreme -- see
    /// <see cref="Helpers.RangeSentinels"/>.
    /// </summary>
    decimal? EffectiveMin => Min ?? _attributes.MinNumber(UnderlyingNumericType);

    /// <summary>
    /// The maximum bound actually rendered: the <see cref="Max"/> parameter, else the model property's
    /// <c>[MaxValue]</c>/<c>[Range]</c> upper bound. Null when neither is set (or the bound isn't
    /// representable as <see cref="decimal"/>), so the <c>max</c> attribute is omitted rather than
    /// rendered as an unbounded ceiling. Passes <see cref="UnderlyingNumericType"/> -- see
    /// <see cref="EffectiveMin"/>.
    /// </summary>
    decimal? EffectiveMax => Max ?? _attributes.MaxNumber(UnderlyingNumericType);

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

    /// <summary>
    /// Opt-in AntD-style stepper: renders a minus button before the editor and a plus button after
    /// it, as one joined input group. Off by default, and while off this control renders exactly
    /// today's markup -- no group wrapper, no buttons (see <see cref="StepperGroupClass"/>). Each
    /// press moves the value by <see cref="StepAmount"/>, clamped to
    /// <see cref="EffectiveMin"/>/<see cref="EffectiveMax"/>.
    /// </summary>
    /// <remarks>
    /// Two deliberate deviations from AntD's <c>InputNumber</c> handlers. The buttons are laid out
    /// horizontally (flanking the editor) rather than stacked up/down inside the box, so they stay a
    /// comfortable pointer target at every <see cref="EditTextControlBase{TValue}.Size"/> without the
    /// two 16px-tall halves AntD's vertical stack produces. And there is no press-and-hold
    /// auto-repeat: a held button steps once. Keyboard users step with the native input's own
    /// Up/Down arrows -- which is why the buttons carry <c>tabindex="-1"</c> and are not tab stops
    /// (matching AntD's own handlers): tabbing through a form should not have to pass three stops per
    /// numeric field to reach the next one.
    /// </remarks>
    [Parameter] public bool ShowStepper { get; set; }

    /// <summary>
    /// Overrides the stepper's decrement-button accessible name (default:
    /// <c>"Decrease {ResolvedLabel}"</c>, e.g. "Decrease Quantity"). Folding the field's own label in
    /// follows the same rule as <see cref="EditTextInputBase.ClearButtonLabel"/>: a form with two
    /// stepper fields otherwise renders two buttons both named "Decrease", which a screen-reader user
    /// browsing a button list can't tell apart. No effect unless <see cref="ShowStepper"/> is set.
    /// </summary>
    [Parameter] public string? DecreaseButtonLabel { get; set; }

    /// <summary> Overrides the stepper's increment-button accessible name (default: <c>"Increase {ResolvedLabel}"</c>) -- see <see cref="DecreaseButtonLabel"/>.</summary>
    [Parameter] public string? IncreaseButtonLabel { get; set; }

    /// <summary> The decrement button's accessible name actually rendered: the <see cref="DecreaseButtonLabel"/> parameter, else <c>"Decrease {<see cref="EditTextControlBase{TValue}.ResolvedLabel"/>}"</c>.</summary>
    string EffectiveDecreaseButtonLabel => DecreaseButtonLabel ?? $"Decrease {ResolvedLabel}";

    /// <summary> The increment button's accessible name actually rendered: the <see cref="IncreaseButtonLabel"/> parameter, else <c>"Increase {<see cref="EditTextControlBase{TValue}.ResolvedLabel"/>}"</c>.</summary>
    string EffectiveIncreaseButtonLabel => IncreaseButtonLabel ?? $"Increase {ResolvedLabel}";

    /// <summary>
    /// The stepper group's class list -- <c>edit-number-stepper</c> plus this control's
    /// <see cref="EditInputShell.SizeClass"/> token, so the buttons track the same
    /// <c>--edit-control-height-sm/-lg</c> the editor beside them does. Only ever consulted inside the
    /// <see cref="ShowStepper"/> branch, so a default-mode render emits no group element at all.
    /// </summary>
    string StepperGroupClass => $"edit-number-stepper {EditInputShell.SizeClass(Size)}".TrimEnd();

    /// <summary>
    /// How far one stepper press moves the value: the explicitly-configured step (the
    /// <see cref="Step"/> parameter, else the model property's <c>[Step]</c>) when it's a positive
    /// number, otherwise 1. The fallback covers both shapes <see cref="EffectiveStep"/> renders
    /// without a number -- <c>"any"</c> for a non-integral <typeparamref name="T"/> and no attribute
    /// at all for an integral one -- since both mean "the control imposes no increment", and a
    /// press still has to move by something. A non-positive configured step falls back too: stepping
    /// by zero is a no-op button and stepping by a negative would invert both arrows.
    /// </summary>
    decimal StepAmount => (Step ?? _attributes.Step()) is { } step && step > 0 ? step : 1m;

    /// <summary>
    /// True once the bound value already sits at or past <see cref="EffectiveMin"/> -- the press would
    /// clamp straight back to where it started, so the button is natively <c>disabled</c> rather than
    /// silently doing nothing (which also removes it from the accessibility tree's actionable set).
    /// An empty (null) value is never "at" a bound: its first press steps from zero.
    /// </summary>
    bool IsAtMin => IsAtBound(EffectiveMin, -1);

    /// <summary> True once the bound value already sits at or past <see cref="EffectiveMax"/> -- see <see cref="IsAtMin"/>.</summary>
    bool IsAtMax => IsAtBound(EffectiveMax, 1);

    bool IsAtBound(decimal? bound, int direction)
    {
        if (bound is not { } limit || CurrentValue is null || !TryGetDecimalValue(out var current)) return false;
        return direction < 0 ? current <= limit : current >= limit;
    }

    /// <summary>
    /// Moves the bound value by <paramref name="direction"/> x <see cref="StepAmount"/>, clamped to
    /// whichever of <see cref="EffectiveMin"/>/<see cref="EffectiveMax"/> is set, and commits through
    /// <see cref="InputBase{TValue}.CurrentValueAsString"/> -- the same parse/validate/notify path a
    /// typed entry takes, so the decimal-to-<typeparamref name="T"/> conversion, the
    /// <see cref="ParsingErrorMessage"/> and the field-changed notification all come for free. (A
    /// fractional <see cref="Step"/> on an integral <typeparamref name="T"/> therefore surfaces as a
    /// parse error rather than being silently rounded -- it's a consumer configuration error, and the
    /// native input's own arrows behave the same way.)
    /// </summary>
    void StepValue(int direction)
    {
        // Belt-and-braces: the buttons are natively disabled while the control is, so this only fires
        // if something dispatches the click programmatically.
        if (IsDisabled) return;

        decimal next;
        try
        {
            // Boxed, because T carries no numeric constraint -- same reason FormatNumber switches on
            // the CLR type rather than an interface. A null (empty nullable) value steps from zero, so
            // the first press on an empty field yields +/- one step instead of nothing.
            var current = CurrentValue is null ? 0m : Convert.ToDecimal(CurrentValue, CultureInfo.InvariantCulture);
            next = current + direction * StepAmount;
        }
        catch (OverflowException)
        {
            // A double/float bound outside decimal's range, or a step that would carry the value past
            // decimal.MaxValue. Drop the press rather than throw out of the click handler.
            return;
        }

        if (EffectiveMin is { } min && next < min) next = min;
        if (EffectiveMax is { } max && next > max) next = max;

        CurrentValueAsString = next.ToString(CultureInfo.InvariantCulture);
    }

    // CurrentValue as a decimal, false when it doesn't fit in one (a double/float beyond decimal's
    // range). Only reached with a non-null CurrentValue.
    bool TryGetDecimalValue(out decimal value)
    {
        try
        {
            value = Convert.ToDecimal(CurrentValue, CultureInfo.InvariantCulture);
            return true;
        }
        catch (OverflowException)
        {
            value = 0m;
            return false;
        }
    }

    /// <summary> Optional leading affix content (e.g. a currency symbol or icon), rendered by <see cref="EditInputShell"/>. Setting this switches the control into the shell's AntD-style affix layout.</summary>
    [Parameter] public RenderFragment? Prefix { get; set; }

    /// <summary> Optional custom trailing affix content, rendered by <see cref="EditInputShell"/> after the (absent, for EditNumber) clear button and character count. Setting this switches the control into the shell's AntD-style affix layout.</summary>
    [Parameter] public RenderFragment? Suffix { get; set; }

    /// <summary>
    /// Open-vocabulary autofill hints, rendered as an HTML <c>&lt;datalist&gt;</c> wired to the input
    /// via <c>list=</c> -- same feature and contract as <see cref="EditString.Suggestions"/> (see its
    /// remarks for the null-vs-empty distinction and why this differs from the closed-vocabulary Select
    /// controls). <c>list</c> is valid on <c>type="number"</c>, and EditNumber has no password mode to
    /// suppress it for.
    /// </summary>
    [Parameter] public IEnumerable<string>? Suggestions { get; set; }

    /// <summary> Backing store for <see cref="SuggestionsListId"/> -- see <see cref="EditString.SuggestionsListId"/>.</summary>
    string? _suggestionsListId;

    /// <summary>
    /// The id of the <c>&lt;datalist&gt;</c> this control renders (and the input's <c>list=</c> points
    /// at), or null when <see cref="Suggestions"/> is unset. A fresh <c>dl-{guid}</c> per component
    /// instance, matching <see cref="EditString.SuggestionsListId"/> -- see its remarks for why this one
    /// id is deliberately not derived from the control's element id (a list of rows bound to the same
    /// property would otherwise emit one shared datalist id and show every row the FIRST row's
    /// suggestions).
    /// </summary>
    string? SuggestionsListId =>
        Suggestions is not null ? _suggestionsListId ??= $"dl-{Guid.NewGuid():N}" : null;

    /// <summary>
    /// The <c>list</c> attribute contribution -- see <see cref="EditString.SuggestionsInputAttributes"/>.
    /// </summary>
    IReadOnlyDictionary<string, object>? SuggestionsInputAttributes =>
        SuggestionsListId is { } id ? new Dictionary<string, object>(1) { ["list"] = id } : null;

    /// <summary>
    /// The input's full <c>@attributes</c> splat: the consumer's unmatched attributes with this
    /// control's own state attributes (<see cref="EditControlBase{TValue}.EditorStateAttributes"/> --
    /// <c>disabled</c>/<c>aria-required</c>/<c>aria-invalid</c>/<c>aria-errormessage</c>) and the
    /// <c>list</c> contribution (<see cref="SuggestionsInputAttributes"/>) layered on top -- see
    /// <see cref="EditString.EditorAttributes"/>'s and
    /// <see cref="EditControlBase{TValue}.EditorStateAttributes"/>'s remarks for why every CONDITIONAL
    /// attribute must be folded into this dictionary rather than written as its own explicit attribute
    /// (a null explicit frame erases a consumer's splatted same-named attribute outright, not merely
    /// omitting itself).
    /// </summary>
    IReadOnlyDictionary<string, object>? EditorAttributes =>
        AttributeSplat.RestWith(
            AttributeSplat.RestWith(AdditionalAttributes, EditorStateAttributes),
            SuggestionsInputAttributes);

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
    protected override string? FormatValueAsString(T? value) =>
        value is null ? null : FormatNumber(value, null, CultureInfo.InvariantCulture);

    // Numeric zero (any T) counts as "default" for the NullOrDefault hiding modes.
    // CurrentValue is guaranteed non-null here — the base method handles the null branch.
    protected override bool IsValueDefault() => Convert.ToDouble(CurrentValue) == 0;

    string? GetFormattedNumber()
    {
        try
        {
            if (Value != null)
                return FormatNumber(Value, EffectiveFormat, CultureInfo.CurrentCulture);
        }
        catch (FormatException)
        {
            // Invalid custom Format string — show blank in read-only mode rather than throw.
        }

        return string.Empty;
    }

    /// <summary>
    /// The one numeric-type switch backing both <see cref="FormatValueAsString"/> (edit-mode,
    /// <paramref name="format"/> null, <paramref name="provider"/> always <see cref="CultureInfo.InvariantCulture"/>
    /// so the native number input round-trips regardless of the current culture) and
    /// <see cref="GetFormattedNumber"/> (read-only, <paramref name="format"/> the optional
    /// <see cref="EffectiveFormat"/>, <paramref name="provider"/> <see cref="CultureInfo.CurrentCulture"/>).
    /// Previously two hand-synced 11-case switches over the same types -- a type added to one and not
    /// the other silently degraded to the naked <c>value.ToString()</c> fallback for whichever path was
    /// missed. <paramref name="value"/> is boxed (<typeparamref name="T"/> isn't itself constrained to
    /// a numeric interface), so the switch still dispatches on the CLR type actually bound.
    /// </summary>
    static string? FormatNumber(object value, string? format, IFormatProvider provider) => value switch
    {
        int i => i.ToString(format, provider),
        long l => l.ToString(format, provider),
        short s => s.ToString(format, provider),
        float f => f.ToString(format, provider),
        double d => d.ToString(format, provider),
        decimal m => m.ToString(format, provider),
        byte b => b.ToString(format, provider),
        sbyte sb => sb.ToString(format, provider),
        ushort us => us.ToString(format, provider),
        uint ui => ui.ToString(format, provider),
        ulong ul => ul.ToString(format, provider),
        _ => value.ToString()
    };
}
