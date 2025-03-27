using System.Globalization;
using Controls.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Controls;

public partial class EditRadioEnum<TEnum>
{
    [Parameter] public string? Id { get; set; } 
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public required Expression<Func<TEnum>> Field { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public bool HasHorizontalRadioButtons { get; set; }
    [Parameter] public bool SortByName { get; set; }

    /// <summary> The enum type to provide the values for, must match the Value Parameter </summary>
    [Parameter]
    public required Type Type { get; set; }

    bool ShouldShowComponent => true;

    List<TEnum> GetOptions() => SortByName
        ? Enum.GetValues(Type).Cast<TEnum>().OrderBy(x => x).ToList()
        : Enum.GetValues(Type).Cast<TEnum>().ToList();

    [CascadingParameter] public FormOptions? FormOptions { get; set; } [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    string _id = string.Empty;
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, IdPrefix, _fieldIdentifier);
    }

    protected override bool TryParseValueFromString(string value, out TEnum result, out string validationErrorMessage)
    {
        // Lets Blazor convert the value for us
        if (BindConverter.TryConvertTo(value, CultureInfo.CurrentCulture, out TEnum parsedValue))
        {
            result = parsedValue;
            validationErrorMessage = null;
            return true;
        }

        // Map null/empty value to null if the bound object is nullable
        if (string.IsNullOrEmpty(value))
        {
            var nullableType = Nullable.GetUnderlyingType(typeof(TEnum));
            if (nullableType != null)
            {
                result = default;
                validationErrorMessage = null;
                return true;
            }
        }

        // The value is invalid => set the error message
        result = default;
        validationErrorMessage = $"The {FieldIdentifier.FieldName} field is not valid.";
        return false;
    }
}