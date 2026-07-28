namespace Controls.Helpers;

public static class AttributesHelper
{
    public static MemberInfo GetExpressionMember<T>(Expression<Func<T>> accessor)
    {
        var accessorBody = accessor.Body;

        // Unwrap casts to object
        if (accessorBody is UnaryExpression unaryExpression
            && unaryExpression.NodeType == ExpressionType.Convert
            && unaryExpression.Type == typeof(object))
        {
            accessorBody = unaryExpression.Operand;
        }

        if (!(accessorBody is MemberExpression memberExpression))
        {
            throw new ArgumentException(
                $"The provided expression contains a {accessorBody.GetType().Name} which is not supported. {nameof(FieldIdentifier)} only supports simple member accessors (fields, properties) of an object.");
        }

        return memberExpression.Member;
    }

    public static List<Attribute> GetExpressionCustomAttributes<T>(Expression<Func<T>> accessor) =>
        GetExpressionMember(accessor).GetCustomAttributes().ToList();

    // Basic Attributes
    public static string? Description(this List<Attribute>? attrs) =>
        attrs?.OfType<DescriptionAttribute>().FirstOrDefault()?.Description;

    public static string? Tooltip(this List<Attribute>? attrs) =>
        attrs?.OfType<ToolTipAttribute>().FirstOrDefault()?.Value;

    /// <summary>
    /// The model-declared placeholder/hint text for a field: <see cref="PlaceholderAttribute"/> first,
    /// then DataAnnotations' own <c>[Display(Prompt = "…")]</c> — the framework's existing "watermark"
    /// slot, honored here for the same reason <see cref="GetLabelText"/> honors <c>[Display(Name)]</c>
    /// (a model already annotated for MVC/Razor Pages needs no second attribute). <c>GetPrompt()</c>
    /// rather than <c>.Prompt</c> so a localized <c>[Display(Prompt=…, ResourceType=…)]</c> resolves
    /// through its resource manager instead of surfacing the raw resource key. Null when neither is
    /// present, so every caller can fall through to its own default.
    /// </summary>
    public static string? Placeholder(this List<Attribute>? attrs) =>
        attrs?.OfType<PlaceholderAttribute>().FirstOrDefault()?.Value
        ?? attrs?.OfType<DisplayAttribute>().FirstOrDefault()?.GetPrompt();

    public static string GetId(string? id, FormGroupOptions? formGroupOptions, string? idPrefix,
        FieldIdentifier fieldIdentifier)
    {
        // Explicit id always wins.
        if (!string.IsNullOrEmpty(id))
            return id;

        var fieldName = fieldIdentifier.FieldName;
        if (!string.IsNullOrEmpty(formGroupOptions?.Name))
            fieldName = formGroupOptions.Name + "-" + fieldName;

        if (idPrefix != null)
            fieldName = idPrefix + "-" + fieldName;

        return fieldName.Replace(" ", "");
    }

    // Complex
    public static (int? MinLength, int? MaxLength) GetMinAndMaxLengths(List<Attribute> attributes)
    {
        // null means "no length constraint" rather than a misleading 0; when both a StringLength and
        // a separate Min/MaxLength apply, take the tighter bound.
        int? min = null;
        int? max = null;
        var stringLengthAttribute = attributes.OfType<StringLengthAttribute>().FirstOrDefault();
        if (stringLengthAttribute != null)
        {
            min = stringLengthAttribute.MinimumLength;
            max = stringLengthAttribute.MaximumLength;
        }

        var minLengthAttribute = attributes.OfType<MinLengthAttribute>().FirstOrDefault();
        if (minLengthAttribute != null)
        {
            min = Math.Max(min ?? 0, minLengthAttribute.Length);
        }

        var maxLengthAttribute = attributes.OfType<MaxLengthAttribute>().FirstOrDefault();
        if (maxLengthAttribute != null)
        {
            // Upper bound: the tighter constraint is the SMALLER of the two (both validators run,
            // so the effective max is whichever rejects first). Math.Max here would report the
            // looser bound and break the MaxLength message rewrite.
            max = max is null ? maxLengthAttribute.Length : Math.Min(max.Value, maxLengthAttribute.Length);
        }

        return (min, max);
    }

    public static string GetLabelText(this List<Attribute>? attrs, FieldIdentifier fieldIdentifier)
    {
        // Order: DisplayNameAttribute, EnumDisplayNameAttribute, DisplayAttribute, PropertyName
        var displayNameAttribute = attrs?.OfType<DisplayNameAttribute>().FirstOrDefault();
        var labelText = displayNameAttribute?.DisplayName;

        if (displayNameAttribute == null)
        {
            var enumDisplayName = attrs?.OfType<EnumDisplayNameAttribute>().FirstOrDefault();
            if (enumDisplayName != null)
            {
                labelText = enumDisplayName.Value;
            }
        }

        if (string.IsNullOrEmpty(labelText))
        {
            // [Display(Name = …)] — DataAnnotations' own naming attribute. Honoring it keeps the
            // label consistent with the validation messages DataAnnotations generates (which use
            // [Display]), and with EnumHelpers.GetName, which already honors it for enum members.
            // GetName() (not .Name) so a localized [Display(Name=…, ResourceType=…)] resolves through
            // its resource manager instead of surfacing the raw resource key.
            var displayAttribute = attrs?.OfType<DisplayAttribute>().FirstOrDefault();
            var displayName = displayAttribute?.GetName();
            if (!string.IsNullOrEmpty(displayName))
            {
                labelText = displayName;
            }
        }

        if (string.IsNullOrEmpty(labelText))
        {
            labelText = fieldIdentifier.FieldName;
            // split by camel case
            labelText = string.Concat(labelText.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
        }

        return labelText;
    }
}

// Custom Attributes
public class ToolTipAttribute(string value) : Attribute
{
    public string Value { get; protected set; } = value;
}

public class EnumDisplayNameAttribute(string value) : Attribute
{
    public string Value { get; protected set; } = value;
}

/// <summary>
/// Declares a control's placeholder/hint text on the model property, so the hint lives next to the
/// field it describes instead of being repeated at every markup site (the same rationale as
/// <see cref="ToolTipAttribute"/> and <c>[Description]</c>). Every control that renders a placeholder
/// resolves it as: its own <c>Placeholder</c> parameter → this attribute → <c>[Display(Prompt)]</c> →
/// the control's built-in default. Controls without a placeholder concept (native date inputs, radios,
/// checkbox lists, file upload) ignore it.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class PlaceholderAttribute(string value) : Attribute
{
    public string Value { get; protected set; } = value;
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class MustBeTrueAttribute : ValidationAttribute
{
    public MustBeTrueAttribute()
    {
        ErrorMessage = "Must be checked";
    }

    public override bool IsValid(object? value)
    {
        return value is true;
    }
}