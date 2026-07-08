using System.Collections.Concurrent;

namespace Controls;

/// <summary> Validation shown under the input field when it doesn't meet the requirements based on DataAnnotations. </summary>
public partial class FieldValidationDisplay
{
    // The value-type reflection is a pure function of (model type, field name), but OnParametersSet
    // re-runs on every parameter cycle — the List/FieldIdentifier parameters defeat Blazor's change
    // skip, so a 50-field form otherwise paid 50 GetProperty reflections per keystroke. Memoize it.
    static readonly ConcurrentDictionary<(Type, string), string> _valueTypeCache = new();
    [CascadingParameter] EditContext? EditContext { get; set; }
    [CascadingParameter] FormOptions? FormOptions { get; set; }
    [CascadingParameter] FormDefaults? FormDefaults { get; set; }

    [Parameter] public required FieldIdentifier FieldIdentifier { get; set; }
    [Parameter] public required List<Attribute> Attributes { get; set; }
    [Parameter] public string? Id { get; set; }
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public string? Label { get; set; }

    bool _isRequired;
    int? _minCharacters;
    int? _maxCharacters;
    string _fieldName = string.Empty;
    string _label = string.Empty;
    string? _valueType;

    // Recomputed on parameter change (not just init): a dynamic Label must be reflected in the
    // rewritten messages ("Old Label is required" was frozen forever), and the list controls
    // re-derive their FieldIdentifier when the model/EditContext is swapped.
    protected override void OnParametersSet()
    {
        _isRequired = Attributes.Any(x => x is RequiredAttribute);
        var minAndMax = AttributesHelper.GetMinAndMaxLengths(Attributes);
        _minCharacters = minAndMax.MinLength;
        _maxCharacters = minAndMax.MaxLength;
        _fieldName = FieldIdentifier.FieldName;
        _label = Label ?? Attributes.GetLabelText(FieldIdentifier);
        _valueType = _valueTypeCache.GetOrAdd(
            (FieldIdentifier.Model.GetType(), FieldIdentifier.FieldName),
            static key => key.Item1.GetProperty(key.Item2)?.PropertyType?.ToString() ?? string.Empty);
        // Field registration with FormOptions.FieldIdentifiers moved to EditControlBase.InitState
        // (and the list/radio equivalents) so it runs once per control regardless of whether
        // this validation display is conditionally rendered.
    }

    // Per-form FormOptions → per-tree FormDefaults (Effective* walks nested instances per property)
    // → process-wide static. DefaultShowFieldNameInValidation is a *static* member: in `FormOptions.X`
    // the name binds to the type, not the (possibly-null) cascaded instance, so the final ?? fallback
    // is null-safe despite appearances.
    bool ShowFieldNameInValidation =>
        FormOptions?.ShowFieldNameInValidation ?? FormDefaults?.EffectiveShowFieldNameInValidation ?? FormOptions.DefaultShowFieldNameInValidation;

    /// <summary> Overrides the default validation messages. </summary>
    string GetValidationMessage(string message, bool showLabel) =>
        ValidationHelper.GetValidationMessage(message, _fieldName, _label, _valueType, _maxCharacters, _minCharacters, showLabel);

    /// <summary> Overrides the default validation messages, using the form option to determine label visibility. </summary>
    string GetValidationMessage(string message) =>
        GetValidationMessage(message, ShowFieldNameInValidation);
}