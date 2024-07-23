using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;

namespace Controls;

public static class EnumHelpers
{
    public static string GetName(this object value)
    {
        var fi = value.GetType().GetField(value.ToString() ?? string.Empty);
        var attributes = fi?.GetCustomAttributes(typeof(EnumDisplayNameAttribute), false) as EnumDisplayNameAttribute[];
        if (attributes != null && attributes.Any())
        {
            return attributes.First().Value;
        }
        
        var text = value.ToString();
        if (text != null)
        {
            // split by camel case
            text = string.Concat(text.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
            return text;
        }

        return "";
    }
}

public class ToolTipAttribute(string value) : Attribute
{
    public string Value { get; protected set; } = value;
}
public class EnumDisplayNameAttribute(string value) : Attribute
{
    public string Value { get; protected set; } = value;
}
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
            throw new ArgumentException($"The provided expression contains a {accessorBody.GetType().Name} which is not supported. {nameof(FieldIdentifier)} only supports simple member accessors (fields, properties) of an object.");
        }

        return memberExpression.Member;
    }

    public static List<Attribute> GetExpressionCustomAttributes<T>(Expression<Func<T>> accessor) => GetExpressionMember(accessor).GetCustomAttributes().ToList();
    
    // Basic Attributes
    public static bool IsRequired(this List<Attribute>? attrs) => attrs?.OfType<RequiredAttribute>().FirstOrDefault() != null;
    public static string? Description(this List<Attribute>? attrs) => attrs?.OfType<DescriptionAttribute>().FirstOrDefault()?.Description;
    public static string? ToolTip(this List<Attribute>? attrs) => attrs?.OfType<ToolTipAttribute>().FirstOrDefault()?.Value;
    public static string GetId(string? id, FieldIdentifier fieldIdentifier) => id ?? fieldIdentifier.FieldName.Replace(" ", "");

    // Complex
    public static (int? MinLength, int? MaxLength) GetMinAndMaxLengths(List<Attribute> attributes)
    {
        var min = 0;
        var max = 0;
        var stringLengthAttribute = attributes.OfType<StringLengthAttribute>().FirstOrDefault();
        if (stringLengthAttribute != null)
        {
            min = stringLengthAttribute.MinimumLength;
            max = stringLengthAttribute.MaximumLength;
        }

        var minLengthAttribute = attributes.OfType<MinLengthAttribute>().FirstOrDefault();
        if(minLengthAttribute != null)
        {
            min = Math.Max(min, minLengthAttribute.Length);
        }
        var maxLengthAttribute = attributes.OfType<MaxLengthAttribute>().FirstOrDefault();
        if (maxLengthAttribute != null)
        {
            max = Math.Max(max, maxLengthAttribute.Length);
        }

        return (min, max);
    }
    public static string GetLabelText(this List<Attribute>? attrs, FieldIdentifier fieldIdentifier)
    {
        // Order: DisplayNameAttribute, EnumDisplayNameAttribute, PropertyName
        var displayNameAttribute = attrs?.OfType<DisplayNameAttribute>().FirstOrDefault();
        var labelText = displayNameAttribute?.DisplayName;
        if (displayNameAttribute == null)
        {
            var enumDisplayName = attrs?.OfType<EnumDisplayNameAttribute>().FirstOrDefault();
            if(enumDisplayName != null)
            {
                labelText = enumDisplayName.Value;
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
