namespace Controls;

/// <summary>
/// Edit control for date and date/time values, rendered as a native <c>&lt;input type="date"&gt;</c>
/// (or <c>datetime-local</c>/<c>month</c>/<c>time</c>, per <see cref="Type"/>) with a customizable
/// read-only format. Zero JS, styled entirely by <c>edit-controls.css</c>. For the AntD-style calendar
/// dropdown (the default date control), use <see cref="EditDate{T}"/> instead — the two controls
/// support the identical set of bound types and <see cref="Type"/> values, so the choice is purely
/// native input vs. calendar dropdown UX.
/// </summary>
// T is annotated 'All' because TryParseValueFromString feeds it to BindConverter.TryConvertTo<T>,
// which declares that requirement for its TypeConverter fallback (mirrors the framework's InputDate<T>).
public partial class EditDateNative<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> : EditControlBase<T>
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

    /// <summary> Format string for displaying the date in read-only mode. Defaults to "MM-dd-yyyy".</summary>
    [Parameter] public string DateFormat { get; set; } = "MM-dd-yyyy";

    /// <summary> The HTML input type — Date, DateTimeLocal, Month, or Time. Defaults to Date.</summary>
    [Parameter] public InputDateType Type { get; set; } = InputDateType.Date;

    /// <summary> Error message format string used when the value can't be parsed. <c>{0}</c> is replaced with the field name.</summary>
    [Parameter] public string ParsingErrorMessage { get; set; } = "The {0} field must be a date.";

    /// <summary>
    /// Visual size, shared with the <c>Select</c> family's <see cref="SelectSize"/> (Default/Small/
    /// Large). Adds <c>edit-input-sm</c>/<c>edit-input-lg</c> to the input's class. EditDateNative never
    /// enters the shell's affix mode (no Prefix/Suffix/clear/count/password params), so the wrapper
    /// class is passed through for consistency but never actually renders. Unthemed these are inert
    /// hooks -- the opt-in <c>.edit-theme</c> section is what actually sizes them.
    /// <see cref="SelectSize.Default"/> adds no class (byte-identical legacy DOM).
    /// </summary>
    [Parameter] public SelectSize Size { get; set; }

    /// <summary>
    /// Which DOM event commits keystrokes to <see cref="InputBase{TValue}.CurrentValue"/> --
    /// <see cref="UpdateTrigger.Input"/> (<c>oninput</c>) commits on every keystroke,
    /// <see cref="UpdateTrigger.Change"/> (<c>onchange</c>) commits on blur/Enter. Resolution order:
    /// this parameter, then the cascaded <see cref="FormDefaults.EffectiveUpdateOn"/>, then this
    /// control's own default of <see cref="UpdateTrigger.Change"/>. Choosing <see cref="UpdateTrigger.Input"/>
    /// here is not free: browsers report a partially-typed <c>type="date"</c>/<c>datetime-local</c>/
    /// <c>month</c>/<c>time</c> input's value as an empty string until the user finishes typing a
    /// valid value, so per-keystroke binding flashes a spurious <see cref="ParsingErrorMessage"/>
    /// validation error on every keystroke -- which is exactly why <see cref="UpdateTrigger.Change"/>
    /// is the default.
    /// </summary>
    [Parameter] public UpdateTrigger? UpdateOn { get; set; }

    /// <summary> The resolved DOM event name ("oninput" or "onchange") driving <c>@bind-value:event</c>, per <see cref="UpdateOn"/>'s resolution order.</summary>
    protected string UpdateEventName => ResolveUpdateEvent(UpdateOn, UpdateTrigger.Change);

    /// <summary>
    /// The input's <c>class</c> attribute. <see cref="Size"/> at its default reproduces today's exact
    /// string (byte-identical legacy DOM); otherwise appends <see cref="EditInputShell.SizeClass"/>'s
    /// token.
    /// </summary>
    string InputClass => EditInputShell.BuildInputClass("edit-input edit-date-input", Size, CssClass);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditDateNative<T>)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    // Ported from Microsoft.AspNetCore.Components.Forms.InputDate<T>:
    // BindConverter handles DateTime, DateTime?, DateTimeOffset, DateTimeOffset?, DateOnly, DateOnly?, TimeOnly, TimeOnly?.
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

    // Ported from InputDate<T>: format-string varies with Type so the value round-trips through the
    // browser's <input type="date|datetime-local|month|time"> in the format it expects.
    protected override string FormatValueAsString(T? value)
    {
        var format = Type switch
        {
            InputDateType.Date => "yyyy-MM-dd",
            InputDateType.DateTimeLocal => "yyyy-MM-ddTHH:mm:ss",
            InputDateType.Month => "yyyy-MM",
            InputDateType.Time => "HH:mm:ss",
            _ => "yyyy-MM-dd"
        };

        return value switch
        {
            DateTime dt => BindConverter.FormatValue(dt, format, CultureInfo.InvariantCulture),
            DateTimeOffset dto => BindConverter.FormatValue(dto, format, CultureInfo.InvariantCulture),
            DateOnly @do => BindConverter.FormatValue(@do, format, CultureInfo.InvariantCulture),
            TimeOnly to => BindConverter.FormatValue(to, format, CultureInfo.InvariantCulture),
            _ => string.Empty
        };
    }

    // Format the bound value directly by its type with DateFormat. (The old code re-parsed the
    // round-tripped editor string and ran ToUniversalTime().ToLocalTime(), which rendered TimeOnly
    // as a date and could shift dates across midnight in non-UTC zones.) The try/catch falls back
    // to the value's own ToString() if DateFormat is incompatible with the type (e.g. a date format
    // on a TimeOnly), so a mis-set format degrades instead of throwing.
    string GetDisplayValue()
    {
        try
        {
            return CurrentValue switch
            {
                null => string.Empty,
                DateTime dt => dt.ToString(DateFormat, CultureInfo.CurrentCulture),
                DateTimeOffset dto => dto.ToString(DateFormat, CultureInfo.CurrentCulture),
                DateOnly d => d.ToString(DateFormat, CultureInfo.CurrentCulture),
                TimeOnly t => t.ToString(DateFormat, CultureInfo.CurrentCulture),
                _ => CurrentValue.ToString() ?? string.Empty
            };
        }
        catch (FormatException)
        {
            return CurrentValue?.ToString() ?? string.Empty;
        }
    }

    // Detect default DateTime / DateTimeOffset even when boxed inside a nullable T —
    // EqualityComparer<DateTime?>.Default.Equals(default(DateTime), null) is false, but the
    // wrapped default value is still semantically empty for hiding purposes.
    protected override bool IsValueDefault() => CurrentValue switch
    {
        DateTime dt => dt == default,
        DateTimeOffset dto => dto == default,
        DateOnly d => d == default,
        TimeOnly t => t == default,
        _ => EqualityComparer<T>.Default.Equals(CurrentValue, default!)
    };
}
