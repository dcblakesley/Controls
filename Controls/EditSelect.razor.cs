namespace Controls;

/// <summary>
/// Select component where you create the options within the markup yourself. <br/>
/// If you want an Enum to back the select, use <see cref="EditSelectEnum{TValue}"/> instead. <br/>
/// If you want to use a list of strings to back the select, use <see cref="EditSelectString{TValue}"/> instead.
/// </summary>
public partial class EditSelect<TValue> : EditControlBase<TValue>
{
    // Component-specific parameters

    /// <summary> Expression that binds to the property in the model.</summary>
    [Parameter] public required Expression<Func<TValue>> Field { get; set; }

    /// <summary> The <c>&lt;option&gt;</c> elements to render inside the select.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
    }

    // Ported from Microsoft.AspNetCore.Components.Forms.InputSelect<TValue>:
    // strings pass through; enums (and nullable enums) round-trip via BindConverter.
    protected override bool TryParseValueFromString(string? value, out TValue result, out string validationErrorMessage)
    {
        var typeOfValue = typeof(TValue);

        if (typeOfValue == typeof(string))
        {
            result = (TValue)(object)value!;
            validationErrorMessage = null!;
            return true;
        }

        if (typeOfValue.IsEnum || (Nullable.GetUnderlyingType(typeOfValue)?.IsEnum ?? false))
        {
            if (BindConverter.TryConvertTo<TValue>(value, CultureInfo.CurrentCulture, out var parsedValue))
            {
                result = parsedValue!;
                validationErrorMessage = null!;
                return true;
            }

            result = default!;
            validationErrorMessage = $"The {FieldIdentifier.FieldName} field is not valid.";
            return false;
        }

        // Fallback to BindConverter for any other primitive value type.
        if (BindConverter.TryConvertTo<TValue>(value, CultureInfo.CurrentCulture, out var fallback))
        {
            result = fallback!;
            validationErrorMessage = null!;
            return true;
        }

        result = default!;
        validationErrorMessage = $"The {FieldIdentifier.FieldName} field is not valid.";
        return false;
    }

    // Base IsValueDefault covers EqualityComparer<TValue>.Default behavior — no override needed.
}
