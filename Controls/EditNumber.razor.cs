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

    /// <summary> The increment/decrement step for the number input. Defaults to 1.0.</summary>
    [Parameter] public decimal Step { get; set; } = 1.0m;

    /// <summary> Optional format string for displaying the number in read-only mode (e.g., "N2" for 2 decimal places).</summary>
    [Parameter] public string? Format { get; set; }

    /// <summary> Error message format string used when the value can't be parsed. <c>{0}</c> is replaced with the field name.</summary>
    [Parameter] public string ParsingErrorMessage { get; set; } = "The {0} field must be a number.";

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
                    decimal d => d.ToString(Format),
                    float f => f.ToString(Format),
                    double d => d.ToString(Format),
                    int i => i.ToString(Format),
                    long l => l.ToString(Format),
                    short s => s.ToString(Format),
                    byte b => b.ToString(Format),
                    sbyte sb => sb.ToString(Format),
                    uint ui => ui.ToString(Format),
                    ulong ul => ul.ToString(Format),
                    ushort us => us.ToString(Format),
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
