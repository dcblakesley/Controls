namespace Controls;

/// <summary> Edit control for date and date/time values, displays as a date input with customizable format.</summary>
public partial class EditDate<T> : EditControlBase<T>
{
    // Component-specific parameters

    /// <summary> Expression that binds to the date/datetime property in the model.</summary>
    [Parameter] public required Expression<Func<T>> Field { get; set; }

    /// <summary> Format string for displaying the date in read-only mode. Defaults to "MM-dd-yyyy".</summary>
    [Parameter] public string DateFormat { get; set; } = "MM-dd-yyyy";

    /// <summary> The HTML input type — Date, DateTimeLocal, Month, or Time. Defaults to Date.</summary>
    [Parameter] public InputDateType Type { get; set; } = InputDateType.Date;

    /// <summary> Error message format string used when the value can't be parsed. <c>{0}</c> is replaced with the field name.</summary>
    [Parameter] public string ParsingErrorMessage { get; set; } = "The {0} field must be a date.";

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
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

    string GetDisplayValue()
    {
        string valueAsString = CurrentValueAsString ?? string.Empty;
        if (string.IsNullOrEmpty(valueAsString))
            return string.Empty;

        return DateTime.Parse(valueAsString).ToUniversalTime().ToLocalTime().ToString(DateFormat);
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
