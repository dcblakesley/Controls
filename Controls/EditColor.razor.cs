namespace Controls;

/// <summary>
/// Edit control for a color, backed by the <see cref="ColorPicker"/> UI-kit popup. Adds form binding,
/// validation, label, read-only view, and <see cref="FormOptions"/> support (the same contract every
/// other scalar control provides) on top of the picker's swatch/drag/type UX. Binds a plain
/// <c>string?</c>: 3/4/6/8-digit hex (with or without <c>#</c>) and <c>rgb()</c>/<c>rgba()</c> text go
/// in, normalized lowercase <c>#rrggbb</c> — or <c>#rrggbbaa</c> for a translucent color while
/// <see cref="ShowAlpha"/> is on — comes out.
/// </summary>
/// <remarks>
/// <para>
/// Validation-state ARIA reaches the picker's actual trigger button through <see cref="ColorPicker"/>'s
/// <c>AriaRequired</c>/<c>AriaInvalid</c>/<c>AriaDescribedBy</c>/<c>AriaErrorMessage</c> parameters —
/// the same forwarding shape <see cref="EditDate{T}"/> uses onto <see cref="DatePicker"/>, and for the
/// same reason: the consumer's own unmatched attributes land on the picker's outer
/// <c>.wss-color-picker</c> wrapper (its documented <c>AdditionalAttributes</c> target), which also
/// carries the EditContext state classes via <c>CssClass</c>.
/// </para>
/// <para>
/// A value the picker can't parse at all is treated as "no color" rather than an error — the trigger
/// shows the empty indicator and the field validates on its own merits (a <c>[Required]</c> string is
/// still satisfied by non-empty garbage; use a <c>[RegularExpression]</c> if the exact form matters).
/// Only a TYPED entry that fails to parse produces a message, via <see cref="ParsingErrorMessage"/>.
/// </para>
/// </remarks>
public partial class EditColor : EditControlBase<string?>
{
    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime (Blazor validates
    /// unmatched component parameters at <c>SetParametersAsync</c> time, not compile time). Remove
    /// the attribute from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<string?>>? Field { get; set; }

    /// <inheritdoc cref="ColorPicker.ShowAlpha"/>
    [Parameter] public bool ShowAlpha { get; set; } = true;
    /// <inheritdoc cref="ColorPicker.ShowText"/>
    [Parameter] public bool ShowText { get; set; }
    /// <inheritdoc cref="ColorPicker.AllowClear"/>
    [Parameter] public bool AllowClear { get; set; }
    /// <inheritdoc cref="ColorPicker.Presets"/>
    [Parameter] public IReadOnlyList<string>? Presets { get; set; }
    /// <inheritdoc cref="ColorPicker.PresetsLabel"/>
    [Parameter] public string PresetsLabel { get; set; } = "Presets";
    /// <inheritdoc cref="ColorPicker.Placement"/>
    [Parameter] public PopupPlacement Placement { get; set; } = PopupPlacement.Bottom;

    /// <summary>
    /// Error message format string used when a typed entry in the picker's HEX box can't be parsed as
    /// a color at all — i.e. the inner <see cref="ColorPicker"/> raises
    /// <see cref="ColorPicker.OnParseError"/>. <c>{0}</c> is replaced with the field name — same
    /// formatting as <see cref="EditDate{T}.ParsingErrorMessage"/>. Surfaces as a validation message
    /// via a dedicated <see cref="ValidationMessageStore"/> scoped to this control's own
    /// <see cref="FieldIdentifier"/> (see <see cref="OnPickerParseErrorAsync"/>), since this control
    /// never routes through <see cref="TryParseValueFromString"/> — the picker sets values through its
    /// own value callback, not string parsing. Cleared the moment a valid value next commits.
    /// </summary>
    [Parameter] public string ParsingErrorMessage { get; set; } = "The {0} field must be a color.";

    /// <summary>
    /// Base accessible name of the picker's trigger button. Null (default) uses the resolved field
    /// label — the <see cref="IEditControl.Label"/> parameter, or the property's
    /// <c>[DisplayName]</c>/auto-generated text — so the trigger's accessible name matches its visible
    /// <see cref="FormLabel"/> instead of <see cref="ColorPicker"/>'s generic "Color" default (which
    /// would otherwise win the accessible-name computation over the <c>label[for]</c> association,
    /// exactly as <see cref="EditDate{T}.InputLabel"/> documents for the date field). The current value
    /// is appended either way, so the rendered name reads e.g. "Brand Color: #ff0000".
    /// </summary>
    [Parameter] public string? TriggerLabel { get; set; }

    /// <inheritdoc cref="ColorPicker.EmptyLabel"/>
    [Parameter] public string EmptyLabel { get; set; } = "no color";
    /// <inheritdoc cref="ColorPicker.PanelLabel"/>
    [Parameter] public string PanelLabel { get; set; } = "Choose color";
    /// <inheritdoc cref="ColorPicker.SaturationLabel"/>
    [Parameter] public string SaturationLabel { get; set; } = "Saturation and brightness";
    /// <inheritdoc cref="ColorPicker.SaturationValueTextFormat"/>
    [Parameter] public string SaturationValueTextFormat { get; set; } = "Saturation {0}%, brightness {1}%";
    /// <inheritdoc cref="ColorPicker.HueLabel"/>
    [Parameter] public string HueLabel { get; set; } = "Hue";
    /// <inheritdoc cref="ColorPicker.AlphaLabel"/>
    [Parameter] public string AlphaLabel { get; set; } = "Opacity";
    /// <inheritdoc cref="ColorPicker.ClearLabel"/>
    [Parameter] public string ClearLabel { get; set; } = "Clear color";
    /// <inheritdoc cref="ColorPicker.FormatLabel"/>
    [Parameter] public string FormatLabel { get; set; } = "Color format";
    /// <inheritdoc cref="ColorPicker.HexLabel"/>
    [Parameter] public string HexLabel { get; set; } = "Hex";
    /// <inheritdoc cref="ColorPicker.RedLabel"/>
    [Parameter] public string RedLabel { get; set; } = "Red";
    /// <inheritdoc cref="ColorPicker.GreenLabel"/>
    [Parameter] public string GreenLabel { get; set; } = "Green";
    /// <inheritdoc cref="ColorPicker.BlueLabel"/>
    [Parameter] public string BlueLabel { get; set; } = "Blue";
    /// <inheritdoc cref="ColorPicker.AlphaPercentLabel"/>
    [Parameter] public string AlphaPercentLabel { get; set; } = "Alpha percent";

    string EffectiveTriggerLabel => TriggerLabel ?? Label ?? _attributes.GetLabelText(_fieldIdentifier);

    // The picker sets the value through its own ValueChanged callback, not string parsing -- mirrors
    // EditDate's contract for a wrapped UI-kit engine. Binding to CurrentValueAsString (the debug
    // bound-value display excepted, which only ever reads it) is unsupported.
    protected override bool TryParseValueFromString(string? value, out string? result, out string validationErrorMessage)
        => throw new NotSupportedException(
            $"{nameof(EditColor)} does not parse string input; it binds via the ColorPicker value callback.");

    // Dedicated store for OnPickerParseErrorAsync's message -- a separate instance from whatever
    // DataAnnotationsValidator already maintains for this same EditContext, since this control can't
    // route a parse failure through TryParseValueFromString (see its NotSupportedException above) the
    // way InputBase's built-in mechanism would. Multiple independent stores over one EditContext
    // compose fine -- each only ever touches the entries it added itself. Same shape as EditDate's.
    ValidationMessageStore? _parseErrorMessages;

    /// <summary>
    /// Raised by the inner <see cref="ColorPicker"/> when a typed HEX entry can't be parsed as a color
    /// at all. Mirrors <see cref="EditDate{T}"/>'s equivalent (and, through it, the shape of
    /// <c>InputBase&lt;T&gt;.SetCurrentValueAsStringAsync</c>'s own built-in parsing-error path): clear
    /// this field's prior entry, add the formatted message, and notify — just against a store this
    /// control owns instead of InputBase's private one, which is never reached here.
    /// </summary>
    Task OnPickerParseErrorAsync(string text)
    {
        if (EditContext is null) return Task.CompletedTask;
        _parseErrorMessages ??= new ValidationMessageStore(EditContext);
        _parseErrorMessages.Clear(FieldIdentifier);
        _parseErrorMessages.Add(FieldIdentifier,
            string.Format(CultureInfo.InvariantCulture, ParsingErrorMessage, FieldIdentifier.FieldName));
        // CurrentValue never changed (the bad text was reverted, not committed) -- notify explicitly,
        // same as InputBase's own equivalent failure path, so FormOptions/consumers watching field
        // changes still see this as a touch.
        EditContext.NotifyFieldChanged(FieldIdentifier);
        EditContext.NotifyValidationStateChanged();
        return Task.CompletedTask;
    }

    void OnValueChanged(string? value)
    {
        // A value only ever reaches here once the picker itself successfully committed it -- clear any
        // stale parse-error message from a prior unparseable entry so it can never outlive the very
        // next valid commit.
        if (_parseErrorMessages is not null && EditContext is not null)
        {
            _parseErrorMessages.Clear(FieldIdentifier);
            EditContext.NotifyValidationStateChanged();
        }
        CurrentValue = value;
    }

    /// <summary>
    /// Drops any outstanding parse-error message when the control unmounts. The store's entries live on
    /// the <see cref="EditContext"/>, not on this component, so a control removed while showing a parse
    /// error (an <c>IsHidden</c>/<see cref="HidingMode"/> toggle, a tab switch) would otherwise leave
    /// the message behind for a <c>ValidationView</c> summary to link to a field that no longer renders.
    /// Only ever touches entries this control added — see <see cref="_parseErrorMessages"/>.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _parseErrorMessages is not null && EditContext is not null)
        {
            _parseErrorMessages.Clear(FieldIdentifier);
            EditContext.NotifyValidationStateChanged();
        }
        base.Dispose(disposing);
    }

    // The validation-state ARIA goes through ColorPicker's dedicated Aria* parameters (straight onto
    // its trigger button); this splat carries only the consumer's own attributes plus the state
    // classes, landing on the picker's outer wrapper (its documented AdditionalAttributes target).
    // Shared builder with the two date controls -- see BuildPickerAttributes's own remarks.
    IReadOnlyDictionary<string, object> PickerAttributes => EditControlInit.BuildPickerAttributes(AdditionalAttributes, CssClass);

    /// <summary>
    /// The read-only view's text: the normalized form of the bound value, or empty when there is none —
    /// including for a value the picker itself can't parse, so read-only and edit mode agree about what
    /// counts as "no color".
    /// </summary>
    string GetDisplayValue() =>
        CurrentValue is { Length: > 0 } text && ColorMath.TryParse(text, out var color)
            ? ColorMath.ToHex(color, ShowAlpha)
            : string.Empty;

    // Empty string counts as semantically empty for a color field, same as the text controls' own
    // rule (EditTextInputBase.IsValueDefault) -- the base's EqualityComparer default would only
    // recognize null.
    protected override bool IsValueDefault() => string.IsNullOrEmpty(CurrentValue);
}
