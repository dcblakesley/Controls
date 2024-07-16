using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;

namespace Controls;

public static class EnumHelpers
{
    public static string GetEnumDescription(this Enum value)
    {
        var type = value.GetType();
        var name = Enum.GetName(type, value)!;

        var field = type.GetField(name);
        if (field != null)
        {
            var attr = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
            if (attr != null)
                return attr.Description;

        }
        return "";
    }
    public static string? GetDescription(object value)
    {
        var fi = value.GetType().GetField(value.ToString() ?? string.Empty);

        var attributes = fi!.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];

        if (attributes != null && attributes.Any())
        {
            return attributes.First().Description;
        }

        return value.ToString();
    }
    public static string GetDisplayName(object value)
    {
        var fi = value.GetType().GetField(value.ToString() ?? string.Empty);

        var attributes = fi?.GetCustomAttributes(typeof(DisplayNameAttribute), false) as DisplayNameAttribute[];

        if (attributes != null && attributes.Any())
        {
            return attributes.First().DisplayName;
        }

        return "";
    }
    public static string GetName(this object value)
    {
        var fi = value.GetType().GetField(value.ToString() ?? string.Empty);
        var attributes = fi?.GetCustomAttributes(typeof(DisplayNameAttribute), false) as DisplayNameAttribute[];
        if (attributes != null && attributes.Any())
        {
            return attributes.First().DisplayName;
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
    public static string? GetToolTip(object value)
    {
        var fi = value.GetType().GetField(value.ToString() ?? string.Empty);

        var attributes = fi?.GetCustomAttributes(typeof(ToolTipAttribute), false) as ToolTipAttribute[];

        if (attributes != null && attributes.Any())
        {
            return attributes.First().Value;
        }

        return null;
    }
}

public class ToolTipAttribute(string value) : Attribute
{
    public string Value { get; protected set; } = value;
}
public class DisplayNameAttribute(string value) : Attribute
{
    public string DisplayName { get; protected set; } = value;
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

    public static List<Attribute> GetExpressionCustomAttributes<T>(Expression<Func<T>> accessor)
    {
        return GetExpressionMember(accessor).GetCustomAttributes().ToList();
    }

    public static IEnumerable<TAttribute> GetExpressionCustomAttributes<T, TAttribute>(Expression<Func<T>> accessor, bool inherit = false)
        where TAttribute : Attribute
    {
        return GetExpressionMember(accessor).GetCustomAttributes<TAttribute>(inherit);
    }

    public static string GetLabelText(IEnumerable<Attribute> attrs, FieldIdentifier fieldIdentifier)
    {
        // Order: DisplayNameAttribute, PropertyName
        var displayNameAttribute = attrs.OfType<DisplayNameAttribute>().FirstOrDefault();
        var labelText = displayNameAttribute?.DisplayName;
        if (string.IsNullOrEmpty(labelText))
        {
            labelText = fieldIdentifier.FieldName;
            // split by camel case
            labelText = string.Concat(labelText.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
        }

        return labelText;
    }
    public static string GetId(string? id, string? name)
    {
        // Ensure there is always an Id. Allow the users to set it manually, generate one from the Name, or generate a random one.
        var i = id ?? name?.Replace(" ", "");
        if (string.IsNullOrEmpty(i))
            i = Guid.NewGuid().ToString();
        return i;
    }
    public static string? GetDescription(List<Attribute> attrs)
    {
        var descriptionAttribute = attrs.OfType<DescriptionAttribute>().FirstOrDefault();
        return descriptionAttribute?.Description;
    }
    public static string? GetToolTip(List<Attribute> attrs)
    {
        var descriptionAttribute = attrs.OfType<ToolTipAttribute>().FirstOrDefault();
        return descriptionAttribute?.Value;
    }

    public static bool GetIsRequired(List<Attribute> attrs)
    {
        var requiredAttribute = attrs.OfType<RequiredAttribute>().FirstOrDefault();
        return requiredAttribute != null;
    }
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
}
