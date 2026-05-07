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

    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        var effectiveHidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;

        if (effectiveHidingMode == HidingMode.None)
            return true;

        var value = Value;
        var isNull = value == null;
        var isDefault = isNull || EqualityComparer<T>.Default.Equals(value, default);

        // Special handling for DateTime / DateTimeOffset (default value isn't null)
        if (!isNull && value is DateTime dateTime)
            isDefault = dateTime == default;
        else if (!isNull && value is DateTimeOffset dateTimeOffset)
            isDefault = dateTimeOffset == default;

        var isReadOnly = !IsEditMode || (FormOptions != null && !FormOptions.IsEditMode);

        return effectiveHidingMode switch
        {
            HidingMode.WhenReadOnlyAndNull => !(isReadOnly && isNull),
            HidingMode.WhenReadOnlyAndNullOrDefault => !(isReadOnly && isDefault),
            HidingMode.WhenNull => !isNull,
            HidingMode.WhenNullOrDefault => !isDefault,
            _ => true
        };
    }
}
